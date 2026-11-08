using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.IO;

namespace ALMOXPRO.UI.ViewModels;

public partial class LocationsViewModel : ViewModelBase
{
    [ObservableProperty]
    private LocationDto? _selected;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private LocationUpsertDto _editor = new();

    public ObservableCollection<LocationDto> Items { get; } = [];
    public ObservableCollection<LookupDto> Warehouses { get; } = [];

    public override string Title => "Localizações";

    public LocationsViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog)
        : base(scopeFactory, dialog)
    {
    }

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async services =>
    {
        var lookups = services.GetRequiredService<ILookupService>();
        Warehouses.Clear();
        foreach (var warehouse in await lookups.WarehousesAsync())
            Warehouses.Add(warehouse);

        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private void New()
    {
        Editor = new LocationUpsertDto { WarehouseId = Warehouses.FirstOrDefault()?.Id ?? 0 };
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void Edit()
    {
        if (Selected is null)
            return;
        Editor = new LocationUpsertDto
        {
            Id = Selected.Id,
            WarehouseId = Selected.WarehouseId,
            Building = Selected.Building,
            Floor = Selected.Floor,
            Corridor = Selected.Corridor,
            Shelf = Selected.Shelf,
            Rack = Selected.Rack,
            Position = Selected.Position,
            Status = Selected.Status
        };
        IsEditorOpen = true;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async services =>
    {
        var locations = services.GetRequiredService<ILocationService>();
        var result = await locations.SaveAsync(Editor);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }
        IsEditorOpen = false;
        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private Task DeleteAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;
        if (!Dialog.Confirm($"Excluir a localização '{Selected.Code}'?"))
            return;

        var locations = services.GetRequiredService<ILocationService>();
        var result = await locations.DeleteAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }
        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private Task PrintLabelAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;

        var labels = services.GetRequiredService<ILabelGenerator>();
        var pdf = labels.LocationLabelPdf(Selected.Code, Selected.WarehouseName);

        var path = Dialog.SaveFile($"etiqueta_local_{Selected.Code}.pdf", "PDF (*.pdf)|*.pdf");
        if (path is null)
            return;

        await File.WriteAllBytesAsync(path, pdf);
        Dialog.ShowInfo($"Etiqueta gerada em:\n{path}");
    });

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        var locations = services.GetRequiredService<ILocationService>();
        Items.Clear();
        foreach (var location in await locations.GetAllAsync())
            Items.Add(location);
    }
}
