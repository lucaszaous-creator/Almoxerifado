using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;

namespace ALMOXPRO.UI.ViewModels;

public record ReportOption(ReportKind Kind, string Label);

public partial class ReportsViewModel : ViewModelBase
{
    [ObservableProperty]
    private ReportOption? _selectedReport;

    [ObservableProperty]
    private DateTime? _from;

    [ObservableProperty]
    private DateTime? _to;

    [ObservableProperty]
    private DataView? _preview;

    [ObservableProperty]
    private string _previewInfo = string.Empty;

    private ReportTable? _lastTable;

    public ObservableCollection<ReportOption> Reports { get; } =
    [
        new(ReportKind.Produtos, "Produtos"),
        new(ReportKind.Estoque, "Estoque"),
        new(ReportKind.Entradas, "Entradas"),
        new(ReportKind.Saidas, "Saídas"),
        new(ReportKind.Movimentacoes, "Movimentações"),
        new(ReportKind.Fornecedores, "Fornecedores"),
        new(ReportKind.ProdutosVencidos, "Produtos vencidos"),
        new(ReportKind.EstoqueMinimo, "Estoque mínimo"),
        new(ReportKind.ValorEmEstoque, "Valor em estoque"),
        new(ReportKind.CurvaAbc, "Curva ABC"),
        new(ReportKind.ConsumoPorSetor, "Consumo por setor"),
        new(ReportKind.ConsumoPorFuncionario, "Consumo por funcionário"),
    ];

    public override string Title => "Relatórios";

    public ReportsViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog)
        : base(scopeFactory, dialog)
    {
        SelectedReport = Reports[0];
    }

    [RelayCommand]
    private Task GenerateAsync() => RunAsync(async services =>
    {
        if (SelectedReport is null)
            return;

        var reports = services.GetRequiredService<IReportService>();
        _lastTable = await reports.BuildAsync(SelectedReport.Kind, From, To);

        var dataTable = new DataTable();
        foreach (var column in _lastTable.Columns)
            dataTable.Columns.Add(column);
        foreach (var row in _lastTable.Rows)
            dataTable.Rows.Add(row.Cast<object>().ToArray());

        Preview = dataTable.DefaultView;
        PreviewInfo = $"{_lastTable.Title} — {_lastTable.Rows.Count} registro(s)";
    });

    [RelayCommand]
    private Task ExportPdfAsync() => ExportAsync("pdf", (exporter, table) => exporter.ToPdf(table));

    [RelayCommand]
    private Task ExportExcelAsync() => ExportAsync("xlsx", (exporter, table) => exporter.ToExcel(table));

    [RelayCommand]
    private Task ExportCsvAsync() => ExportAsync("csv", (exporter, table) => exporter.ToCsv(table));

    private Task ExportAsync(string extension, Func<IReportExporter, ReportTable, byte[]> render) =>
        RunAsync(async services =>
        {
            if (_lastTable is null)
            {
                Dialog.ShowError("Gere o relatório antes de exportar.");
                return;
            }

            var filter = extension switch
            {
                "pdf" => "PDF (*.pdf)|*.pdf",
                "xlsx" => "Excel (*.xlsx)|*.xlsx",
                _ => "CSV (*.csv)|*.csv"
            };

            var suggested = $"{_lastTable.Title.Replace(' ', '_').ToLowerInvariant()}_{DateTime.Now:yyyyMMdd}.{extension}";
            var path = Dialog.SaveFile(suggested, filter);
            if (path is null)
                return;

            var exporter = services.GetRequiredService<IReportExporter>();
            var bytes = render(exporter, _lastTable);
            await File.WriteAllBytesAsync(path, bytes);

            var audit = services.GetRequiredService<IAuditService>();
            await audit.LogActionAsync("Exportação", "Relatórios", "reports", _lastTable.Title);

            Dialog.ShowInfo($"Relatório exportado em:\n{path}");
        });
}
