using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Shared.Security;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;

namespace ALMOXPRO.UI.ViewModels;

public record NavItem(string Label, string Icon, string PermissionCode, Type ViewModelType);

public partial class MainViewModel : ViewModelBase
{
    private readonly ISessionService _session;
    private readonly IThemeService _theme;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    [ObservableProperty]
    private string _breadcrumb = "Início";

    [ObservableProperty]
    private bool _isDarkTheme;

    public string UserName => _session.Current?.Name ?? string.Empty;
    public string UserLogin => _session.Current?.Login ?? string.Empty;

    public ObservableCollection<NavItem> NavItems { get; } = [];

    public override string Title => "ALMOX PRO";

    public MainViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog,
        ISessionService session, IThemeService theme)
        : base(scopeFactory, dialog)
    {
        _session = session;
        _theme = theme;
        IsDarkTheme = theme.IsDark;
        BuildNavigation();

        var first = NavItems.FirstOrDefault();
        if (first is not null)
            SelectedNavItem = first;
    }

    private void BuildNavigation()
    {
        var all = new List<NavItem>
        {
            new("Dashboard", "ViewDashboard", PermissionCodes.DashboardView, typeof(DashboardViewModel)),
            new("Produtos", "PackageVariantClosed", PermissionCodes.ProductsView, typeof(ProductsViewModel)),
            new("Categorias", "Shape", PermissionCodes.CategoriesView, typeof(CategoriesViewModel)),
            new("Fornecedores", "TruckDelivery", PermissionCodes.SuppliersView, typeof(SuppliersViewModel)),
            new("Localizações", "MapMarker", PermissionCodes.LocationsView, typeof(LocationsViewModel)),
            new("Entradas", "TrayArrowDown", PermissionCodes.StockEntry, typeof(EntriesViewModel)),
            new("Saídas", "TrayArrowUp", PermissionCodes.StockExit, typeof(ExitsViewModel)),
            new("Requisições", "ClipboardTextOutline", PermissionCodes.RequisitionsView, typeof(RequisitionsViewModel)),
            new("Transferências", "SwapHorizontal", PermissionCodes.StockTransfer, typeof(TransfersViewModel)),
            new("Inventário", "ClipboardListOutline", PermissionCodes.InventoryView, typeof(InventoryViewModel)),
            new("Relatórios", "ChartBox", PermissionCodes.ReportsView, typeof(ReportsViewModel)),
            new("Setores e Equipe", "AccountTie", PermissionCodes.OrganizationView, typeof(OrganizationViewModel)),
            new("Usuários", "AccountGroup", PermissionCodes.UsersView, typeof(UsersViewModel)),
            new("Permissões", "ShieldAccount", PermissionCodes.RolesManage, typeof(RolesViewModel)),
            new("Auditoria", "History", PermissionCodes.AuditView, typeof(AuditViewModel)),
            new("Configurações", "Cog", PermissionCodes.SettingsView, typeof(SettingsViewModel)),
        };

        foreach (var item in all.Where(i => _session.HasPermission(i.PermissionCode)))
            NavItems.Add(item);
    }

    async partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is null)
            return;

        if (!_session.HasPermission(value.PermissionCode))
        {
            Dialog.ShowError("Você não possui permissão para acessar esta tela.");
            return;
        }

        var viewModel = (ViewModelBase)App.Services.GetRequiredService(value.ViewModelType);
        CurrentViewModel = viewModel;
        Breadcrumb = $"Início  ›  {value.Label}";
        await viewModel.LoadAsync();
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        _theme.Toggle();
        IsDarkTheme = _theme.IsDark;
        await RunAsync(async services =>
        {
            var settings = services.GetRequiredService<ISettingsService>();
            await settings.SetAsync(SettingKeys.Theme, _theme.IsDark ? "dark" : "light");
        });
    }

    [RelayCommand]
    private void ChangePassword()
    {
        var viewModel = App.Services.GetRequiredService<ChangePasswordViewModel>();
        var window = new Views.ChangePasswordWindow
        {
            DataContext = viewModel,
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private async Task LogoffAsync(Window window)
    {
        if (!Dialog.Confirm("Deseja encerrar a sessão?"))
            return;

        await RunAsync(async services =>
        {
            var auth = services.GetRequiredService<IAuthService>();
            await auth.LogoffAsync(_session.UserId ?? 0);
        });

        _session.End();
        ((App)System.Windows.Application.Current).ShowLogin();
        window.Close();
    }
}
