using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Shared.Security;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

    [ObservableProperty]
    private UpdateInfo? _availableUpdate;

    [ObservableProperty]
    private bool _isUpdating;

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

        _ = CheckForUpdatesAsync();
        _ = StartSessionMonitorAsync();

        // Navegação disparada por outras telas (cartões do dashboard).
        WeakReferenceMessenger.Default.Register<OpenScreenMessage>(this,
            (_, message) => NavigateTo(message.ViewModelType));
        WeakReferenceMessenger.Default.Register<OpenReportMessage>(this, (_, message) =>
        {
            NavigateTo(typeof(ReportsViewModel));
            if (CurrentViewModel is ReportsViewModel reports)
                _ = reports.OpenReportAsync(message.Kind);
        });
    }

    private void NavigateTo(Type viewModelType)
    {
        var item = NavItems.FirstOrDefault(n => n.ViewModelType == viewModelType);
        if (item is not null)
            SelectedNavItem = item;
    }

    // ---- Busca global (barra superior) ----

    [ObservableProperty]
    private string _globalSearch = string.Empty;

    [RelayCommand]
    private async Task GlobalSearchGoAsync()
    {
        var term = GlobalSearch.Trim();
        if (term.Length == 0)
            return;

        NavigateTo(typeof(ProductsViewModel));
        if (CurrentViewModel is ProductsViewModel products)
            await products.SearchForAsync(term);
        GlobalSearch = string.Empty;
    }

    // ---- Monitor de sessão: bloqueio por inatividade e backup automático ----

    private DateTime _lastActivity = DateTime.Now;
    private int _sessionTimeoutMinutes;
    private System.Windows.Threading.DispatcherTimer? _monitorTimer;

    /// <summary>Chamado pela janela a cada interação do usuário (teclado/mouse).</summary>
    public void RegisterActivity() => _lastActivity = DateTime.Now;

    private async Task StartSessionMonitorAsync()
    {
        try
        {
            using var scope = ScopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var raw = await settings.GetAsync(SettingKeys.SessionTimeoutMinutes);
            _sessionTimeoutMinutes = int.TryParse(raw, out var minutes) ? minutes : 15;
        }
        catch (Exception)
        {
            _sessionTimeoutMinutes = 15;
        }

        _monitorTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _monitorTimer.Tick += async (_, _) => await OnSessionTickAsync();
        _monitorTimer.Start();
    }

    private async Task OnSessionTickAsync()
    {
        // Bloqueio por inatividade (0 = desativado).
        if (_sessionTimeoutMinutes > 0 &&
            (DateTime.Now - _lastActivity).TotalMinutes >= _sessionTimeoutMinutes)
        {
            await ForceLogoffAsync();
            return;
        }

        await RunAutoBackupIfDueAsync();
    }

    private async Task ForceLogoffAsync()
    {
        _monitorTimer?.Stop();
        try
        {
            using var scope = ScopeFactory.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await auth.LogoffAsync(_session.UserId ?? 0);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Falha ao registrar logoff por inatividade");
        }

        _session.End();
        var window = System.Windows.Application.Current.MainWindow;
        ((App)System.Windows.Application.Current).ShowLogin();
        window?.Close();
    }

    /// <summary>Backup automático diário, executado em segundo plano enquanto o sistema é usado.</summary>
    private async Task RunAutoBackupIfDueAsync()
    {
        try
        {
            using var scope = ScopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            if (await settings.GetAsync(SettingKeys.BackupAutoEnabled) != "true")
                return;

            var lastRaw = await settings.GetAsync(SettingKeys.BackupLastAutoAt);
            if (DateTime.TryParse(lastRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var last)
                && DateTime.UtcNow - last < TimeSpan.FromHours(24))
                return;

            var directory = await settings.GetAsync(SettingKeys.BackupDirectory);
            var backup = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IBackupService>();
            var path = await backup.BackupAsync(string.IsNullOrWhiteSpace(directory) ? null : directory, true);
            await settings.SetAsync(SettingKeys.BackupLastAutoAt, DateTime.UtcNow.ToString("O"));
            Serilog.Log.Information("Backup automático gerado em {Path}", path);
        }
        catch (Exception ex)
        {
            // Nunca interrompe o uso; a falha fica registrada para diagnóstico.
            Serilog.Log.Warning(ex, "Falha no backup automático");
        }
    }

    /// <summary>Verificação silenciosa em segundo plano; nunca interrompe o uso.</summary>
    private async Task CheckForUpdatesAsync()
    {
        var updates = App.Services.GetService<IUpdateService>();
        if (updates is null)
            return;

        try
        {
            AvailableUpdate = await updates.CheckForUpdateAsync();
        }
        catch (Exception)
        {
            // Sem rede: o botão simplesmente não aparece.
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (AvailableUpdate is null || IsUpdating)
            return;

        if (!Dialog.Confirm(
                $"Baixar e instalar a versão {AvailableUpdate.TagName}?\n\n" +
                "O aplicativo será fechado para concluir a atualização. " +
                "Suas configurações e o banco de dados são preservados.",
                "Atualização do ALMOX PRO"))
            return;

        IsUpdating = true;
        try
        {
            var updates = App.Services.GetRequiredService<IUpdateService>();
            var installerPath = await updates.DownloadInstallerAsync(AvailableUpdate);

            // Instalação silenciosa do Inno Setup; o app reabre ao final.
            Process.Start(new ProcessStartInfo(installerPath)
            {
                UseShellExecute = true,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS"
            });
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            IsUpdating = false;
            Dialog.ShowError($"Não foi possível baixar a atualização:\n{ex.Message}");
        }
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
            new("Notas Fiscais", "FileDocumentOutline", PermissionCodes.FiscalView, typeof(FiscalViewModel)),
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
