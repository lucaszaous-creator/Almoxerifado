using System.Globalization;
using System.Xml.Linq;

namespace ALMOXPRO.Application.Services;

public record ParsedNFeSummary(
    string AccessKey,
    string EmitterCnpj,
    string EmitterName,
    DateTime IssuedAt,
    decimal TotalValue);

/// <summary>
/// Extrai os campos usados pelo sistema dos XMLs da Distribuição DF-e
/// (resNFe = resumo; procNFe = nota completa com protocolo).
/// </summary>
public static class FiscalXmlParser
{
    private static readonly XNamespace Ns = "http://www.portalfiscal.inf.br/nfe";

    public static ParsedNFeSummary ParseResNFe(string xml)
    {
        var root = XDocument.Parse(xml).Root
            ?? throw new InvalidOperationException("XML de resumo inválido.");

        return new ParsedNFeSummary(
            Value(root, "chNFe"),
            Value(root, "CNPJ"),
            Value(root, "xNome"),
            ParseDate(Value(root, "dhEmi")),
            ParseDecimal(Value(root, "vNF")));
    }

    public static ParsedNFeSummary ParseProcNFe(string xml)
    {
        var doc = XDocument.Parse(xml);
        var infNFe = doc.Descendants(Ns + "infNFe").FirstOrDefault()
            ?? throw new InvalidOperationException("XML procNFe inválido: infNFe não encontrado.");

        var accessKey = (infNFe.Attribute("Id")?.Value ?? string.Empty).Replace("NFe", "");
        var emit = infNFe.Element(Ns + "emit");
        var ide = infNFe.Element(Ns + "ide");
        var total = infNFe.Element(Ns + "total")?.Element(Ns + "ICMSTot");

        return new ParsedNFeSummary(
            accessKey,
            emit?.Element(Ns + "CNPJ")?.Value ?? string.Empty,
            emit?.Element(Ns + "xNome")?.Value ?? string.Empty,
            ParseDate(ide?.Element(Ns + "dhEmi")?.Value ?? ide?.Element(Ns + "dEmi")?.Value ?? string.Empty),
            ParseDecimal(total?.Element(Ns + "vNF")?.Value ?? "0"));
    }

    private static string Value(XElement root, string name) =>
        root.Descendants(Ns + name).FirstOrDefault()?.Value
        ?? root.Descendants(name).FirstOrDefault()?.Value
        ?? string.Empty;

    private static DateTime ParseDate(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            return dto.UtcDateTime;
        return DateTime.UtcNow;
    }

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
}
