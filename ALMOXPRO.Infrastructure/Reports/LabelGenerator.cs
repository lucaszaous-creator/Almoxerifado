using ALMOXPRO.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ALMOXPRO.Infrastructure.Reports;

/// <summary>Etiquetas de produto e localização em PDF, prontas para impressão.</summary>
public class LabelGenerator : ILabelGenerator
{
    private readonly IBarcodeGenerator _barcode;
    private readonly IQrCodeGenerator _qrCode;

    static LabelGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public LabelGenerator(IBarcodeGenerator barcode, IQrCodeGenerator qrCode)
    {
        _barcode = barcode;
        _qrCode = qrCode;
    }

    public byte[] ProductLabelPdf(string productName, string internalCode, string? barcode, string? qrContent, int copies = 1)
    {
        var barcodePng = string.IsNullOrWhiteSpace(barcode) ? null : _barcode.GeneratePng(barcode, 280, 70);
        var qrPng = string.IsNullOrWhiteSpace(qrContent) ? null : _qrCode.GeneratePng(qrContent, 6);

        return Document.Create(container =>
        {
            for (var i = 0; i < Math.Max(1, copies); i++)
            {
                container.Page(page =>
                {
                    // Etiqueta 90x50mm.
                    page.Size(90, 50, Unit.Millimetre);
                    page.Margin(3, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Content().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(productName).FontSize(10).SemiBold().ClampLines(2);
                            col.Item().Text($"Cód: {internalCode}").FontSize(9);
                            if (barcodePng is not null)
                                col.Item().PaddingTop(4).Image(barcodePng).FitWidth();
                        });

                        if (qrPng is not null)
                            row.ConstantItem(60).AlignMiddle().Image(qrPng).FitArea();
                    });
                });
            }
        }).GeneratePdf();
    }

    public byte[] LocationLabelPdf(string locationCode, string warehouseName, int copies = 1)
    {
        var qrPng = _qrCode.GeneratePng($"ALMOXPRO:LOCATION:{locationCode}", 8);

        return Document.Create(container =>
        {
            for (var i = 0; i < Math.Max(1, copies); i++)
            {
                container.Page(page =>
                {
                    page.Size(90, 50, Unit.Millimetre);
                    page.Margin(3, Unit.Millimetre);

                    page.Content().Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Column(col =>
                        {
                            col.Item().Text(warehouseName).FontSize(9).FontColor(Colors.Grey.Darken1);
                            col.Item().Text(locationCode).FontSize(16).Bold();
                        });
                        row.ConstantItem(90).AlignMiddle().Image(qrPng).FitArea();
                    });
                });
            }
        }).GeneratePdf();
    }
}
