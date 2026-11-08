using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Services;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ALMOXPRO.UI.ViewModels;

public partial class CategoriesViewModel : ViewModelBase
{
    [ObservableProperty]
    private CategoryDto? _selected;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private CategoryUpsertDto _editor = new();

    public ObservableCollection<CategoryDto> Items { get; } = [];
    public ObservableCollection<LookupDto> ParentOptions { get; } = [];

    public override string Title => "Categorias";

    public CategoriesViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog)
        : base(scopeFactory, dialog)
    {
    }

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async services =>
    {
        var categories = services.GetRequiredService<ICategoryService>();
        var all = await categories.GetAllAsync();

        Items.Clear();
        ParentOptions.Clear();
        foreach (var category in all)
        {
            Items.Add(category);
            if (category.ParentId is null)
                ParentOptions.Add(new LookupDto(category.Id, category.Name));
        }
    });

    [RelayCommand]
    private void New()
    {
        Editor = new CategoryUpsertDto();
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void Edit()
    {
        if (Selected is null)
            return;
        Editor = new CategoryUpsertDto
        {
            Id = Selected.Id,
            Name = Selected.Name,
            Description = Selected.Description,
            ParentId = Selected.ParentId,
            Status = Selected.Status
        };
        IsEditorOpen = true;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async services =>
    {
        var categories = services.GetRequiredService<ICategoryService>();
        var result = await categories.SaveAsync(Editor);
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
        if (!Dialog.Confirm($"Excluir a categoria '{Selected.Name}'?"))
            return;

        var categories = services.GetRequiredService<ICategoryService>();
        var result = await categories.DeleteAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }
        await LoadIntoAsync(services);
    });

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        var categories = services.GetRequiredService<ICategoryService>();
        var all = await categories.GetAllAsync();
        Items.Clear();
        ParentOptions.Clear();
        foreach (var category in all)
        {
            Items.Add(category);
            if (category.ParentId is null)
                ParentOptions.Add(new LookupDto(category.Id, category.Name));
        }
    }
}
