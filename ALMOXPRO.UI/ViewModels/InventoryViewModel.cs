using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Security;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ALMOXPRO.UI.ViewModels;

public partial class InventoryViewModel : ViewModelBase
{
    private readonly ISessionService _session;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private InventoryListDto? _selected;

    [ObservableProperty]
    private bool _isCountingOpen;

    [ObservableProperty]
    private InventoryType _newType = InventoryType.Geral;

    [ObservableProperty]
    private LookupDto? _newWarehouse;

    // Contagem
    [ObservableProperty]
    private string _scanCode = string.Empty;

    [ObservableProperty]
    private decimal _scanQuantity = 1;

    [ObservableProperty]
    private InventoryItemDto? _selectedItem;

    [ObservableProperty]
    private decimal _manualQuantity;

    public ObservableCollection<InventoryListDto> Items { get; } = [];
    public ObservableCollection<InventoryItemDto> CountItems { get; } = [];
    public ObservableCollection<LookupDto> Warehouses { get; } = [];
    public Array InventoryTypes => Enum.GetValues(typeof(InventoryType));

    public bool CanExecute => _session.HasPermission(PermissionCodes.InventoryExecute);
    public bool CanAdjust => _session.HasPermission(PermissionCodes.InventoryAdjust);

    public override string Title => "Inventário";

    public InventoryViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog, ISessionService session)
        : base(scopeFactory, dialog)
    {
        _session = session;
    }

    public override async Task LoadAsync()
    {
        await RunAsync(async services =>
        {
            var lookups = services.GetRequiredService<ILookupService>();
            Warehouses.Clear();
            foreach (var warehouse in await lookups.WarehousesAsync())
                Warehouses.Add(warehouse);

            await LoadIntoAsync(services);
        });
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadIntoAsync);

    [RelayCommand]
    private Task OpenInventoryAsync() => RunAsync(async services =>
    {
        if (NewWarehouse is null)
        {
            Dialog.ShowError("Selecione o almoxarifado.");
            return;
        }

        var inventories = services.GetRequiredService<IInventoryService>();
        var result = await inventories.OpenAsync(new InventoryOpenDto
        {
            Type = NewType,
            WarehouseId = NewWarehouse.Id
        });

        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }

        Dialog.Notify("Inventário aberto. Saldos congelados para contagem.");
        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private Task StartCountingAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;

        var inventories = services.GetRequiredService<IInventoryService>();
        CountItems.Clear();
        foreach (var item in await inventories.GetItemsAsync(Selected.Id))
            CountItems.Add(item);
        IsCountingOpen = true;
    });

    [RelayCommand]
    private Task ScanAsync() => RunAsync(async services =>
    {
        if (Selected is null || string.IsNullOrWhiteSpace(ScanCode))
            return;

        var inventories = services.GetRequiredService<IInventoryService>();
        var result = await inventories.RegisterCountByCodeAsync(Selected.Id, ScanCode, ScanQuantity);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }

        ScanCode = string.Empty;
        ScanQuantity = 1;
        await ReloadCountItemsAsync(services);
    });

    [RelayCommand]
    private Task RegisterManualCountAsync() => RunAsync(async services =>
    {
        if (Selected is null || SelectedItem is null)
            return;

        var inventories = services.GetRequiredService<IInventoryService>();
        var result = await inventories.RegisterCountAsync(Selected.Id, SelectedItem.Id, ManualQuantity);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }
        await ReloadCountItemsAsync(services);
    });

    [RelayCommand]
    private Task ApplyAdjustmentsAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;
        if (!Dialog.Confirm("Aplicar os ajustes de estoque para os itens com diferença?"))
            return;

        var inventories = services.GetRequiredService<IInventoryService>();
        var result = await inventories.ApplyAdjustmentsAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }

        Dialog.Notify("Ajustes aplicados ao estoque.");
        await ReloadCountItemsAsync(services);
    });

    [RelayCommand]
    private Task FinishAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;
        if (!Dialog.Confirm("Concluir este inventário? A contagem será encerrada."))
            return;

        var inventories = services.GetRequiredService<IInventoryService>();
        var result = await inventories.FinishAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }

        IsCountingOpen = false;
        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private Task CancelInventoryAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;
        if (!Dialog.Confirm("Cancelar este inventário?"))
            return;

        var inventories = services.GetRequiredService<IInventoryService>();
        var result = await inventories.CancelAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }

        IsCountingOpen = false;
        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private void CloseCounting() => IsCountingOpen = false;

    private async Task ReloadCountItemsAsync(IServiceProvider services)
    {
        if (Selected is null)
            return;
        var inventories = services.GetRequiredService<IInventoryService>();
        CountItems.Clear();
        foreach (var item in await inventories.GetItemsAsync(Selected.Id))
            CountItems.Add(item);
    }

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        var inventories = services.GetRequiredService<IInventoryService>();
        var result = await inventories.SearchAsync(new PagedQuery { Page = Page, PageSize = 25 });
        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    }
}
