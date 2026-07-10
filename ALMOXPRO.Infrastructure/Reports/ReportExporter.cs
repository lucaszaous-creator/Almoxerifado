using ALMOXPRO.Application.Interfaces;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace ALMOXPRO.Infrastructure.Reports;

/// <summary>Exporta tabelas de relatório em PDF (QuestPDF), Excel (ClosedXML) e CSV.</summary>
public class ReportExporter : IReportExporter
{
    static ReportExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ToPdf(ReportTable table)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item().Text("ALMOX PRO").FontSize(8).FontColor(Colors.Grey.Medium);
                    header.Item().Text(table.Title).FontSize(16).SemiBold();
                    if (!string.IsNullOrWhiteSpace(table.Subtitle))
                        header.Item().Text(table.Subtitle).FontSize(10).FontColor(Colors.Grey.Darken1);
                    header.Item().PaddingBottom(8)
                        .Text($"Emitido em {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().Table(pdfTable =>
                {
                    pdfTable.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in table.Columns)
                            columns.RelativeColumn();
                    });

                    pdfTable.Header(header =>
                    {
                        foreach (var column in table.Columns)
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4)
                                .Text(column).SemiBold();
                    });

                    foreach (var row in table.Rows)
                    {
                        foreach (var cell in row)
                            pdfTable.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(4).Text(cell);
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ToExcel(ReportTable table)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(Truncate(table.Title, 31));

        sheet.Cell(1, 1).Value = table.Title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        var headerRow = 2;
        if (!string.IsNullOrWhiteSpace(table.Subtitle))
        {
            sheet.Cell(2, 1).Value = table.Subtitle;
            headerRow = 3;
        }

        for (var c = 0; c < table.Columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = table.Columns[c];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.LightGray);
        }

        for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Rows[r].Count; c++)
                sheet.Cell(headerRow + 1 + r, c + 1).Value = table.Rows[r][c];

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ToCsv(ReportTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", table.Columns.Select(Escape)));
        foreach (var row in table.Rows)
            sb.AppendLine(string.Join(";", row.Select(Escape)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Escape(string value) =>
        value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
