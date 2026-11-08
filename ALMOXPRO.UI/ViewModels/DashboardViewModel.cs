using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Services;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ALMOXPRO.UI.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    [ObservableProperty]
    private DashboardDto? _data;

    public ObservableCollection<AbcItemDto> AbcCurve { get; } = [];
    public ObservableCollection<RecentMovementDto> RecentMovements { get; } = [];
    public ObservableCollection<ChartPointDto> Chart { get; } = [];

    public override string Title => "Dashboard";

    public DashboardViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog)
        : base(scopeFactory, dialog)
    {
    }

    public override Task LoadAsync() => RefreshAsync();

    // Cartões clicáveis: navegam para a tela/relatório correspondente.
    [RelayCommand]
    private void OpenProducts() =>
        WeakReferenceMessenger.Default.Send(new OpenScreenMessage(typeof(ProductsViewModel)));

    [RelayCommand]
    private void OpenReport(string kind) =>
        WeakReferenceMessenger.Default.Send(new OpenReportMessage(Enum.Parse<ReportKind>(kind)));

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async services =>
    {
        var dashboard = services.GetRequiredService<IDashboardService>();
        Data = await dashboard.GetAsync();

        AbcCurve.Clear();
        foreach (var item in Data.AbcCurve.Take(10))
            AbcCurve.Add(item);

        RecentMovements.Clear();
        foreach (var item in Data.RecentMovements)
            RecentMovements.Add(item);

        Chart.Clear();
        foreach (var item in Data.MovementsLast30Days)
            Chart.Add(item);
    });
}
