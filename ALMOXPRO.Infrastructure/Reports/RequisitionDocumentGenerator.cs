using ALMOXPRO.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ALMOXPRO.Infrastructure.Reports;

/// <summary>
/// Documento de requisição de materiais em PDF (A4), com quadro de itens e
/// campos de assinatura para o almoxarife e para quem retira o material.
/// </summary>
public class RequisitionDocumentGenerator : IRequisitionDocumentGenerator
{
    static RequisitionDocumentGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(RequisitionDocument document)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(document.CompanyName).FontSize(14).Bold();
                            col.Item().Text("Requisição de Materiais").FontSize(11)
                                .FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(180).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Nº {document.Number}").FontSize(14).Bold();
                            col.Item().Text($"Situação: {document.Status}").FontSize(9);
                        });
                    });
                    header.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(12).Column(content =>
                {
                    content.Item().Table(info =>
                    {
                        info.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });
                        info.Cell().Element(InfoCell).Text(t =>
                        {
                            t.Span("Setor solicitante: ").SemiBold();
                            t.Span(document.SectorName);
                        });
                        info.Cell().Element(InfoCell).Text(t =>
                        {
                            t.Span("Almoxarifado: ").SemiBold();
                            t.Span(document.WarehouseName);
                        });
                        info.Cell().Element(InfoCell).Text(t =>
                        {
                            t.Span("Solicitante / retira: ").SemiBold();
                            t.Span(document.RequestedBy);
                        });
                        info.Cell().Element(InfoCell).Text(t =>
                        {
                            t.Span("Data: ").SemiBold();
                            t.Span(document.RequestDate.ToString("dd/MM/yyyy HH:mm"));
                        });
                        info.Cell().Element(InfoCell).Text(t =>
                        {
                            t.Span("Emitida por: ").SemiBold();
                            t.Span(document.CreatedByUser);
                        });
                        info.Cell().Element(InfoCell).Text(t =>
                        {
                            t.Span("Observações: ").SemiBold();
                            t.Span(string.IsNullOrWhiteSpace(document.Notes) ? "-" : document.Notes);
                        });

                        static QuestPDF.Infrastructure.IContainer InfoCell(QuestPDF.Infrastructure.IContainer cell) =>
                            cell.PaddingVertical(3);
                    });

                    content.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(70);
                            cols.RelativeColumn();
                            cols.ConstantColumn(40);
                            cols.ConstantColumn(80);
                            cols.ConstantColumn(80);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Código");
                            h.Cell().Element(HeaderCell).Text("Produto");
                            h.Cell().Element(HeaderCell).Text("Un.");
                            h.Cell().Element(HeaderCell).AlignRight().Text("Solicitado");
                            h.Cell().Element(HeaderCell).AlignRight().Text("Entregue");

                            static QuestPDF.Infrastructure.IContainer HeaderCell(QuestPDF.Infrastructure.IContainer cell) =>
                                cell.Background(Colors.Grey.Lighten3).BorderBottom(1)
                                    .BorderColor(Colors.Grey.Medium).Padding(5).DefaultTextStyle(x => x.SemiBold());
                        });

                        foreach (var item in document.Items)
                        {
                            table.Cell().Element(BodyCell).Text(item.Code);
                            table.Cell().Element(BodyCell).Text(item.Name);
                            table.Cell().Element(BodyCell).Text(item.Unit);
                            table.Cell().Element(BodyCell).AlignRight().Text(item.Quantity.ToString("N2"));
                            // Coluna "Entregue" em branco para preenchimento manual na conferência.
                            table.Cell().Element(BodyCell).AlignRight().Text("________");
                        }

                        static QuestPDF.Infrastructure.IContainer BodyCell(QuestPDF.Infrastructure.IContainer cell) =>
                            cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5);
                    });

                    content.Item().PaddingTop(50).Row(signatures =>
                    {
                        signatures.RelativeItem().PaddingHorizontal(10).Column(col =>
                        {
                            col.Item().LineHorizontal(1).LineColor(Colors.Black);
                            col.Item().AlignCenter().PaddingTop(4).Text("Entregue por (Almoxarifado)").FontSize(9);
                        });
                        signatures.RelativeItem().PaddingHorizontal(10).Column(col =>
                        {
                            col.Item().LineHorizontal(1).LineColor(Colors.Black);
                            col.Item().AlignCenter().PaddingTop(4).Text("Recebido por (assinatura e data)").FontSize(9);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("ALMOX PRO — documento gerado em ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }
}
