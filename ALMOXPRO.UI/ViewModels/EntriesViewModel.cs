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

public partial class EntryItemRow : ObservableObject
{
    public int ProductId { get; set; }

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private decimal _quantity = 1;

    [ObservableProperty]
    private decimal _unitCost;

    [ObservableProperty]
    private string? _lotNumber;

    [ObservableProperty]
    private DateTime? _expirationDate;

    public decimal Total => Math.Round(Quantity * UnitCost, 2);
}

public partial class EntriesViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private bool _isEditorOpen;

    // Cabeçalho da nova entrada
    [ObservableProperty]
    private EntryType _entryType = EntryType.NotaFiscal;

    [ObservableProperty]
    private LookupDto? _selectedWarehouse;

    [ObservableProperty]
    private LookupDto? _selectedSupplier;

    [ObservableProperty]
    private string? _documentNumber;

    [ObservableProperty]
    private string? _notes;

    // Item em digitação
    [ObservableProperty]
    private string _itemCode = string.Empty;

    [ObservableProperty]
    private ProductListDto? _foundProduct;

    [ObservableProperty]
    private decimal _itemQuantity = 1;

    [ObservableProperty]
    private decimal _itemUnitCost;

    [ObservableProperty]
    private string? _itemLot;

    [ObservableProperty]
    private DateTime? _itemExpiration;

    public ObservableCollection<EntryListDto> Items { get; } = [];
    public ObservableCollection<EntryItemRow> EditorItems { get; } = [];
    public ObservableCollection<LookupDto> Warehouses { get; } = [];
    public ObservableCollection<LookupDto> Suppliers { get; } = [];
    public Array EntryTypes => Enum.GetValues(typeof(EntryType));

    public decimal EditorTotal => EditorItems.Sum(i => i.Total);

    public override string Title => "Entrada de Materiais";

    public EntriesViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog)
        : base(scopeFactory, dialog)
    {
    }

    public override async Task LoadAsync()
    {
        await RunAsync(async services =>
        {
            var lookups = services.GetRequiredService<ILookupService>();
            Warehouses.Clear();
            foreach (var warehouse in await lookups.WarehousesAsync())
                Warehouses.Add(warehouse);
            Suppliers.Clear();
            foreach (var supplier in await lookups.SuppliersAsync())
                Suppliers.Add(supplier);

            await LoadIntoAsync(services);
        });
    }

    [RelayCommand]
    private Task SearchEntriesAsync() => RunAsync(LoadIntoAsync);

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page < TotalPages) { Page++; await SearchEntriesAsync(); }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (Page > 1) { Page--; await SearchEntriesAsync(); }
    }

    [RelayCommand]
    private void New()
    {
        EntryType = EntryType.NotaFiscal;
        SelectedWarehouse = Warehouses.FirstOrDefault();
        SelectedSupplier = null;
        DocumentNumber = null;
        Notes = null;
        EditorItems.Clear();
        ClearItemInput();
        IsEditorOpen = true;
        OnPropertyChanged(nameof(EditorTotal));
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
        else
            ItemUnitCost = FoundProduct.AverageCost;
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

        EditorItems.Add(new EntryItemRow
        {
            ProductId = FoundProduct.Id,
            ProductName = FoundProduct.Name,
            Quantity = ItemQuantity,
            UnitCost = ItemUnitCost,
            LotNumber = string.IsNullOrWhiteSpace(ItemLot) ? null : ItemLot,
            ExpirationDate = ItemExpiration
        });
        ClearItemInput();
        OnPropertyChanged(nameof(EditorTotal));
    }

    [RelayCommand]
    private void RemoveItem(EntryItemRow row)
    {
        EditorItems.Remove(row);
        OnPropertyChanged(nameof(EditorTotal));
    }

    [RelayCommand]
    private Task ConfirmAsync() => RunAsync(async services =>
    {
        if (SelectedWarehouse is null)
        {
            Dialog.ShowError("Selecione o almoxarifado.");
            return;
        }

        var dto = new EntryCreateDto
        {
            Type = EntryType,
            WarehouseId = SelectedWarehouse.Id,
            SupplierId = SelectedSupplier?.Id,
            DocumentNumber = DocumentNumber,
            Notes = Notes,
            Items = EditorItems.Select(i => new EntryItemDto
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost,
                LotNumber = i.LotNumber,
                ExpirationDate = i.ExpirationDate.HasValue ? DateOnly.FromDateTime(i.ExpirationDate.Value) : null
            }).ToList()
        };

        var entries = services.GetRequiredService<IMaterialEntryService>();
        var result = await entries.CreateAsync(dto);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        Dialog.Notify("Entrada registrada. Estoque atualizado.");
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
        ItemUnitCost = 0;
        ItemLot = null;
        ItemExpiration = null;
    }

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        var entries = services.GetRequiredService<IMaterialEntryService>();
        var result = await entries.SearchAsync(new PagedQuery { Page = Page, PageSize = 25, Search = Search });
        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    }
}
