using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Domain.Entities.Fiscal;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Results;
using ALMOXPRO.Shared.Security;
using Microsoft.Extensions.Logging;

namespace ALMOXPRO.Application.Services;

public interface IFiscalService
{
    Task<PagedResult<FiscalDocumentDto>> SearchAsync(PagedQuery query, FiscalDocumentStatus? status = null,
        CancellationToken ct = default);

    /// <summary>Sincroniza com a SEFAZ: baixa as notas emitidas contra o CNPJ desde o último NSU.</summary>
    Task<Result<FiscalSyncSummary>> SyncAsync(CancellationToken ct = default);

    /// <summary>Envia a manifestação do destinatário e atualiza a situação da nota.</summary>
    Task<Result> ManifestAsync(int documentId, ManifestationType type, string? justification,
        CancellationToken ct = default);

    /// <summary>Gera o DANFE (PDF). Exige o XML completo (após Ciência + nova sincronização).</summary>
    Task<Result<byte[]>> GetDanfePdfAsync(int documentId, CancellationToken ct = default);

    /// <summary>Valida e grava o certificado (arquivo A1 ou instalado no Windows) e os dados fiscais.</summary>
    Task<Result<CertificateInfo>> SaveConfigurationAsync(FiscalConfigInput input, CancellationToken ct = default);

    Task<CertificateInfo?> GetCertificateInfoAsync(CancellationToken ct = default);

    /// <summary>Certificados instalados no Windows, para escolha na tela de configuração.</summary>
    IReadOnlyList<InstalledCertificate> ListInstalledCertificates();
}

public record FiscalSyncSummary(int NewDocuments, int UpdatedDocuments, string UltNsu);

public class FiscalService : IFiscalService
{
    private readonly IUnitOfWork _uow;
    private readonly IFiscalGateway _gateway;
    private readonly IDanfeGenerator _danfe;
    private readonly ICurrentSession _session;
    private readonly ILogger<FiscalService> _logger;

    public FiscalService(IUnitOfWork uow, IFiscalGateway gateway, IDanfeGenerator danfe,
        ICurrentSession session, ILogger<FiscalService> logger)
    {
        _uow = uow;
        _gateway = gateway;
        _danfe = danfe;
        _session = session;
        _logger = logger;
    }

    public async Task<PagedResult<FiscalDocumentDto>> SearchAsync(PagedQuery query,
        FiscalDocumentStatus? status = null, CancellationToken ct = default)
    {
        var page = await _uow.FiscalDocuments.SearchAsync(query, status, ct);
        return new PagedResult<FiscalDocumentDto>
        {
            Items = page.Items.Select(d => new FiscalDocumentDto(
                d.Id, d.AccessKey, d.EmitterCnpj, d.EmitterName, d.IssuedAt,
                d.TotalValue, d.Status, d.HasFullXml, d.ManifestedAt, d.ManifestJustification)).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<Result<FiscalSyncSummary>> SyncAsync(CancellationToken ct = default)
    {
        if (await IsDemoModeAsync(ct))
            return await SyncDemoAsync(ct);

        var configResult = await LoadConfigAsync(ct);
        if (configResult.IsFailure)
            return Result.Failure<FiscalSyncSummary>(configResult.Errors);
        var config = configResult.Value;

        var ultNsu = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalUltNsu, ct))?.Value ?? "0";
        var newCount = 0;
        var updatedCount = 0;

        // A SEFAZ pagina por NSU: repete até alcançar o maxNSU (limite de
        // segurança de 20 iterações por sincronização).
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var result = await _gateway.FetchDocumentsAsync(config, ultNsu, ct);

            // 137 = nenhum documento localizado; 138 = documentos localizados.
            if (result.StatusCode == 137)
                break;
            if (result.StatusCode != 138)
                return Result.Failure<FiscalSyncSummary>(
                    $"SEFAZ retornou {result.StatusCode}: {result.StatusMessage}");

            foreach (var doc in result.Documents)
            {
                if (doc.Schema.StartsWith("resNFe", StringComparison.OrdinalIgnoreCase))
                {
                    var summary = FiscalXmlParser.ParseResNFe(doc.Xml);
                    if (await UpsertAsync(summary, doc, isFull: false, ct))
                        newCount++;
                }
                else if (doc.Schema.StartsWith("procNFe", StringComparison.OrdinalIgnoreCase))
                {
                    var summary = FiscalXmlParser.ParseProcNFe(doc.Xml);
                    if (await UpsertAsync(summary, doc, isFull: true, ct))
                        newCount++;
                    else
                        updatedCount++;
                }
                // resEvento/procEventoNFe: eventos de terceiros — ignorados por ora.
            }

            ultNsu = result.UltNsu;
            await SaveSettingAsync(SettingKeys.FiscalUltNsu, ultNsu, ct);
            await _uow.SaveChangesAsync(ct);

            if (string.CompareOrdinal(result.UltNsu, result.MaxNsu) >= 0)
                break;
        }

        _logger.LogInformation("Sincronização DF-e concluída: {New} novas, {Updated} atualizadas, ultNSU {Nsu}",
            newCount, updatedCount, ultNsu);
        return Result.Success(new FiscalSyncSummary(newCount, updatedCount, ultNsu));
    }

    /// <summary>Retorna true quando criou um documento novo; false quando atualizou um existente.</summary>
    private async Task<bool> UpsertAsync(ParsedNFeSummary summary, FiscalSyncDocument doc,
        bool isFull, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(summary.AccessKey))
            return false;

        var existing = await _uow.FiscalDocuments.GetByAccessKeyAsync(summary.AccessKey, ct);
        if (existing is null)
        {
            await _uow.FiscalDocuments.AddAsync(new FiscalDocument
            {
                AccessKey = summary.AccessKey,
                Nsu = doc.Nsu,
                EmitterCnpj = summary.EmitterCnpj,
                EmitterName = summary.EmitterName,
                IssuedAt = summary.IssuedAt,
                TotalValue = summary.TotalValue,
                HasFullXml = isFull,
                Xml = doc.Xml
            }, ct);
            return true;
        }

        // Nota já conhecida: o procNFe substitui o resumo (chega após a Ciência).
        if (isFull && !existing.HasFullXml)
        {
            existing.HasFullXml = true;
            existing.Xml = doc.Xml;
            existing.Nsu = doc.Nsu;
        }
        return false;
    }

    public async Task<Result> ManifestAsync(int documentId, ManifestationType type, string? justification,
        CancellationToken ct = default)
    {
        var document = await _uow.FiscalDocuments.GetByIdAsync(documentId, ct);
        if (document is null)
            return Result.Failure("Nota fiscal não encontrada.");

        // Modo demonstração: registra a manifestação localmente, sem a SEFAZ.
        if (!await IsDemoModeAsync(ct))
        {
            var configResult = await LoadConfigAsync(ct);
            if (configResult.IsFailure)
                return Result.Failure(configResult.Errors);

            var result = await _gateway.SendManifestationAsync(configResult.Value,
                document.AccessKey, type, justification, ct);
            if (!result.Success)
                return Result.Failure($"SEFAZ recusou o evento ({result.StatusCode}): {result.Message}");
        }

        var newStatus = type switch
        {
            ManifestationType.Ciencia => FiscalDocumentStatus.Ciencia,
            ManifestationType.Confirmacao => FiscalDocumentStatus.Confirmada,
            ManifestationType.Desconhecimento => FiscalDocumentStatus.Desconhecida,
            _ => FiscalDocumentStatus.OperacaoNaoRealizada
        };
        document.RegisterManifestation(newStatus, _session.UserId ?? 0, justification);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<byte[]>> GetDanfePdfAsync(int documentId, CancellationToken ct = default)
    {
        var document = await _uow.FiscalDocuments.GetByIdAsync(documentId, ct);
        if (document is null)
            return Result.Failure<byte[]>("Nota fiscal não encontrada.");
        if (!document.HasFullXml)
            return Result.Failure<byte[]>(
                "O XML completo ainda não foi baixado. Registre a Ciência da Operação e sincronize novamente " +
                "(a SEFAZ libera o XML completo após a ciência).");

        return Result.Success(_danfe.GeneratePdf(document.Xml));
    }

    public IReadOnlyList<InstalledCertificate> ListInstalledCertificates() =>
        _gateway.ListInstalledCertificates();

    public async Task<Result<CertificateInfo>> SaveConfigurationAsync(FiscalConfigInput input,
        CancellationToken ct = default)
    {
        var digits = new string(input.Cnpj.Where(char.IsDigit).ToArray());
        if (digits.Length != 14)
            return Result.Failure<CertificateInfo>("Informe o CNPJ da empresa (14 dígitos).");
        if (string.IsNullOrWhiteSpace(input.Uf))
            return Result.Failure<CertificateInfo>("Selecione a UF da empresa.");

        CertificateInfo info;
        if (input.UseWindowsStore)
        {
            if (string.IsNullOrWhiteSpace(input.Thumbprint))
                return Result.Failure<CertificateInfo>("Selecione um certificado instalado no Windows.");
            try
            {
                info = _gateway.InspectStoreCertificate(input.Thumbprint);
            }
            catch (Exception ex)
            {
                return Result.Failure<CertificateInfo>($"Não foi possível usar o certificado selecionado: {ex.Message}");
            }

            await SaveSettingAsync(SettingKeys.FiscalCertificateSource, "store", ct);
            await SaveSettingAsync(SettingKeys.FiscalCertificateThumbprint, input.Thumbprint, ct);
        }
        else if (input.PfxBytes is not null)
        {
            // Novo arquivo enviado: valida antes de gravar.
            try
            {
                info = _gateway.InspectCertificate(input.PfxBytes, input.PfxPassword ?? string.Empty);
            }
            catch (Exception ex)
            {
                return Result.Failure<CertificateInfo>($"Certificado inválido ou senha incorreta: {ex.Message}");
            }

            await SaveSettingAsync(SettingKeys.FiscalCertificateSource, "pfx", ct);
            await SaveSettingAsync(SettingKeys.FiscalCertificatePfx, Convert.ToBase64String(input.PfxBytes), ct);
            await SaveSettingAsync(SettingKeys.FiscalCertificatePassword,
                SecretProtector.Protect(input.PfxPassword ?? string.Empty), ct);
        }
        else
        {
            // Manteve o arquivo já configurado: apenas revalida.
            await SaveSettingAsync(SettingKeys.FiscalCertificateSource, "pfx", ct);
            var current = await GetCertificateInfoAsync(ct);
            if (current is null)
                return Result.Failure<CertificateInfo>("Envie o arquivo do certificado A1 (.pfx).");
            info = current;
        }

        await SaveSettingAsync(SettingKeys.FiscalCnpj, digits, ct);
        await SaveSettingAsync(SettingKeys.FiscalUf, input.Uf.Trim().ToUpperInvariant(), ct);
        await SaveSettingAsync(SettingKeys.FiscalProduction, input.Production ? "true" : "false", ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(info);
    }

    public async Task<CertificateInfo?> GetCertificateInfoAsync(CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct);
        if (config.IsFailure)
            return null;
        try
        {
            return !string.IsNullOrWhiteSpace(config.Value.CertificateThumbprint)
                ? _gateway.InspectStoreCertificate(config.Value.CertificateThumbprint!)
                : _gateway.InspectCertificate(config.Value.CertificatePfx!, config.Value.CertificatePassword ?? string.Empty);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<bool> IsDemoModeAsync(CancellationToken ct) =>
        (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalDemoMode, ct))?.Value == "true";

    /// <summary>
    /// Simula a Distribuição DF-e com notas fictícias: cria os documentos de
    /// exemplo e, para os que já tiveram a Ciência registrada, "libera" o XML
    /// completo — reproduzindo o ciclo real resumo → ciência → nota completa.
    /// </summary>
    private async Task<Result<FiscalSyncSummary>> SyncDemoAsync(CancellationToken ct)
    {
        var newCount = 0;
        var updatedCount = 0;

        foreach (var demo in FiscalDemoData.Documents)
        {
            var existing = await _uow.FiscalDocuments.GetByAccessKeyAsync(demo.AccessKey, ct);
            if (existing is null)
            {
                var xml = demo.StartsWithFullXml ? demo.FullXml : demo.SummaryXml;
                var parsed = demo.StartsWithFullXml
                    ? FiscalXmlParser.ParseProcNFe(xml)
                    : FiscalXmlParser.ParseResNFe(xml);

                await _uow.FiscalDocuments.AddAsync(new FiscalDocument
                {
                    AccessKey = parsed.AccessKey,
                    Nsu = demo.Nsu,
                    EmitterCnpj = parsed.EmitterCnpj,
                    EmitterName = parsed.EmitterName,
                    IssuedAt = parsed.IssuedAt,
                    TotalValue = parsed.TotalValue,
                    HasFullXml = demo.StartsWithFullXml,
                    Xml = xml
                }, ct);
                newCount++;
            }
            else if (!existing.HasFullXml && existing.Status != FiscalDocumentStatus.Recebida)
            {
                // Após a Ciência, a SEFAZ libera o XML completo.
                existing.HasFullXml = true;
                existing.Xml = demo.FullXml;
                updatedCount++;
            }
        }

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Sincronização DEMO: {New} novas, {Updated} atualizadas", newCount, updatedCount);
        return Result.Success(new FiscalSyncSummary(newCount, updatedCount, "demo"));
    }

    private async Task<Result<FiscalConfig>> LoadConfigAsync(CancellationToken ct)
    {
        var cnpj = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalCnpj, ct))?.Value;
        var uf = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalUf, ct))?.Value;
        var production = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalProduction, ct))?.Value != "false";
        var source = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalCertificateSource, ct))?.Value ?? "pfx";

        if (string.IsNullOrWhiteSpace(cnpj) || string.IsNullOrWhiteSpace(uf))
            return Result.Failure<FiscalConfig>(
                "Configuração fiscal incompleta. Em Configurações, informe o certificado, o CNPJ e a UF.");

        if (source == "store")
        {
            var thumbprint = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalCertificateThumbprint, ct))?.Value;
            if (string.IsNullOrWhiteSpace(thumbprint))
                return Result.Failure<FiscalConfig>(
                    "Selecione o certificado instalado no Windows em Configurações.");
            return Result.Success(new FiscalConfig(null, null, thumbprint, cnpj, uf, production));
        }

        var pfx = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalCertificatePfx, ct))?.Value;
        var password = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalCertificatePassword, ct))?.Value;
        if (string.IsNullOrWhiteSpace(pfx))
            return Result.Failure<FiscalConfig>(
                "Configuração fiscal incompleta. Em Configurações, envie o certificado A1.");

        return Result.Success(new FiscalConfig(
            Convert.FromBase64String(pfx),
            string.IsNullOrEmpty(password) ? string.Empty : SecretProtector.Unprotect(password),
            null, cnpj, uf, production));
    }

    private async Task SaveSettingAsync(string key, string value, CancellationToken ct)
    {
        var setting = await _uow.Settings.GetByKeyAsync(key, ct);
        if (setting is null)
            await _uow.Settings.AddAsync(new AppSetting { Key = key, Value = value }, ct);
        else
            setting.Value = value;
        await _uow.SaveChangesAsync(ct);
    }
}
