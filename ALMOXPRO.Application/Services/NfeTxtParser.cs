using System.Globalization;
using System.Text.RegularExpressions;

namespace ALMOXPRO.Application.Services;

public record NfeTxtItem(
    string Code,
    string Description,
    string Ncm,
    string Cfop,
    string Unit,
    decimal Quantity,
    decimal UnitValue,
    string? IcmsCst,
    decimal? IcmsRate,
    decimal? IcmsBaseReductionPct,
    /// <summary>CSOSN quando o arquivo veio de emitente do Simples (registros N10*).</summary>
    string? Csosn);

/// <summary>Resultado da leitura de um TXT de NF-e (primeira nota do arquivo).</summary>
public record NfeTxtNote(
    string NatOp,
    int Finality,
    string? ReferencedKey,
    string? EmitterCnpj,
    string RecipientCnpjCpf,
    string RecipientName,
    int RecipientIeIndicator,
    string? RecipientIe,
    string Street,
    string Number,
    string District,
    string CityCode,
    string CityName,
    string Uf,
    string Cep,
    /// <summary>tPag do primeiro detPag (registro YA); null quando ausente.</summary>
    int? PaymentMethod,
    string? AdditionalInfo,
    /// <summary>True quando algum item destaca imposto (CST 00/20/60 ou PIS por alíquota).</summary>
    bool LooksLikeTaxedSale,
    IReadOnlyList<NfeTxtItem> Items,
    IReadOnlyList<string> Warnings,
    int NotesInFile);

/// <summary>
/// Lê o arquivo TXT da NF-e no leiaute pipe-delimited do emissor gratuito
/// (o mesmo consumido por UniNFe/NFe.mail e exportado por PMS hoteleiros).
/// Lê apenas os campos que o formulário de emissão usa; o restante é ignorado.
/// Tolera as variações 3.10/4.00 do registro B (indPag) e do registro I
/// (cBenef/CEST) localizando o CFOP pelo formato em vez da posição fixa.
/// </summary>
public static class NfeTxtParser
{
    public static bool TryParse(string content, out NfeTxtNote note, out IReadOnlyList<string> errors)
    {
        note = null!;
        var errorList = new List<string>();
        var warnings = new List<string>();
        errors = errorList;

        var lines = content
            .Split('\n')
            .Select(l => l.TrimEnd('\r').Trim())
            .Where(l => l.Length > 0 && l.Contains('|'))
            .ToList();
        if (lines.Count == 0)
        {
            errorList.Add("O arquivo não contém registros no formato esperado (campos separados por '|').");
            return false;
        }

        var notesInFile = 0;
        string natOp = string.Empty, emitterCnpj = string.Empty;
        var finality = 1;
        string? referencedKey = null;
        string recDoc = string.Empty, recName = string.Empty, recIe = string.Empty;
        var recIeInd = 9;
        string street = string.Empty, number = string.Empty, district = string.Empty;
        string cityCode = string.Empty, cityName = string.Empty, uf = string.Empty, cep = string.Empty;
        int? payment = null;
        string? additionalInfo = null;
        var items = new List<NfeTxtItem>();
        var seenA = 0;

        foreach (var line in lines)
        {
            var fields = line.Split('|');
            // O separador final é opcional no leiaute ("C02|123|"), remove o campo vazio do fim.
            if (fields.Length > 1 && fields[^1].Length == 0)
                fields = fields[..^1];
            var tag = fields[0].Trim().ToUpperInvariant().Replace(" ", "");
            var f = fields.Skip(1).Select(v => v.Trim()).ToArray();
            string F(int i) => i < f.Length ? f[i] : string.Empty;

            switch (tag)
            {
                case "NOTAFISCAL":
                    notesInFile = ParseInt(F(0)) ?? 0;
                    break;

                case "A":
                    seenA++;
                    if (seenA > 1)
                        goto Done; // Só a primeira nota do arquivo é importada.
                    if (!F(0).StartsWith("4.", StringComparison.Ordinal))
                        warnings.Add($"Arquivo na versão de leiaute {F(0)} (esperado 4.00) — os campos foram interpretados, confira antes de emitir.");
                    break;

                case "B":
                {
                    // 4.00: cUF|cNF|natOp|mod|serie|nNF|... — 3.10 tem indPag entre natOp e mod.
                    var shift = F(3) is "55" or "65" ? 0 : F(4) is "55" or "65" ? 1 : -1;
                    if (shift < 0)
                    {
                        errorList.Add("Registro B: não foi possível localizar o modelo (55) — leiaute não reconhecido.");
                        break;
                    }
                    natOp = F(2);
                    var model = F(3 + shift);
                    if (model != "55")
                        errorList.Add($"O arquivo é do modelo {model}; esta tela emite apenas NF-e modelo 55.");
                    var fin = ParseInt(F(15 + shift));
                    if (fin is >= 1 and <= 4)
                        finality = fin.Value;
                    break;
                }

                // Chave referenciada (NFref): B13 no leiaute antigo, BA02 no atual.
                case "B13" or "BA02":
                {
                    var key = Regex.Match(line, @"\d{44}");
                    if (key.Success)
                        referencedKey = key.Value;
                    break;
                }

                case "C02":
                    emitterCnpj = Digits(F(0));
                    break;

                case "E":
                    recName = F(0);
                    recIeInd = ParseInt(F(1)) is 1 or 2 ? ParseInt(F(1))!.Value : 9;
                    recIe = F(2);
                    break;
                case "E02" or "E03":
                    recDoc = Digits(F(0));
                    break;

                // Endereço do destinatário (o C05, do emitente, é ignorado —
                // o emitente da nota vem das Configurações do sistema).
                case "E05":
                    street = F(0);
                    number = F(1);
                    district = F(3);
                    cityCode = Digits(F(4));
                    cityName = F(5);
                    uf = F(6);
                    cep = Digits(F(7));
                    break;

                case "I":
                {
                    // cProd|cEAN|xProd|NCM|...|CFOP|uCom|qCom|vUnCom|... — o CFOP é
                    // localizado pelo formato (4 dígitos iniciando em 1-7) porque
                    // versões do leiaute inserem EXTIPI/cBenef/CEST antes dele.
                    var cfopIndex = -1;
                    for (var i = 4; i <= 8 && i < f.Length; i++)
                        if (Regex.IsMatch(f[i], @"^[1-7]\d{3}$"))
                        {
                            cfopIndex = i;
                            break;
                        }
                    if (cfopIndex < 0)
                    {
                        errorList.Add($"Item \"{F(2)}\": CFOP não encontrado no registro I.");
                        break;
                    }

                    var qty = ParseDecimal(F(cfopIndex + 2));
                    var unitValue = ParseDecimal(F(cfopIndex + 3));
                    if (qty is null || unitValue is null)
                    {
                        errorList.Add($"Item \"{F(2)}\": quantidade/valor unitário inválidos no registro I.");
                        break;
                    }

                    items.Add(new NfeTxtItem(F(0), F(2), Digits(F(3)), f[cfopIndex],
                        F(cfopIndex + 1), qty.Value, unitValue.Value, null, null, null, null));
                    break;
                }

                // ICMS do último item lido.
                case "N02": // ICMS00: orig|CST|modBC|vBC|pICMS|vICMS
                    UpdateLastItem(items, i => i with { IcmsCst = "00", IcmsRate = ParseDecimal(F(4)) });
                    break;
                case "N04": // ICMS20: orig|CST|modBC|pRedBC|vBC|pICMS|vICMS
                    UpdateLastItem(items, i => i with
                    {
                        IcmsCst = "20",
                        IcmsBaseReductionPct = ParseDecimal(F(3)),
                        IcmsRate = ParseDecimal(F(5))
                    });
                    break;
                case "N06": // ICMS40/41/50: orig|CST|vICMSDeson|motDesICMS
                    UpdateLastItem(items, i => i with { IcmsCst = F(1) });
                    break;
                case "N08": // ICMS60
                    UpdateLastItem(items, i => i with { IcmsCst = "60" });
                    break;
                case var n when n.StartsWith("N10", StringComparison.Ordinal): // CSOSN (Simples)
                    UpdateLastItem(items, i => i with { Csosn = F(1) });
                    break;

                case "YA":
                {
                    // 4.00 aceita indPag opcional antes do tPag: YA|tPag|vPag ou YA|indPag|tPag|vPag.
                    var first = ParseInt(F(0));
                    payment ??= first is 0 or 1 or 2 && F(0).Length == 1 ? ParseInt(F(1)) : first;
                    break;
                }

                case "Z":
                    additionalInfo = string.IsNullOrWhiteSpace(F(1)) ? F(0) : F(1);
                    break;
            }
        }

    Done:
        if (seenA == 0)
            errorList.Add("Registro A (versão do leiaute) não encontrado — o arquivo não parece ser um TXT de NF-e.");
        if (items.Count == 0)
            errorList.Add("Nenhum item (registro I) encontrado no arquivo.");
        if (recDoc.Length == 0)
            errorList.Add("CNPJ/CPF do destinatário (registro E02/E03) não encontrado.");
        if (errorList.Count > 0)
            return false;

        var moreNotes = Math.Max(notesInFile, seenA);
        if (moreNotes > 1)
            warnings.Add($"O arquivo contém {moreNotes} notas — somente a primeira foi importada (importe as demais separadamente).");
        if (items.Any(i => i.Csosn is not null))
            warnings.Add("O arquivo usa CSOSN (emitente do Simples Nacional) — confira o regime tributário nas Configurações.");

        var taxed = items.Any(i => i.IcmsCst is "00" or "20" or "60");
        note = new NfeTxtNote(natOp, finality, referencedKey, emitterCnpj, recDoc, recName,
            recIeInd, recIe, street, number, district, cityCode, cityName, uf, cep,
            payment, additionalInfo, taxed, items, warnings, moreNotes);
        return true;
    }

    private static void UpdateLastItem(List<NfeTxtItem> items, Func<NfeTxtItem, NfeTxtItem> update)
    {
        if (items.Count > 0)
            items[^1] = update(items[^1]);
    }

    private static string Digits(string value) => new([.. value.Where(char.IsDigit)]);

    private static int? ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    private static decimal? ParseDecimal(string value)
    {
        value = value.Replace(',', '.');
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
