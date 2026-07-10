using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ALMOXPRO.UI.ViewModels;

public partial class ExitItemRow : ObservableObject
{
    public int ProductId { get; set; }

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private decimal _quantity = 1;

    [ObservableProperty]
    private decimal _available;
}

public partial class ExitsViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private ExitType _exitType = ExitType.Consumo;

    [ObservableProperty]
    private LookupDto? _selectedWarehouse;

    [ObservableProperty]
    private LookupDto? _selectedCostCenter;

    [ObservableProperty]
    private LookupDto? _selectedEmployee;

    [ObservableProperty]
    private LookupDto? _selectedSector;

    [ObservableProperty]
    private string? _workOrder;

    [ObservableProperty]
    private string? _reason;

    [ObservableProperty]
    private string? _responsibleName;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string _itemCode = string.Empty;

    [ObservableProperty]
    private ProductListDto? _foundProduct;

    [ObservableProperty]
    private decimal _itemQuantity = 1;

    public ObservableCollection<ExitListDto> Items { get; } = [];
    public ObservableCollection<ExitItemRow> EditorItems { get; } = [];
    public ObservableCollection<LookupDto> Warehouses { get; } = [];
    public ObservableCollection<LookupDto> CostCenters { get; } = [];
    public ObservableCollection<LookupDto> Employees { get; } = [];
    public ObservableCollection<LookupDto> Sectors { get; } = [];
    public Array ExitTypes => Enum.GetValues(typeof(ExitType));

    public override string Title => "Saída de Materiais";

    public ExitsViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog)
        : base(scopeFactory, dialog)
    {
    }

    public override async Task LoadAsync()
    {
        await RunAsync(async services =>
        {
            var lookups = services.GetRequiredService<ILookupService>();
            Warehouses.Clear();
            foreach (var item in await lookups.WarehousesAsync()) Warehouses.Add(item);
            CostCenters.Clear();
            foreach (var item in await lookups.CostCentersAsync()) CostCenters.Add(item);
            Employees.Clear();
            foreach (var item in await lookups.EmployeesAsync()) Employees.Add(item);
            Sectors.Clear();
            foreach (var item in await lookups.SectorsAsync()) Sectors.Add(item);

            await LoadIntoAsync(services);
        });
    }

    [RelayCommand]
    private Task SearchExitsAsync() => RunAsync(LoadIntoAsync);

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page < TotalPages) { Page++; await SearchExitsAsync(); }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (Page > 1) { Page--; await SearchExitsAsync(); }
    }

    [RelayCommand]
    private void New()
    {
        ExitType = ExitType.Consumo;
        SelectedWarehouse = Warehouses.FirstOrDefault();
        SelectedCostCenter = null;
        SelectedEmployee = null;
        SelectedSector = null;
        WorkOrder = null;
        Reason = null;
        ResponsibleName = null;
        Notes = null;
        EditorItems.Clear();
        ClearItemInput();
        IsEditorOpen = true;
    }

    [RelayCommand]
    private Task FindProductAsync() => RunAsync(async services =>
    {
        if (string.IsNullOrWhiteSpace(ItemCode))
            return;

        var products = services.GetRequiredService<IProductService>();
        FoundProduct = await products.FindByCodeAsync(ItemCode);
        if (FoundProduct is null)
            Dialog.ShowError($"Nenhum produto encontrado para o código '{ItemCode}'.");
    });

    [RelayCommand]
    private void AddItem()
    {
        if (FoundProduct is null)
        {
            Dialog.ShowError("Localize um produto pelo código antes de adicionar.");
            return;
        }
        if (ItemQuantity <= 0)
        {
            Dialog.ShowError("A quantidade deve ser maior que zero.");
            return;
        }

        EditorItems.Add(new ExitItemRow
        {
            ProductId = FoundProduct.Id,
            ProductName = FoundProduct.Name,
            Quantity = ItemQuantity,
            Available = FoundProduct.CurrentStock
        });
        ClearItemInput();
    }

    [RelayCommand]
    private void RemoveItem(ExitItemRow row) => EditorItems.Remove(row);

    [RelayCommand]
    private Task ConfirmAsync() => RunAsync(async services =>
    {
        if (SelectedWarehouse is null)
        {
            Dialog.ShowError("Selecione o almoxarifado.");
            return;
        }

        var dto = new ExitCreateDto
        {
            Type = ExitType,
            WarehouseId = SelectedWarehouse.Id,
            CostCenterId = SelectedCostCenter?.Id,
            EmployeeId = SelectedEmployee?.Id,
            SectorId = SelectedSector?.Id,
            WorkOrder = WorkOrder,
            Reason = Reason,
            ResponsibleName = ResponsibleName,
            Notes = Notes,
            Items = EditorItems.Select(i => new ExitItemDto
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };

        var exits = services.GetRequiredService<IMaterialExitService>();
        var result = await exits.CreateAsync(dto);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        Dialog.ShowInfo("Saída registrada com sucesso. Estoque atualizado.");
        IsEditorOpen = false;
        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    private void ClearItemInput()
    {
        ItemCode = string.Empty;
        FoundProduct = null;
        ItemQuantity = 1;
    }

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        var exits = services.GetRequiredService<IMaterialExitService>();
        var result = await exits.SearchAsync(new PagedQuery { Page = Page, PageSize = 25, Search = Search });
        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    }
}
