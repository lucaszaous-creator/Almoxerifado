using ALMOXPRO.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Xml.Linq;

namespace ALMOXPRO.Infrastructure.Fiscal;

/// <summary>
/// DANFE (documento auxiliar) em PDF A4 gerado a partir do XML procNFe:
/// emitente, destinatário, itens, totais, chave de acesso com código de
/// barras e protocolo de autorização.
/// </summary>
public class DanfeGenerator : IDanfeGenerator
{
    private static readonly XNamespace Ns = "http://www.portalfiscal.inf.br/nfe";
    private readonly IBarcodeGenerator _barcode;

    static DanfeGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DanfeGenerator(IBarcodeGenerator barcode) => _barcode = barcode;

    public byte[] GeneratePdf(string procNFeXml)
    {
        var doc = XDocument.Parse(procNFeXml);
        var infNFe = doc.Descendants(Ns + "infNFe").First();
        var ide = infNFe.Element(Ns + "ide");
        var emit = infNFe.Element(Ns + "emit");
        var dest = infNFe.Element(Ns + "dest");
        var totais = infNFe.Element(Ns + "total")?.Element(Ns + "ICMSTot");
        var protocolo = doc.Descendants(Ns + "infProt").FirstOrDefault();
        var accessKey = (infNFe.Attribute("Id")?.Value ?? string.Empty).Replace("NFe", "");

        var items = infNFe.Elements(Ns + "det").Select(det =>
        {
            var prod = det.Element(Ns + "prod");
            return new
            {
                Codigo = Text(prod, "cProd"),
                Descricao = Text(prod, "xProd"),
                Ncm = Text(prod, "NCM"),
                Unidade = Text(prod, "uCom"),
                Quantidade = Number(prod, "qCom"),
                ValorUnitario = Number(prod, "vUnCom"),
                ValorTotal = Number(prod, "vProd")
            };
        }).ToList();

        var barcodePng = string.IsNullOrEmpty(accessKey) ? null : _barcode.GeneratePng(accessKey, 460, 60);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Border(1).BorderColor(Colors.Grey.Darken1).Padding(8).Row(row =>
                {
                    row.RelativeItem(3).Column(col =>
                    {
                        col.Item().Text(Text(emit, "xNome")).FontSize(12).Bold();
                        col.Item().Text($"CNPJ: {FormatCnpj(Text(emit, "CNPJ"))}   IE: {Text(emit, "IE")}");
                        var end = emit?.Element(Ns + "enderEmit");
                        col.Item().Text($"{Text(end, "xLgr")}, {Text(end, "nro")} - {Text(end, "xBairro")}");
                        col.Item().Text($"{Text(end, "xMun")}/{Text(end, "UF")} - CEP {Text(end, "CEP")}");
                    });
                    row.RelativeItem(2).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text("DANFE").FontSize(14).Bold();
                        col.Item().AlignRight().Text("Documento Auxiliar da Nota Fiscal Eletrônica").FontSize(7);
                        col.Item().AlignRight().Text($"Nº {Text(ide, "nNF")}  Série {Text(ide, "serie")}").FontSize(10).SemiBold();
                        col.Item().AlignRight().Text($"Emissão: {FormatDate(Text(ide, "dhEmi"))}");
                    });
                });

                page.Content().PaddingVertical(6).Column(content =>
                {
                    // Chave de acesso + código de barras
                    content.Item().Border(1).BorderColor(Colors.Grey.Darken1).Padding(6).Column(col =>
                    {
                        col.Item().Text("CHAVE DE ACESSO").FontSize(6).FontColor(Colors.Grey.Darken1);
                        col.Item().Text(FormatAccessKey(accessKey)).FontSize(9).SemiBold();
                        if (barcodePng is not null)
                            col.Item().PaddingTop(4).MaxHeight(50).Image(barcodePng).FitWidth();
                        if (protocolo is not null)
                            col.Item().PaddingTop(4).Text(
                                $"Protocolo de autorização: {Text(protocolo, "nProt")} — {FormatDate(Text(protocolo, "dhRecbto"))}")
                                .FontSize(7);
                    });

                    // Destinatário
                    content.Item().PaddingTop(6).Border(1).BorderColor(Colors.Grey.Darken1).Padding(6).Column(col =>
                    {
                        col.Item().Text("DESTINATÁRIO").FontSize(6).FontColor(Colors.Grey.Darken1);
                        col.Item().Text($"{Text(dest, "xNome")} — CNPJ/CPF: {FormatCnpj(Text(dest, "CNPJ") is { Length: > 0 } c ? c : Text(dest, "CPF"))}");
                        var endDest = dest?.Element(Ns + "enderDest");
                        if (endDest is not null)
                            col.Item().Text($"{Text(endDest, "xLgr")}, {Text(endDest, "nro")} - {Text(endDest, "xMun")}/{Text(endDest, "UF")}");
                    });

                    // Itens
                    content.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(55);
                            cols.RelativeColumn();
                            cols.ConstantColumn(50);
                            cols.ConstantColumn(30);
                            cols.ConstantColumn(55);
                            cols.ConstantColumn(55);
                            cols.ConstantColumn(60);
                        });

                        table.Header(h =>
                        {
                            foreach (var title in new[] { "Código", "Descrição", "NCM", "Un.", "Qtde", "V. Unit.", "V. Total" })
                                h.Cell().Background(Colors.Grey.Lighten3).BorderBottom(1)
                                    .BorderColor(Colors.Grey.Darken1).Padding(3).Text(title).SemiBold().FontSize(7);
                        });

                        foreach (var item in items)
                        {
                            table.Cell().Element(Cell).Text(item.Codigo);
                            table.Cell().Element(Cell).Text(item.Descricao);
                            table.Cell().Element(Cell).Text(item.Ncm);
                            table.Cell().Element(Cell).Text(item.Unidade);
                            table.Cell().Element(Cell).AlignRight().Text(item.Quantidade.ToString("N4"));
                            table.Cell().Element(Cell).AlignRight().Text(item.ValorUnitario.ToString("N4"));
                            table.Cell().Element(Cell).AlignRight().Text(item.ValorTotal.ToString("N2"));
                        }

                        static QuestPDF.Infrastructure.IContainer Cell(QuestPDF.Infrastructure.IContainer cell) =>
                            cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
                    });

                    // Totais
                    content.Item().PaddingTop(6).Border(1).BorderColor(Colors.Grey.Darken1).Padding(6).Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("Valor dos produtos: ").SemiBold();
                            t.Span(Number(totais, "vProd").ToString("C2", Culture));
                        });
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("Frete: ").SemiBold();
                            t.Span(Number(totais, "vFrete").ToString("C2", Culture));
                        });
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("ICMS: ").SemiBold();
                            t.Span(Number(totais, "vICMS").ToString("C2", Culture));
                        });
                        row.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Span("TOTAL DA NOTA: ").Bold();
                            t.Span(Number(totais, "vNF").ToString("C2", Culture)).Bold();
                        });
                    });
                });

                page.Footer().AlignCenter()
                    .Text($"DANFE gerado pelo ALMOX PRO em {DateTime.Now:dd/MM/yyyy HH:mm} — sem valor fiscal, consulte o XML")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    private static string Text(XElement? parent, string name) =>
        parent?.Element(Ns + name)?.Value ?? string.Empty;

    private static decimal Number(XElement? parent, string name) =>
        decimal.TryParse(Text(parent, name), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static string FormatCnpj(string cnpj) =>
        cnpj.Length == 14
            ? $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}"
            : cnpj;

    private static string FormatAccessKey(string key) =>
        string.Join(" ", Enumerable.Range(0, key.Length / 4).Select(i => key.Substring(i * 4, 4)));

    private static string FormatDate(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : value;
}
