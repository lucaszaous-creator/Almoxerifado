using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Domain.Entities.Fiscal;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Results;
using Microsoft.Extensions.Logging;

namespace ALMOXPRO.Application.Services;

public record IssueNfeItemInput(
    string Code,
    string Description,
    string Ncm,
    string Cfop,
    string Unit,
    decimal Quantity,
    decimal UnitValue,
    /// <summary>CST do ICMS na venda tributada: "00", "20", "40", "41" ou "60". Ignorado nas operações sem destaque.</summary>
    string? IcmsCst = null,
    /// <summary>Alíquota de ICMS (%) — obrigatória nos CST 00 e 20.</summary>
    decimal? IcmsRate = null,
    /// <summary>% de redução da base de cálculo — obrigatória no CST 20.</summary>
    decimal? IcmsBaseReductionPct = null);

public record IssueNfeInput(
    string NatureOfOperation,
    /// <summary>True = venda com impostos (restaurante/balcão); false = remessa/devolução sem destaque.</summary>
    bool IsTaxedSale,
    /// <summary>tPag: 01 dinheiro, 03 crédito, 04 débito, 15 boleto, 17 PIX, 99 outros (só venda).</summary>
    int PaymentMethod,
    /// <summary>True para devolução (finNFe 4, exige a chave da NF-e de origem).</summary>
    bool IsDevolution,
    string? ReferencedAccessKey,
    string RecipientCnpjCpf,
    string RecipientName,
    /// <summary>indIEDest: 1 = contribuinte ICMS, 2 = isento, 9 = não contribuinte.</summary>
    int RecipientIeIndicator,
    string? RecipientIe,
    string RecipientStreet,
    string RecipientNumber,
    string RecipientDistrict,
    string RecipientCityCode,
    string RecipientCityName,
    string RecipientUf,
    string RecipientCep,
    string? AdditionalInfo,
    IReadOnlyList<IssueNfeItemInput> Items);

public interface IFiscalEmissionService
{
    Task<PagedResult<IssuedNfeDto>> SearchAsync(PagedQuery query, CancellationToken ct = default);

    /// <summary>Monta, numera, assina e envia a NF-e; grava o procNFe autorizado.</summary>
    Task<Result<IssuedNfeDto>> IssueAsync(IssueNfeInput input, CancellationToken ct = default);

    /// <summary>Cancela uma NF-e autorizada (evento 110111, exige justificativa).</summary>
    Task<Result> CancelAsync(int id, string justification, CancellationToken ct = default);

    /// <summary>DANFE (PDF) da nota emitida.</summary>
    Task<Result<byte[]>> GetDanfePdfAsync(int id, CancellationToken ct = default);

    /// <summary>XML procNFe autorizado da nota emitida (para envio ao cliente/contador).</summary>
    Task<Result<string>> GetXmlAsync(int id, CancellationToken ct = default);

    /// <summary>Consulta o status do serviço da SEFAZ com o certificado configurado.</summary>
    Task<Result<SefazServiceStatus>> CheckServiceStatusAsync(CancellationToken ct = default);
}

public class FiscalEmissionService : IFiscalEmissionService
{
    private readonly IUnitOfWork _uow;
    private readonly IFiscalGateway _gateway;
    private readonly IDanfeGenerator _danfe;
    private readonly ICurrentSession _session;
    private readonly ILogger<FiscalEmissionService> _logger;

    public FiscalEmissionService(IUnitOfWork uow, IFiscalGateway gateway, IDanfeGenerator danfe,
        ICurrentSession session, ILogger<FiscalEmissionService> logger)
    {
        _uow = uow;
        _gateway = gateway;
        _danfe = danfe;
        _session = session;
        _logger = logger;
    }

    public async Task<PagedResult<IssuedNfeDto>> SearchAsync(PagedQuery query, CancellationToken ct = default)
    {
        var page = await _uow.IssuedNfes.SearchAsync(query, ct);
        return new PagedResult<IssuedNfeDto>
        {
            Items = page.Items.Select(ToDto).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<Result<IssuedNfeDto>> IssueAsync(IssueNfeInput input, CancellationToken ct = default)
    {
        var validation = Validate(input);
        if (validation.IsFailure)
            return Result.Failure<IssuedNfeDto>(validation.Errors);

        var demo = await FiscalConfigLoader.IsDemoModeAsync(_uow, ct);

        FiscalConfig? config = null;
        if (!demo)
        {
            var configResult = await FiscalConfigLoader.LoadAsync(_uow, ct);
            if (configResult.IsFailure)
                return Result.Failure<IssuedNfeDto>(configResult.Errors);
            config = configResult.Value;
        }

        var emitterResult = await LoadEmitterAsync(demo, config, ct);
        if (emitterResult.IsFailure)
            return Result.Failure<IssuedNfeDto>(emitterResult.Errors);
        var emitter = emitterResult.Value;

        var series = await GetSeriesAsync(ct);
        var number = await _uow.IssuedNfes.GetLastNumberAsync(series, ct) + 1;
        var issuedAt = DateTimeOffset.Now;
        var cnf = NfeAccessKey.NewRandomCode(number);
        var accessKey = NfeAccessKey.Build(emitter.Uf, issuedAt, emitter.Cnpj, 55, series, number, 1, cnf);

        var recipientDigits = Digits(input.RecipientCnpjCpf);
        var pisRate = input.IsTaxedSale ? await GetRateAsync(SettingKeys.FiscalPisRate, 0.65m, ct) : 0m;
        var cofinsRate = input.IsTaxedSale ? await GetRateAsync(SettingKeys.FiscalCofinsRate, 3.00m, ct) : 0m;

        var items = input.Items.Select((item, index) =>
        {
            // Arredondamento fiscal: meio sempre para cima (away from zero), não o bancário do .NET.
            var total = Math.Round(item.Quantity * item.UnitValue, 2, MidpointRounding.AwayFromZero);
            var cst = input.IsTaxedSale ? item.IcmsCst!.Trim() : "41";
            var reduction = cst == "20" ? item.IcmsBaseReductionPct!.Value : 0m;

            // Base e valor do ICMS só nos CST com destaque (00 e 20).
            var rate = cst is "00" or "20" ? item.IcmsRate!.Value : 0m;
            var baseValue = cst switch
            {
                "00" => total,
                "20" => Math.Round(total * (1 - reduction / 100m), 2, MidpointRounding.AwayFromZero),
                _ => 0m
            };
            var icmsValue = Math.Round(baseValue * rate / 100m, 2, MidpointRounding.AwayFromZero);

            return new NfeDraftItem(
                index + 1,
                string.IsNullOrWhiteSpace(item.Code) ? (index + 1).ToString("D3") : item.Code.Trim(),
                item.Description.Trim(),
                Digits(item.Ncm),
                Digits(item.Cfop),
                item.Unit.Trim(),
                item.Quantity,
                item.UnitValue,
                total,
                cst,
                reduction,
                baseValue,
                rate,
                icmsValue,
                input.IsTaxedSale ? Math.Round(total * pisRate / 100m, 2, MidpointRounding.AwayFromZero) : 0m,
                input.IsTaxedSale ? Math.Round(total * cofinsRate / 100m, 2, MidpointRounding.AwayFromZero) : 0m);
        }).ToList();

        var draft = new NfeDraft(
            accessKey,
            cnf,
            accessKey[^1] - '0',
            number,
            series,
            issuedAt,
            input.NatureOfOperation.Trim(),
            input.IsDevolution ? 4 : 1,
            input.IsDevolution ? Digits(input.ReferencedAccessKey ?? string.Empty) : null,
            emitter,
            new NfeRecipient(
                recipientDigits,
                input.RecipientName.Trim(),
                input.RecipientIeIndicator,
                string.IsNullOrWhiteSpace(input.RecipientIe) ? null : input.RecipientIe.Trim(),
                input.RecipientStreet.Trim(),
                input.RecipientNumber.Trim(),
                input.RecipientDistrict.Trim(),
                int.Parse(Digits(input.RecipientCityCode)),
                input.RecipientCityName.Trim(),
                input.RecipientUf.Trim().ToUpperInvariant(),
                Digits(input.RecipientCep)),
            items,
            items.Sum(i => i.Total),
            string.IsNullOrWhiteSpace(input.AdditionalInfo) ? null : input.AdditionalInfo.Trim(),
            input.IsTaxedSale,
            input.IsTaxedSale ? input.PaymentMethod : 90,
            pisRate,
            cofinsRate);

        string protocol;
        string procXml;
        if (demo)
        {
            protocol = IssuedNfeDemoXml.DemoProtocol;
            procXml = IssuedNfeDemoXml.BuildProcNfe(draft);
        }
        else
        {
            NfeAuthorizationResult result;
            try
            {
                result = await _gateway.AuthorizeNfeAsync(config!, draft, ct);
            }
            catch (Exception ex)
            {
                // Inclui as falhas de validação de schema da biblioteca, antes do envio.
                return Result.Failure<IssuedNfeDto>($"Falha ao enviar a NF-e à SEFAZ: {ex.Message}");
            }

            if (!result.Success || result.ProcNFeXml is null)
                return Result.Failure<IssuedNfeDto>(
                    $"SEFAZ rejeitou a nota ({result.StatusCode}): {result.Message}");
            protocol = result.Protocol ?? string.Empty;
            procXml = result.ProcNFeXml;
        }

        var entity = new IssuedNfe
        {
            AccessKey = accessKey,
            Number = number,
            Series = series,
            NatureOfOperation = draft.NatureOfOperation,
            RecipientCnpjCpf = recipientDigits,
            RecipientName = draft.Recipient.Name,
            IssuedAt = issuedAt.UtcDateTime,
            TotalValue = draft.TotalValue,
            Status = IssuedNfeStatus.Autorizada,
            Protocol = protocol,
            Xml = procXml,
            IsProduction = !demo && config!.Production,
            IssuedByUserId = _session.UserId
        };
        await _uow.IssuedNfes.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("NF-e {Serie}/{Numero} emitida (chave {Chave}, protocolo {Protocolo}, demo: {Demo})",
            series, number, accessKey, protocol, demo);
        return Result.Success(ToDto(entity));
    }

    public async Task<Result> CancelAsync(int id, string justification, CancellationToken ct = default)
    {
        var nfe = await _uow.IssuedNfes.GetByIdAsync(id, ct);
        if (nfe is null)
            return Result.Failure("Nota fiscal emitida não encontrada.");
        if (nfe.Status == IssuedNfeStatus.Cancelada)
            return Result.Failure("A nota já está cancelada.");
        if (justification.Trim().Length < 15)
            return Result.Failure("O cancelamento exige justificativa com ao menos 15 caracteres.");

        var protocol = IssuedNfeDemoXml.DemoProtocol;
        if (!await FiscalConfigLoader.IsDemoModeAsync(_uow, ct))
        {
            var configResult = await FiscalConfigLoader.LoadAsync(_uow, ct);
            if (configResult.IsFailure)
                return Result.Failure(configResult.Errors);

            NfeCancelResult result;
            try
            {
                result = await _gateway.CancelNfeAsync(configResult.Value, nfe.AccessKey,
                    nfe.Protocol, justification.Trim(), ct);
            }
            catch (Exception ex)
            {
                return Result.Failure($"Falha ao enviar o cancelamento à SEFAZ: {ex.Message}");
            }

            if (!result.Success)
                return Result.Failure($"SEFAZ recusou o cancelamento ({result.StatusCode}): {result.Message}");
            protocol = result.Protocol ?? string.Empty;
        }

        nfe.RegisterCancellation(protocol, justification, _session.UserId ?? 0);
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("NF-e {Chave} cancelada (protocolo {Protocolo})", nfe.AccessKey, protocol);
        return Result.Success();
    }

    public async Task<Result<byte[]>> GetDanfePdfAsync(int id, CancellationToken ct = default)
    {
        var nfe = await _uow.IssuedNfes.GetByIdAsync(id, ct);
        if (nfe is null)
            return Result.Failure<byte[]>("Nota fiscal emitida não encontrada.");
        return Result.Success(_danfe.GeneratePdf(nfe.Xml));
    }

    public async Task<Result<string>> GetXmlAsync(int id, CancellationToken ct = default)
    {
        var nfe = await _uow.IssuedNfes.GetByIdAsync(id, ct);
        if (nfe is null)
            return Result.Failure<string>("Nota fiscal emitida não encontrada.");
        return Result.Success(nfe.Xml);
    }

    public async Task<Result<SefazServiceStatus>> CheckServiceStatusAsync(CancellationToken ct = default)
    {
        if (await FiscalConfigLoader.IsDemoModeAsync(_uow, ct))
            return Result.Success(new SefazServiceStatus(true, 107,
                "Serviço em operação (modo demonstração, sem comunicação real)."));

        var configResult = await FiscalConfigLoader.LoadAsync(_uow, ct);
        if (configResult.IsFailure)
            return Result.Failure<SefazServiceStatus>(configResult.Errors);

        try
        {
            return Result.Success(await _gateway.CheckServiceStatusAsync(configResult.Value, ct));
        }
        catch (Exception ex)
        {
            return Result.Failure<SefazServiceStatus>($"Falha ao consultar a SEFAZ: {ex.Message}");
        }
    }

    private static Result Validate(IssueNfeInput input)
    {
        var errors = new List<string>();

        var natOp = input.NatureOfOperation?.Trim() ?? string.Empty;
        if (natOp.Length is < 1 or > 60)
            errors.Add("Informe a natureza da operação (até 60 caracteres).");

        var doc = Digits(input.RecipientCnpjCpf);
        if (doc.Length is not (11 or 14))
            errors.Add("Informe o CNPJ (14 dígitos) ou CPF (11 dígitos) do destinatário.");
        if (string.IsNullOrWhiteSpace(input.RecipientName))
            errors.Add("Informe a razão social/nome do destinatário.");
        if (input.RecipientIeIndicator is not (1 or 2 or 9))
            errors.Add("Indicador de IE do destinatário inválido (1, 2 ou 9).");
        if (input.RecipientIeIndicator == 1 && string.IsNullOrWhiteSpace(input.RecipientIe))
            errors.Add("Destinatário contribuinte exige a Inscrição Estadual.");
        if (string.IsNullOrWhiteSpace(input.RecipientStreet) || string.IsNullOrWhiteSpace(input.RecipientNumber)
            || string.IsNullOrWhiteSpace(input.RecipientDistrict) || string.IsNullOrWhiteSpace(input.RecipientCityName))
            errors.Add("Preencha o endereço completo do destinatário (logradouro, número, bairro e município).");
        if (Digits(input.RecipientCityCode).Length != 7)
            errors.Add("Informe o código IBGE do município do destinatário (7 dígitos).");
        if (!NfeAccessKey.UfCodes.ContainsKey(input.RecipientUf?.Trim().ToUpperInvariant() ?? string.Empty))
            errors.Add("UF do destinatário inválida.");
        if (Digits(input.RecipientCep).Length != 8)
            errors.Add("Informe o CEP do destinatário (8 dígitos).");

        if (input.IsDevolution && Digits(input.ReferencedAccessKey ?? string.Empty).Length != 44)
            errors.Add("A devolução exige a chave de acesso (44 dígitos) da NF-e de origem.");
        if (input.IsDevolution && input.IsTaxedSale)
            errors.Add("Devolução com impostos destacados ainda não é suportada — emita a devolução como operação sem destaque ou fale com o contador.");
        if (input.IsTaxedSale && input.PaymentMethod is not (1 or 3 or 4 or 15 or 17 or 99))
            errors.Add("Meio de pagamento inválido para a venda.");

        if (input.Items.Count == 0)
            errors.Add("Inclua ao menos um item na nota.");
        foreach (var (item, index) in input.Items.Select((i, idx) => (i, idx + 1)))
        {
            if (string.IsNullOrWhiteSpace(item.Description))
                errors.Add($"Item {index}: informe a descrição.");
            if (Digits(item.Ncm).Length != 8)
                errors.Add($"Item {index}: NCM deve ter 8 dígitos.");
            if (Digits(item.Cfop).Length != 4)
                errors.Add($"Item {index}: CFOP deve ter 4 dígitos.");
            if (string.IsNullOrWhiteSpace(item.Unit))
                errors.Add($"Item {index}: informe a unidade.");
            if (item.Quantity <= 0)
                errors.Add($"Item {index}: quantidade deve ser maior que zero.");
            if (item.UnitValue < 0)
                errors.Add($"Item {index}: valor unitário inválido.");

            if (!input.IsTaxedSale)
                continue;

            var cst = item.IcmsCst?.Trim();
            if (cst is not ("00" or "20" or "40" or "41" or "60"))
                errors.Add($"Item {index}: CST do ICMS deve ser 00, 20, 40, 41 ou 60.");
            if (cst is "00" or "20" && item.IcmsRate is not > 0)
                errors.Add($"Item {index}: informe a alíquota de ICMS para o CST {cst}.");
            if (cst == "20" && item.IcmsBaseReductionPct is not (> 0 and < 100))
                errors.Add($"Item {index}: informe a redução da base (entre 0 e 100%) para o CST 20.");
        }

        return errors.Count > 0 ? Result.Failure(errors.ToArray()) : Result.Success();
    }

    /// <summary>
    /// Dados do emitente vindos das Configurações. No modo demonstração os
    /// campos ausentes são preenchidos com valores fictícios para permitir o
    /// teste sem cadastro prévio.
    /// </summary>
    private async Task<Result<NfeEmitter>> LoadEmitterAsync(bool demo, FiscalConfig? config, CancellationToken ct)
    {
        async Task<string?> Get(string key) => (await _uow.Settings.GetByKeyAsync(key, ct))?.Value;

        var cnpj = demo ? Digits(await Get(SettingKeys.FiscalCnpj) ?? "") : Digits(config!.Cnpj);
        var uf = (demo ? await Get(SettingKeys.FiscalUf) : config!.Uf)?.Trim().ToUpperInvariant();
        var name = (await Get(SettingKeys.FiscalEmitName))?.Trim();
        var ie = (await Get(SettingKeys.FiscalEmitIe))?.Trim();
        var crt = await Get(SettingKeys.FiscalEmitCrt) == "1" ? 1 : 3;
        var street = (await Get(SettingKeys.FiscalEmitStreet))?.Trim();
        var number = (await Get(SettingKeys.FiscalEmitNumber))?.Trim();
        var district = (await Get(SettingKeys.FiscalEmitDistrict))?.Trim();
        var cityCode = Digits(await Get(SettingKeys.FiscalEmitCityCode) ?? "");
        var cityName = (await Get(SettingKeys.FiscalEmitCityName))?.Trim();
        var cep = Digits(await Get(SettingKeys.FiscalEmitCep) ?? "");
        var phone = Digits(await Get(SettingKeys.FiscalEmitPhone) ?? "");

        if (demo)
        {
            // Valores fictícios do modo demonstração (chave e CNPJ inválidos de propósito).
            cnpj = cnpj.Length == 14 ? cnpj : "99999999000191";
            uf = string.IsNullOrWhiteSpace(uf) ? "SP" : uf;
            name ??= "SUA EMPRESA (DEMONSTRACAO)";
            street ??= "Rua Exemplo";
            number ??= "100";
            district ??= "Centro";
            cityCode = cityCode.Length == 7 ? cityCode : "3550308";
            cityName ??= "São Paulo";
            cep = cep.Length == 8 ? cep : "01000000";
        }
        else
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(name)) missing.Add("razão social");
            if (string.IsNullOrWhiteSpace(street)) missing.Add("logradouro");
            if (string.IsNullOrWhiteSpace(number)) missing.Add("número");
            if (string.IsNullOrWhiteSpace(district)) missing.Add("bairro");
            if (cityCode.Length != 7) missing.Add("código IBGE do município (7 dígitos)");
            if (string.IsNullOrWhiteSpace(cityName)) missing.Add("município");
            if (cep.Length != 8) missing.Add("CEP");
            if (missing.Count > 0)
                return Result.Failure<NfeEmitter>(
                    "Complete os dados do emitente em Configurações para emitir NF-e: " + string.Join(", ", missing) + ".");
            if (string.IsNullOrWhiteSpace(uf) || !NfeAccessKey.UfCodes.ContainsKey(uf))
                return Result.Failure<NfeEmitter>("UF do emitente inválida. Revise as Configurações.");
        }

        return Result.Success(new NfeEmitter(cnpj, name!, ie, crt, street!, number!, district!,
            int.Parse(cityCode), cityName!, uf!, cep, string.IsNullOrWhiteSpace(phone) ? null : phone));
    }

    private async Task<decimal> GetRateAsync(string key, decimal fallback, CancellationToken ct)
    {
        var raw = (await _uow.Settings.GetByKeyAsync(key, ct))?.Value?.Replace(',', '.');
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var rate) && rate is >= 0 and <= 100
            ? rate
            : fallback;
    }

    private async Task<int> GetSeriesAsync(CancellationToken ct)
    {
        var raw = (await _uow.Settings.GetByKeyAsync(SettingKeys.FiscalEmitSeries, ct))?.Value;
        return int.TryParse(raw, out var series) && series is >= 1 and <= 889 ? series : 1;
    }

    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());

    private static IssuedNfeDto ToDto(IssuedNfe n) => new(
        n.Id, n.Number, n.Series, n.AccessKey, n.RecipientCnpjCpf, n.RecipientName,
        n.NatureOfOperation, n.IssuedAt, n.TotalValue, n.Status, n.Protocol, n.IsProduction,
        n.CanceledAt, n.CancelJustification);
}
