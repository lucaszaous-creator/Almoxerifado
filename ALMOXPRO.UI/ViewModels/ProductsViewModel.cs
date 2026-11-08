using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Security;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.IO;

namespace ALMOXPRO.UI.ViewModels;

public partial class ProductsViewModel : ViewModelBase
{
    private readonly ISessionService _session;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private ProductListDto? _selected;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private ProductUpsertDto _editor = new();

    public ObservableCollection<ProductListDto> Items { get; } = [];
    public ObservableCollection<LookupDto> Categories { get; } = [];
    public ObservableCollection<LookupDto> Suppliers { get; } = [];
    public ObservableCollection<LocationDto> Locations { get; } = [];
    public string[] Units { get; } = ["UN", "CX", "PC", "KG", "G", "L", "ML", "M", "M2", "M3", "PAR", "RL"];

    public bool CanCreate => _session.HasPermission(PermissionCodes.ProductsCreate);
    public bool CanEdit => _session.HasPermission(PermissionCodes.ProductsEdit);
    public bool CanDelete => _session.HasPermission(PermissionCodes.ProductsDelete);
    public bool CanViewCosts => _session.HasPermission(PermissionCodes.ProductsViewCosts);

    public override string Title => "Produtos";

    public ProductsViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog, ISessionService session)
        : base(scopeFactory, dialog)
    {
        _session = session;
    }

    public override async Task LoadAsync()
    {
        await RunAsync(async services =>
        {
            var lookups = services.GetRequiredService<ILookupService>();
            var locations = services.GetRequiredService<ILocationService>();

            Categories.Clear();
            foreach (var category in await lookups.CategoriesAsync())
                Categories.Add(category);

            Suppliers.Clear();
            foreach (var supplier in await lookups.SuppliersAsync())
                Suppliers.Add(supplier);

            Locations.Clear();
            foreach (var location in await locations.GetAllAsync())
                Locations.Add(location);
        });
        await SearchProductsAsync();
    }

    [RelayCommand]
    private Task SearchProductsAsync() => RunAsync(async services =>
    {
        var products = services.GetRequiredService<IProductService>();
        var result = await products.SearchAsync(new PagedQuery { Page = Page, PageSize = 25, Search = Search });
        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    });

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page < TotalPages)
        {
            Page++;
            await SearchProductsAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (Page > 1)
        {
            Page--;
            await SearchProductsAsync();
        }
    }

    [RelayCommand]
    private Task NewAsync() => RunAsync(async services =>
    {
        var products = services.GetRequiredService<IProductService>();
        Editor = new ProductUpsertDto { InternalCode = await products.SuggestInternalCodeAsync() };
        IsEditorOpen = true;
    });

    [RelayCommand]
    private Task EditAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;
        var products = services.GetRequiredService<IProductService>();
        var dto = await products.GetForEditAsync(Selected.Id);
        if (dto is null)
        {
            Dialog.ShowError("Produto não encontrado.");
            return;
        }
        Editor = dto;
        IsEditorOpen = true;
    });

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async services =>
    {
        var products = services.GetRequiredService<IProductService>();
        var result = await products.SaveAsync(Editor);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }
        IsEditorOpen = false;
        await SearchProductsInternalAsync(services);
    });

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private void SelectPhoto()
    {
        var path = Dialog.OpenFile("Imagens (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp");
        if (path is null)
            return;

        var info = new FileInfo(path);
        if (info.Length > 2 * 1024 * 1024)
        {
            Dialog.ShowError("A foto deve ter no máximo 2 MB.");
            return;
        }

        Editor.Photo = File.ReadAllBytes(path);
        OnPropertyChanged(nameof(Editor));
    }

    [RelayCommand]
    private void RemovePhoto()
    {
        Editor.Photo = null;
        OnPropertyChanged(nameof(Editor));
    }

    /// <summary>Usado pela busca global da barra superior.</summary>
    public async Task SearchForAsync(string term)
    {
        Search = term;
        Page = 1;
        await SearchProductsAsync();
    }

    [RelayCommand]
    private Task DeleteAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;
        if (!Dialog.Confirm($"Excluir o produto '{Selected.Name}'?"))
            return;

        var products = services.GetRequiredService<IProductService>();
        var result = await products.DeleteAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }
        await SearchProductsInternalAsync(services);
    });

    [RelayCommand]
    private Task PrintLabelAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;

        var labels = services.GetRequiredService<ILabelGenerator>();
        var pdf = labels.ProductLabelPdf(
            Selected.Name, Selected.InternalCode, Selected.Barcode,
            $"ALMOXPRO:PRODUCT:{Selected.InternalCode}");

        var path = Dialog.SaveFile($"etiqueta_{Selected.InternalCode}.pdf", "PDF (*.pdf)|*.pdf");
        if (path is null)
            return;

        await File.WriteAllBytesAsync(path, pdf);
        Dialog.ShowInfo($"Etiqueta gerada em:\n{path}");
    });

    private async Task SearchProductsInternalAsync(IServiceProvider services)
    {
        var products = services.GetRequiredService<IProductService>();
        var result = await products.SearchAsync(new PagedQuery { Page = Page, PageSize = 25, Search = Search });
        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    }
}
