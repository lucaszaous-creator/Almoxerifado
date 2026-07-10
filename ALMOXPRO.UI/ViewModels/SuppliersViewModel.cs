using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ALMOXPRO.UI.ViewModels;

public partial class SuppliersViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private SupplierDto? _selected;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private SupplierUpsertDto _editor = new();

    public ObservableCollection<SupplierDto> Items { get; } = [];
    public string[] States { get; } =
    [
        "AC","AL","AP","AM","BA","CE","DF","ES","GO","MA","MT","MS","MG","PA",
        "PB","PR","PE","PI","RJ","RN","RS","RO","RR","SC","SP","SE","TO"
    ];

    public override string Title => "Fornecedores";

    public SuppliersViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog)
        : base(scopeFactory, dialog)
    {
    }

    public override Task LoadAsync() => SearchSuppliersAsync();

    [RelayCommand]
    private Task SearchSuppliersAsync() => RunAsync(LoadIntoAsync);

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page < TotalPages)
        {
            Page++;
            await SearchSuppliersAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (Page > 1)
        {
            Page--;
            await SearchSuppliersAsync();
        }
    }

    [RelayCommand]
    private void New()
    {
        Editor = new SupplierUpsertDto();
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void Edit()
    {
        if (Selected is null)
            return;
        Editor = new SupplierUpsertDto
        {
            Id = Selected.Id,
            CompanyName = Selected.CompanyName,
            TradeName = Selected.TradeName,
            Cnpj = Selected.Cnpj,
            StateRegistration = Selected.StateRegistration,
            Address = Selected.Address,
            City = Selected.City,
            State = Selected.State,
            Phone = Selected.Phone,
            Email = Selected.Email,
            ContactName = Selected.ContactName,
            Notes = Selected.Notes,
            Status = Selected.Status
        };
        IsEditorOpen = true;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async services =>
    {
        var suppliers = services.GetRequiredService<ISupplierService>();
        var result = await suppliers.SaveAsync(Editor);
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
        if (!Dialog.Confirm($"Excluir o fornecedor '{Selected.CompanyName}'?"))
            return;

        var suppliers = services.GetRequiredService<ISupplierService>();
        var result = await suppliers.DeleteAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }
        await LoadIntoAsync(services);
    });

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        var suppliers = services.GetRequiredService<ISupplierService>();
        var result = await suppliers.SearchAsync(new PagedQuery { Page = Page, PageSize = 25, Search = Search });
        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    }
}
