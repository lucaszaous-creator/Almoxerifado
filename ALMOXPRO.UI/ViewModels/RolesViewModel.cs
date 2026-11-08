using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Shared.Security;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ALMOXPRO.UI.ViewModels;

public partial class PermissionOption : ObservableObject
{
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

public partial class RolesViewModel : ViewModelBase
{
    [ObservableProperty]
    private RoleDto? _selected;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private int? _editingId;

    [ObservableProperty]
    private string _editingName = string.Empty;

    [ObservableProperty]
    private string? _editingDescription;

    public ObservableCollection<RoleDto> Items { get; } = [];
    public ObservableCollection<PermissionOption> PermissionOptions { get; } = [];

    public override string Title => "Perfis e Permissões";

    public RolesViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog)
        : base(scopeFactory, dialog)
    {
        foreach (var (code, description) in PermissionCodes.All)
            PermissionOptions.Add(new PermissionOption { Code = code, Description = description });
    }

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadIntoAsync);

    [RelayCommand]
    private void New()
    {
        EditingId = null;
        EditingName = string.Empty;
        EditingDescription = null;
        foreach (var option in PermissionOptions)
            option.IsSelected = false;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void Edit()
    {
        if (Selected is null)
            return;

        EditingId = Selected.Id;
        EditingName = Selected.Name;
        EditingDescription = Selected.Description;
        foreach (var option in PermissionOptions)
            option.IsSelected = Selected.PermissionCodes.Contains(option.Code);
        IsEditorOpen = true;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async services =>
    {
        var roles = services.GetRequiredService<IRoleService>();
        var result = await roles.SaveAsync(new RoleUpsertDto
        {
            Id = EditingId,
            Name = EditingName,
            Description = EditingDescription,
            PermissionCodes = PermissionOptions.Where(p => p.IsSelected).Select(p => p.Code).ToList()
        });

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
        if (!Dialog.Confirm($"Excluir o perfil '{Selected.Name}'?"))
            return;

        var roles = services.GetRequiredService<IRoleService>();
        var result = await roles.DeleteAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }
        await LoadIntoAsync(services);
    });

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        var roles = services.GetRequiredService<IRoleService>();
        Items.Clear();
        foreach (var role in await roles.GetAllAsync())
            Items.Add(role);
    }
}
