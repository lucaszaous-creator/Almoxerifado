using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Security;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace ALMOXPRO.UI.ViewModels;

public partial class RequisitionItemRow : ObservableObject
{
    public int ProductId { get; set; }

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private decimal _quantity = 1;

    [ObservableProperty]
    private decimal _available;
}

/// <summary>Linha do painel de atendimento: quanto entregar agora de cada item.</summary>
public partial class FulfillItemRow : ObservableObject
{
    public int ItemId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal Requested { get; init; }
    public decimal AlreadyFulfilled { get; init; }
    public decimal Remaining => Requested - AlreadyFulfilled;

    [ObservableProperty]
    private decimal _deliverNow;
}

public partial class RequisitionsViewModel : ViewModelBase
{
    private readonly ISessionService _session;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private RequisitionListDto? _selected;

    [ObservableProperty]
    private string _statusFilter = "Todas";

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private bool _isFulfillOpen;

    // Nova requisição
    [ObservableProperty]
    private LookupDto? _selectedWarehouse;

    [ObservableProperty]
    private LookupDto? _selectedSector;

    [ObservableProperty]
    private LookupDto? _selectedEmployee;

    [ObservableProperty]
    private string? _requesterName;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string _itemCode = string.Empty;

    [ObservableProperty]
    private ProductListDto? _foundProduct;

    [ObservableProperty]
    private decimal _itemQuantity = 1;

    public ObservableCollection<RequisitionListDto> Items { get; } = [];
    public ObservableCollection<RequisitionItemViewDto> SelectedItems { get; } = [];
    public ObservableCollection<RequisitionItemRow> EditorItems { get; } = [];
    public ObservableCollection<FulfillItemRow> FulfillItems { get; } = [];
    public ObservableCollection<LookupDto> Warehouses { get; } = [];
    public ObservableCollection<LookupDto> Sectors { get; } = [];
    public ObservableCollection<LookupDto> Employees { get; } = [];
    public string[] StatusFilters { get; } = ["Todas", "Pendente", "Parcial", "Atendida", "Cancelada"];

    public bool CanCreate => _session.HasPermission(PermissionCodes.RequisitionsCreate);
    public bool CanFulfill => _session.HasPermission(PermissionCodes.RequisitionsFulfill);

    public override string Title => "Requisições";

    public RequisitionsViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog, ISessionService session)
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
            foreach (var item in await lookups.WarehousesAsync()) Warehouses.Add(item);
            Sectors.Clear();
            foreach (var item in await lookups.SectorsAsync()) Sectors.Add(item);
            Employees.Clear();
            foreach (var item in await lookups.EmployeesAsync()) Employees.Add(item);

            await LoadIntoAsync(services);
        });
    }

    partial void OnSelectedChanged(RequisitionListDto? value)
    {
        _ = RunAsync(async services =>
        {
            SelectedItems.Clear();
            if (value is null)
                return;
            var requisitions = services.GetRequiredService<IRequisitionService>();
            foreach (var item in await requisitions.GetItemsAsync(value.Id))
                SelectedItems.Add(item);
        });
    }

    [RelayCommand]
    private Task SearchRequisitionsAsync() => RunAsync(LoadIntoAsync);

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page < TotalPages) { Page++; await SearchRequisitionsAsync(); }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (Page > 1) { Page--; await SearchRequisitionsAsync(); }
    }

    [RelayCommand]
    private void New()
    {
        SelectedWarehouse = Warehouses.FirstOrDefault();
        SelectedSector = null;
        SelectedEmployee = null;
        RequesterName = null;
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

        EditorItems.Add(new RequisitionItemRow
        {
            ProductId = FoundProduct.Id,
            ProductName = FoundProduct.Name,
            Quantity = ItemQuantity,
            Available = FoundProduct.CurrentStock
        });
        ClearItemInput();
    }

    [RelayCommand]
    private void RemoveItem(RequisitionItemRow row) => EditorItems.Remove(row);

    [RelayCommand]
    private Task ConfirmAsync() => RunAsync(async services =>
    {
        if (SelectedWarehouse is null || SelectedSector is null)
        {
            Dialog.ShowError("Selecione o almoxarifado e o setor solicitante.");
            return;
        }

        var dto = new RequisitionCreateDto
        {
            WarehouseId = SelectedWarehouse.Id,
            SectorId = SelectedSector.Id,
            EmployeeId = SelectedEmployee?.Id,
            RequesterName = RequesterName,
            Notes = Notes,
            Items = EditorItems.Select(i => new RequisitionItemCreateDto
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };

        var requisitions = services.GetRequiredService<IRequisitionService>();
        var result = await requisitions.CreateAsync(dto);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        Dialog.Notify("Requisição registrada. Imprima o documento para colher a assinatura.");
        IsEditorOpen = false;
        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private Task FulfillAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;
        if (Selected.Status is not (RequisitionStatus.Pendente or RequisitionStatus.AtendidaParcial))
        {
            Dialog.ShowError("Apenas requisições pendentes ou parciais podem ser atendidas.");
            return;
        }

        // Abre o painel de atendimento com o saldo pendente de cada item,
        // permitindo ajustar as quantidades para entrega parcial.
        var requisitions = services.GetRequiredService<IRequisitionService>();
        FulfillItems.Clear();
        foreach (var item in await requisitions.GetItemsAsync(Selected.Id))
        {
            var row = new FulfillItemRow
            {
                ItemId = item.Id,
                ProductName = item.ProductName,
                Requested = item.QuantityRequested,
                AlreadyFulfilled = item.QuantityFulfilled
            };
            row.DeliverNow = row.Remaining;
            FulfillItems.Add(row);
        }
        IsFulfillOpen = true;
    });

    [RelayCommand]
    private void CloseFulfill() => IsFulfillOpen = false;

    [RelayCommand]
    private Task ConfirmFulfillAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;

        var quantities = FulfillItems.ToDictionary(i => i.ItemId, i => i.DeliverNow);
        var partial = FulfillItems.Any(i => i.DeliverNow < i.Remaining);

        if (!Dialog.Confirm(partial
                ? $"Registrar entrega PARCIAL da requisição {Selected.Number}?\nO saldo não entregue continua pendente."
                : $"Atender a requisição {Selected.Number}?\nA saída de estoque será gerada automaticamente."))
            return;

        var requisitions = services.GetRequiredService<IRequisitionService>();
        var result = await requisitions.FulfillAsync(Selected.Id, quantities);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        Dialog.Notify(partial
            ? "Entrega parcial registrada. O restante segue pendente."
            : "Requisição atendida. Saída de estoque gerada e vinculada.");
        IsFulfillOpen = false;
        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private Task CancelRequisitionAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;
        if (!Dialog.Confirm($"Cancelar a requisição {Selected.Number}?"))
            return;

        var requisitions = services.GetRequiredService<IRequisitionService>();
        var result = await requisitions.CancelAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }
        await LoadIntoAsync(services);
    });

    [RelayCommand]
    private Task PrintAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;

        var requisitions = services.GetRequiredService<IRequisitionService>();
        var document = await requisitions.GetPrintDocumentAsync(Selected.Id);
        if (document is null)
        {
            Dialog.ShowError("Requisição não encontrada.");
            return;
        }

        var generator = services.GetRequiredService<IRequisitionDocumentGenerator>();
        var pdf = generator.GeneratePdf(document);

        var path = Dialog.SaveFile($"requisicao_{Selected.Number}.pdf", "PDF (*.pdf)|*.pdf");
        if (path is null)
            return;

        await File.WriteAllBytesAsync(path, pdf);

        // Abre o PDF no visualizador padrão para impressão imediata.
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    });

    private void ClearItemInput()
    {
        ItemCode = string.Empty;
        FoundProduct = null;
        ItemQuantity = 1;
    }

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        RequisitionStatus? status = StatusFilter switch
        {
            "Pendente" => RequisitionStatus.Pendente,
            "Parcial" => RequisitionStatus.AtendidaParcial,
            "Atendida" => RequisitionStatus.Atendida,
            "Cancelada" => RequisitionStatus.Cancelada,
            _ => null
        };

        var requisitions = services.GetRequiredService<IRequisitionService>();
        var result = await requisitions.SearchAsync(
            new PagedQuery { Page = Page, PageSize = 25, Search = Search }, status);
        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    }
}
