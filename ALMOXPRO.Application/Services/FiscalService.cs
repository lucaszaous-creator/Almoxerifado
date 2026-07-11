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

    /// <summary>Valida e grava o certificado A1 e os dados fiscais da empresa.</summary>
    Task<Result<CertificateInfo>> SaveConfigurationAsync(byte[]? pfxBytes, string? password,
        string cnpj, string uf, bool production, CancellationToken ct = default);

    Task<CertificateInfo?> GetCertificateInfoAsync(CancellationToken ct = default);
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

        var configResult = await LoadConfigAsync(ct);
        if (configResult.IsFailure)
            return Result.Failure(configResult.Errors);

        var result = await _gateway.SendManifestationAsync(configResult.Value,
            document.AccessKey, type, justification, ct);
        if (!result.Success)
            return Result.Failure($"SEFAZ recusou o evento ({result.StatusCode}): {result.Message}");

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

    public async Task<Result<CertificateInfo>> SaveConfigurationAsync(byte[]? pfxBytes, string? password,
        string cnpj, string uf, bool production, CancellationToken ct = default)
    {
        var digits = new string(cnpj.Where(char.IsDigit).ToArray());
        if (digits.Length != 14)
            return Result.Failure<CertificateInfo>("Informe o CNPJ da empresa (14 dígitos).");
        if (string.IsNullOrWhiteSpace(uf))
            return Result.Failure<CertificateInfo>("Selecione a UF da empresa.");

        CertificateInfo info;
        if (pfxBytes is not null)
        {
            // Novo certificado enviado: valida antes de gravar.
            try
            {
                info = _gateway.InspectCertificate(pfxBytes, password ?? string.Empty);
            }
            catch (Exception ex)
            {
                return Result.Failure<CertificateInfo>($"Certificado inválido ou senha incorreta: {ex.Message}");
            }

            await SaveSettingAsync(SettingKeys.FiscalCertificatePfx, Convert.ToBase64String(pfxBytes), ct);
            await SaveSettingAsync(SettingKeys.FiscalCertificatePassword,
                SecretProtector.Protect(password ?? string.Empty), ct);
        }
        else
        {
            var current = await GetCertificateInfoAsync(ct);
            if (current is null)
                return Result.Failure<CertificateInfo>("Envie o arquivo do certificado A1 (.pfx).");
            info = current;
        }

        await SaveSettingAsync(SettingKeys.FiscalCnpj, digits, ct);
        await SaveSettingAsync(SettingKeys.FiscalUf, uf.Trim().ToUpperInvariant(), ct);
        await SaveSettingAsync(SettingKeys.FiscalProduction, production ? "true" : "false", ct);
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
            return _gateway.InspectCertificate(config.Value.CertificatePfx, config.Value.CertificatePassword);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<Result<FiscalConfig>> LoadConfigAsync(CancellationToken ct)
    {
        var pfx = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalCertificatePfx, ct))?.Value;
        var password = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalCertificatePassword, ct))?.Value;
        var cnpj = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalCnpj, ct))?.Value;
        var uf = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalUf, ct))?.Value;
        var production = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalProduction, ct))?.Value != "false";

        if (string.IsNullOrWhiteSpace(pfx) || string.IsNullOrWhiteSpace(cnpj) || string.IsNullOrWhiteSpace(uf))
            return Result.Failure<FiscalConfig>(
                "Configuração fiscal incompleta. Em Configurações, envie o certificado A1 e informe CNPJ e UF.");

        return Result.Success(new FiscalConfig(
            Convert.FromBase64String(pfx),
            string.IsNullOrEmpty(password) ? string.Empty : SecretProtector.Unprotect(password),
            cnpj, uf, production));
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
