using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ALMOXPRO.UI.ViewModels;

public partial class AuditViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private DateTime? _from;

    [ObservableProperty]
    private DateTime? _to;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private AuditLogDto? _selected;

    public ObservableCollection<AuditLogDto> Items { get; } = [];

    public override string Title => "Auditoria";

    public AuditViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog)
        : base(scopeFactory, dialog)
    {
    }

    public override Task LoadAsync() => SearchLogsAsync();

    [RelayCommand]
    private Task SearchLogsAsync() => RunAsync(async services =>
    {
        var audit = services.GetRequiredService<IAuditService>();
        var result = await audit.SearchAsync(
            new PagedQuery { Page = Page, PageSize = 50, Search = Search }, From, To, null);

        TotalPages = result.TotalPages;
        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(item);
    });

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page < TotalPages) { Page++; await SearchLogsAsync(); }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (Page > 1) { Page--; await SearchLogsAsync(); }
    }
}
