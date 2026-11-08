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
        DateTime? from = null, DateTime? to = null, int? number = null, CancellationToken ct = default);

    /// <summary>ZIP com os XMLs das notas recebidas que atendem aos filtros (máx. 1000).</summary>
    Task<Result<byte[]>> ExportXmlZipAsync(FiscalDocumentStatus? status = null, DateTime? from = null,
        DateTime? to = null, int? number = null, string? search = null, CancellationToken ct = default);

    /// <summary>Resumo para o painel da tela Notas Fiscais (cards + gráfico dos últimos 5 dias).</summary>
    Task<FiscalSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    /// <summary>Sincroniza com a SEFAZ: baixa as notas emitidas contra o CNPJ desde o último NSU.</summary>
    Task<Result<FiscalSyncSummary>> SyncAsync(CancellationToken ct = default);

    /// <summary>
    /// Relê a Distribuição DF-e desde o início: zera o último NSU e o bloqueio de
    /// intervalo e sincroniza. Use quando as notas não aparecem por o NSU já ter
    /// avançado além delas (ex.: após trocar de ambiente/certificado).
    /// </summary>
    Task<Result<FiscalSyncSummary>> ResyncFromStartAsync(CancellationToken ct = default);

    /// <summary>
    /// Remove as notas fictícias do modo demonstração (recebidas e emitidas) e
    /// libera o bloqueio de sincronização — usado ao desligar o modo demonstração
    /// para que o ambiente real assuma limpo. Retorna quantas notas foram removidas.
    /// </summary>
    Task<Result<int>> PurgeDemoDataAsync(CancellationToken ct = default);

    /// <summary>Envia a manifestação do destinatário e atualiza a situação da nota.</summary>
    Task<Result> ManifestAsync(int documentId, ManifestationType type, string? justification,
        CancellationToken ct = default);

    /// <summary>Gera o DANFE (PDF). Exige o XML completo (após Ciência + nova sincronização).</summary>
    Task<Result<byte[]>> GetDanfePdfAsync(int documentId, CancellationToken ct = default);

    /// <summary>XML armazenado da nota recebida (procNFe completo ou resumo).</summary>
    Task<Result<string>> GetXmlAsync(int documentId, CancellationToken ct = default);

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
        FiscalDocumentStatus? status = null, DateTime? from = null, DateTime? to = null,
        int? number = null, CancellationToken ct = default)
    {
        var page = await _uow.FiscalDocuments.SearchAsync(query, status, from, to, number, ct);
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

    public async Task<Result<byte[]>> ExportXmlZipAsync(FiscalDocumentStatus? status = null,
        DateTime? from = null, DateTime? to = null, int? number = null, string? search = null,
        CancellationToken ct = default)
    {
        var page = await _uow.FiscalDocuments.SearchAsync(
            new PagedQuery { Page = 1, PageSize = 1000, Search = search ?? string.Empty },
            status, from, to, number, ct);
        var withXml = page.Items.Where(d => !string.IsNullOrWhiteSpace(d.Xml)).ToList();
        if (withXml.Count == 0)
            return Result.Failure<byte[]>("Nenhuma nota com XML encontrada para os filtros informados.");

        using var buffer = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(buffer,
                   System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var doc in withXml)
            {
                var entry = zip.CreateEntry($"NFe-{doc.AccessKey}.xml");
                await using var stream = entry.Open();
                await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(doc.Xml), ct);
            }
        }
        return Result.Success(buffer.ToArray());
    }

    public async Task<FiscalSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var chartStart = DateTime.UtcNow.Date.AddDays(-4);
        var cutoff = monthStart < chartStart ? monthStart : chartStart;

        var receivedDates = await _uow.FiscalDocuments.IssueDatesSinceAsync(cutoff, ct);
        var issuedDates = await _uow.IssuedNfes.IssueDatesSinceAsync(cutoff, ct);
        var totalStored = await _uow.FiscalDocuments.CountAllAsync(ct) + await _uow.IssuedNfes.CountAllAsync(ct);

        var issuedMonthValuePage = await _uow.IssuedNfes.SearchAsync(
            new PagedQuery { Page = 1, PageSize = 1000 }, from: monthStart, ct: ct);

        var days = Enumerable.Range(0, 5)
            .Select(offset => DateTime.UtcNow.Date.AddDays(offset - 4))
            .Select(day => new FiscalDayCount(day,
                receivedDates.Count(d => d.Date == day),
                issuedDates.Count(d => d.Date == day)))
            .ToList();

        return new FiscalSummaryDto(
            totalStored,
            receivedDates.Count(d => d >= monthStart),
            issuedDates.Count(d => d >= monthStart),
            issuedMonthValuePage.Items.Where(n => n.Status == IssuedNfeStatus.Autorizada).Sum(n => n.TotalValue),
            days);
    }

    public async Task<Result<FiscalSyncSummary>> SyncAsync(CancellationToken ct = default)
    {
        if (await IsDemoModeAsync(ct))
            return await SyncDemoAsync(ct);

        var configResult = await LoadConfigAsync(ct);
        if (configResult.IsFailure)
            return Result.Failure<FiscalSyncSummary>(configResult.Errors);
        var config = configResult.Value;

        // A SEFAZ pune consultas repetidas em menos de 1 hora quando não há
        // documentos novos (cStat 656 "Consumo Indevido", bloqueio de 1 hora).
        // O guard local impede a chamada antes do horário liberado.
        var blockedRaw = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalSyncBlockedUntil, ct))?.Value;
        if (DateTime.TryParse(blockedRaw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal, out var blockedUntil)
            && DateTime.UtcNow < blockedUntil)
            return Result.Failure<FiscalSyncSummary>(
                $"A consulta à SEFAZ está em espera para evitar novo bloqueio por excesso de chamadas " +
                $"(cStat 656). Nova tentativa liberada a partir de {blockedUntil.ToLocalTime():dd/MM HH:mm}. " +
                $"Não insista antes disso: cada tentativa cedo demais reinicia a penalidade da SEFAZ.");

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
            {
                // Resposta normal "sem novidades": intervalo padrão de 1h e zera a
                // escalada do 656 (não houve consumo indevido nesta consulta).
                await ResetBackoffAsync(ct);
                await BlockSyncForAsync(TimeSpan.FromMinutes(65), ct);
                break;
            }
            if (result.StatusCode == 656)
            {
                // "Consumo Indevido": penalidade por CNPJ que pode durar horas e é
                // renovada a cada tentativa precoce. Recua exponencialmente para sair
                // de vez do bloqueio em vez de reincidir a cada hora.
                var wait = await EscalateBackoffAsync(ct);
                var until = DateTime.UtcNow.Add(wait).ToLocalTime();
                return Result.Failure<FiscalSyncSummary>(
                    $"A SEFAZ bloqueou a consulta por excesso de chamadas (cStat 656). Este bloqueio é " +
                    $"por CNPJ e pode durar horas — inclusive se outro sistema (ex.: NFeMail) estiver " +
                    $"baixando as notas do mesmo CNPJ ao mesmo tempo. O ALMOX PRO só tentará de novo a " +
                    $"partir de {until:dd/MM HH:mm}. Mantenha apenas um sistema baixando as notas e não " +
                    $"clique em sincronizar nesse meio-tempo.");
            }
            if (result.StatusCode != 138)
                return Result.Failure<FiscalSyncSummary>(
                    $"SEFAZ retornou {result.StatusCode}: {result.StatusMessage}");

            // Consulta bem-sucedida: zera qualquer penalidade acumulada de 656.
            await ResetBackoffAsync(ct);

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

    public async Task<Result<FiscalSyncSummary>> ResyncFromStartAsync(CancellationToken ct = default)
    {
        if (await IsDemoModeAsync(ct))
            return await SyncDemoAsync(ct);

        // Zera o ponteiro de leitura e o guard de intervalo; a próxima consulta
        // relê tudo desde o NSU 0 (a SEFAZ entrega os últimos ~3 meses).
        await SaveSettingAsync(SettingKeys.FiscalUltNsu, "0", ct);
        await SaveSettingAsync(SettingKeys.FiscalSyncBlockedUntil, string.Empty, ct);
        _logger.LogInformation("Ressincronização completa: NSU e bloqueio de intervalo zerados.");

        return await SyncAsync(ct);
    }

    /// <summary>Zera a escalada de recuo após uma consulta bem-sucedida ou sem consumo indevido.</summary>
    private async Task ResetBackoffAsync(CancellationToken ct)
    {
        var setting = await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalSyncBackoffLevel, ct);
        if (setting is not null && setting.Value != "0")
        {
            setting.Value = "0";
            await _uow.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Aplica recuo exponencial após um cStat 656 e bloqueia a consulta pela
    /// duração calculada (1h, 2h, 4h, 8h… com teto de 12h). Retorna a duração.
    /// </summary>
    private async Task<TimeSpan> EscalateBackoffAsync(CancellationToken ct)
    {
        var raw = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalSyncBackoffLevel, ct))?.Value;
        var level = int.TryParse(raw, out var parsed) && parsed >= 0 ? parsed : 0;

        var minutes = Math.Min(60 * Math.Pow(2, level), 720); // teto de 12 horas
        var duration = TimeSpan.FromMinutes(minutes);

        await SaveSettingAsync(SettingKeys.FiscalSyncBackoffLevel, (level + 1).ToString(), ct);
        await BlockSyncForAsync(duration, ct);
        _logger.LogWarning("cStat 656: recuo nível {Level}, próxima consulta em {Minutes} min.",
            level, minutes);
        return duration;
    }

    private async Task BlockSyncForAsync(TimeSpan duration, CancellationToken ct)
    {
        await SaveSettingAsync(SettingKeys.FiscalSyncBlockedUntil,
            DateTime.UtcNow.Add(duration).ToString("O", System.Globalization.CultureInfo.InvariantCulture), ct);
        await _uow.SaveChangesAsync(ct);
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

        // A Distribuição DF-e também entrega NFC-e (modelo 65) quando a compra
        // é feita com o CNPJ da empresa; a DANFE só existe para NF-e (55).
        if (FiscalXmlParser.GetModel(document.Xml) == "65")
            return Result.Failure<byte[]>(
                "Esta nota é uma NFC-e (cupom fiscal eletrônico, modelo 65) — a DANFE se aplica apenas " +
                "à NF-e (modelo 55). Use SALVAR XML para arquivar ou enviar ao contador.");

        try
        {
            return Result.Success(_danfe.GeneratePdf(document.Xml));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gerar DANFE da nota {Chave}", document.AccessKey);
            return Result.Failure<byte[]>($"Não foi possível gerar a DANFE desta nota: {ex.Message}");
        }
    }

    public async Task<Result<string>> GetXmlAsync(int documentId, CancellationToken ct = default)
    {
        var document = await _uow.FiscalDocuments.GetByIdAsync(documentId, ct);
        if (document is null)
            return Result.Failure<string>("Nota fiscal não encontrada.");
        if (string.IsNullOrWhiteSpace(document.Xml))
            return Result.Failure<string>("Esta nota ainda não tem XML armazenado.");
        return Result.Success(document.Xml);
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

    private Task<bool> IsDemoModeAsync(CancellationToken ct) =>
        FiscalConfigLoader.IsDemoModeAsync(_uow, ct);

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

    public async Task<Result<int>> PurgeDemoDataAsync(CancellationToken ct = default)
    {
        // Notas recebidas de exemplo: identificadas pelas chaves fixas do demo.
        var demoKeys = FiscalDemoData.Documents.Select(d => d.AccessKey).ToList();
        var demoReceived = await _uow.FiscalDocuments.FindAsync(d => demoKeys.Contains(d.AccessKey), ct);
        foreach (var doc in demoReceived)
            _uow.FiscalDocuments.Remove(doc);

        // Notas emitidas de exemplo: o protocolo fictício as distingue das reais
        // (inclusive das de homologação, que têm protocolo real da SEFAZ).
        var demoIssued = await _uow.IssuedNfes.FindAsync(
            n => n.Protocol == IssuedNfeDemoXml.DemoProtocol, ct);
        foreach (var nfe in demoIssued)
            _uow.IssuedNfes.Remove(nfe);

        // Libera o guard de 1 hora (cStat 656) para permitir sincronizar já.
        var block = await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalSyncBlockedUntil, ct);
        if (block is not null)
            block.Value = string.Empty;

        await _uow.SaveChangesAsync(ct);

        var removed = demoReceived.Count + demoIssued.Count;
        if (removed > 0)
            _logger.LogInformation("Modo demonstração desligado: {Count} nota(s) fictícia(s) removida(s)", removed);
        return Result.Success(removed);
    }

    private Task<Result<FiscalConfig>> LoadConfigAsync(CancellationToken ct) =>
        FiscalConfigLoader.LoadAsync(_uow, ct);

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
