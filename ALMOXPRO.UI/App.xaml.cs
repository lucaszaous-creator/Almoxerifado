using ALMOXPRO.Application;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Infrastructure;
using ALMOXPRO.Persistence;
using ALMOXPRO.Persistence.Context;
using ALMOXPRO.Persistence.Seed;
using ALMOXPRO.UI.Services;
using ALMOXPRO.UI.ViewModels;
using ALMOXPRO.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ALMOXPRO.UI;

public partial class App : System.Windows.Application
{
    private IHost _host = null!;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ALMOXPRO", "Logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(logDirectory, "almoxpro-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Erro não tratado no domínio da aplicação");

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureAppConfiguration(config =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false);
            })
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("Default")
                    ?? throw new InvalidOperationException("Connection string 'Default' não configurada.");

                services.AddApplication();
                services.AddPersistence(connectionString);
                services.AddInfrastructure(connectionString);

                services.AddSingleton<ISessionService, SessionService>();
                services.AddSingleton<ICurrentSession>(provider => provider.GetRequiredService<ISessionService>());
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<IDialogService, DialogService>();

                services.AddTransient<LoginViewModel>();
                services.AddTransient<MainViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<ProductsViewModel>();
                services.AddTransient<CategoriesViewModel>();
                services.AddTransient<SuppliersViewModel>();
                services.AddTransient<LocationsViewModel>();
                services.AddTransient<EntriesViewModel>();
                services.AddTransient<ExitsViewModel>();
                services.AddTransient<TransfersViewModel>();
                services.AddTransient<InventoryViewModel>();
                services.AddTransient<UsersViewModel>();
                services.AddTransient<RolesViewModel>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<AuditViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<ChangePasswordViewModel>();
            })
            .Build();

        Services = _host.Services;
        await _host.StartAsync();

        if (!await InitializeDatabaseAsync())
        {
            Shutdown();
            return;
        }

        await ApplySavedThemeAsync();
        ShowLogin();
    }

    private async Task<bool> InitializeDatabaseAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AlmoxProDbContext>();
            await context.Database.MigrateAsync();
            await DbSeeder.SeedAsync(context, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
            return true;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Falha ao inicializar o banco de dados");
            MessageBox.Show(
                "Não foi possível conectar ao banco de dados PostgreSQL.\n\n" +
                "Verifique a connection string em appsettings.json e se o serviço está em execução.\n\n" +
                $"Detalhes: {ex.Message}",
                "ALMOX PRO", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private async Task ApplySavedThemeAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var theme = await settings.GetAsync(SettingKeys.Theme);
            Services.GetRequiredService<IThemeService>().Apply(theme == "dark");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível carregar o tema salvo");
        }
    }

    public void ShowLogin()
    {
        var login = new LoginWindow { DataContext = Services.GetRequiredService<LoginViewModel>() };
        login.Show();
    }

    public void ShowMain()
    {
        var main = new MainWindow { DataContext = Services.GetRequiredService<MainViewModel>() };
        MainWindow = main;
        main.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Erro não tratado na interface");
        MessageBox.Show(
            $"Ocorreu um erro inesperado:\n\n{e.Exception.Message}",
            "ALMOX PRO", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
