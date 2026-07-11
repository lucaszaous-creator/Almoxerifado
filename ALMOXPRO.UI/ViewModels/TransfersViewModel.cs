using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ALMOXPRO.UI.ViewModels;

public partial class TransfersViewModel : ViewModelBase
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
    private LookupDto? _sourceWarehouse;

    [ObservableProperty]
    private LookupDto? _destinationWarehouse;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string _itemCode = string.Empty;

    [ObservableProperty]
    private ProductListDto? _foundProduct;

    [ObservableProperty]
    private decimal _itemQuantity = 1;

    public ObservableCollection<TransferListDto> Items { get; } = [];
    public ObservableCollection<ExitItemRow> EditorItems { get; } = [];
    public ObservableCollection<LookupDto> Warehouses { get; } = [];

    public override string Title => "Transferências";

    public TransfersViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog)
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

            await LoadIntoAsync(services);
        });
    }

    [RelayCommand]
    private Task SearchTransfersAsync() => RunAsync(LoadIntoAsync);

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page < TotalPages) { Page++; await SearchTransfersAsync(); }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (Page > 1) { Page--; await SearchTransfersAsync(); }
    }

    [RelayCommand]
    private void New()
    {
        SourceWarehouse = Warehouses.FirstOrDefault();
        DestinationWarehouse = null;
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
        if (SourceWarehouse is null || DestinationWarehouse is null)
        {
            Dialog.ShowError("Selecione os almoxarifados de origem e destino.");
            return;
        }

        var dto = new TransferCreateDto
        {
            SourceWarehouseId = SourceWarehouse.Id,
            DestinationWarehouseId = DestinationWarehouse.Id,
            Notes = Notes,
            Items = EditorItems.Select(i => new TransferItemDto
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };

        var transfers = services.GetRequiredService<IStockTransferService>();
        var result = await transfers.CreateAsync(dto);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        Dialog.Notify("Transferência registrada.");
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
        var transfers = services.GetRequiredService<IStockTransferService>();
        var result = await transfers.SearchAsync(new PagedQuery { Page = Page, PageSize = 25, Search = Search });
        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    }
}
