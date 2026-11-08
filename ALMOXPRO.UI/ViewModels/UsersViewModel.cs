using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Security;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ALMOXPRO.UI.ViewModels;

public partial class RoleOption : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

public partial class UsersViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private UserListDto? _selected;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private UserUpsertDto _editor = new();

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private bool _weekdaysOnly;

    [ObservableProperty]
    private string _accessStart = string.Empty;

    [ObservableProperty]
    private string _accessEnd = string.Empty;

    public ObservableCollection<UserListDto> Items { get; } = [];
    public ObservableCollection<RoleOption> RoleOptions { get; } = [];
    public Array Statuses => Enum.GetValues(typeof(Domain.Common.UserStatus));

    public override string Title => "Usuários";

    public UsersViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog)
        : base(scopeFactory, dialog)
    {
    }

    public override async Task LoadAsync()
    {
        await RunAsync(async services =>
        {
            var lookups = services.GetRequiredService<ILookupService>();
            RoleOptions.Clear();
            foreach (var role in await lookups.RolesAsync())
                RoleOptions.Add(new RoleOption { Id = role.Id, Name = role.Name });

            await LoadIntoAsync(services);
        });
    }

    [RelayCommand]
    private Task SearchUsersAsync() => RunAsync(LoadIntoAsync);

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page < TotalPages) { Page++; await SearchUsersAsync(); }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (Page > 1) { Page--; await SearchUsersAsync(); }
    }

    [RelayCommand]
    private void New()
    {
        Editor = new UserUpsertDto();
        NewPassword = string.Empty;
        WeekdaysOnly = false;
        AccessStart = string.Empty;
        AccessEnd = string.Empty;
        foreach (var role in RoleOptions)
            role.IsSelected = false;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private Task EditAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;

        var users = services.GetRequiredService<IUserService>();
        var dto = await users.GetForEditAsync(Selected.Id);
        if (dto is null)
        {
            Dialog.ShowError("Usuário não encontrado.");
            return;
        }

        Editor = dto;
        NewPassword = string.Empty;
        WeekdaysOnly = dto.WeekdaysOnly;
        AccessStart = dto.AccessStartTime?.ToString("HH\\:mm") ?? string.Empty;
        AccessEnd = dto.AccessEndTime?.ToString("HH\\:mm") ?? string.Empty;
        foreach (var role in RoleOptions)
            role.IsSelected = dto.RoleIds.Contains(role.Id);
        IsEditorOpen = true;
    });

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async services =>
    {
        Editor.Password = string.IsNullOrWhiteSpace(NewPassword) ? null : NewPassword;
        Editor.RoleIds = RoleOptions.Where(r => r.IsSelected).Select(r => r.Id).ToList();
        Editor.WeekdaysOnly = WeekdaysOnly;

        // Horários no formato HH:mm; ambos vazios = sem restrição de horário.
        Editor.AccessStartTime = TimeOnly.TryParse(AccessStart, out var start) ? start : null;
        Editor.AccessEndTime = TimeOnly.TryParse(AccessEnd, out var end) ? end : null;
        if (Editor.AccessStartTime.HasValue != Editor.AccessEndTime.HasValue)
        {
            Dialog.ShowError("Informe o horário inicial e final (ex.: 07:00 e 18:00), ou deixe ambos vazios.");
            return;
        }

        var users = services.GetRequiredService<IUserService>();
        var result = await users.SaveAsync(Editor);
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
    private Task DeactivateAsync() => RunAsync(async services =>
    {
        if (Selected is null)
            return;
        if (!Dialog.Confirm($"Inativar o usuário '{Selected.Name}'?"))
            return;

        var users = services.GetRequiredService<IUserService>();
        var result = await users.DeactivateAsync(Selected.Id);
        if (result.IsFailure)
        {
            Dialog.ShowError(result.Error);
            return;
        }
        await LoadIntoAsync(services);
    });

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        var users = services.GetRequiredService<IUserService>();
        var result = await users.SearchAsync(new PagedQuery { Page = Page, PageSize = 25, Search = Search });
        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    }
}
