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

        // Se o banco não estiver acessível, abre a tela de configuração de
        // conexão e tenta novamente até conectar ou o usuário desistir.
        while (true)
        {
            _host = BuildHost();
            Services = _host.Services;
            await _host.StartAsync();

            var error = await InitializeDatabaseAsync();
            if (error is null)
                break;

            await _host.StopAsync();
            _host.Dispose();

            var viewModel = new DatabaseConfigViewModel(ReadConnectionString(),
                $"Não foi possível conectar ao banco de dados.\n{error}");
            var configWindow = new DatabaseConfigWindow { DataContext = viewModel };
            if (configWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        await ApplySavedThemeAsync();
        ShowLogin();
    }

    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureAppConfiguration(config =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false);
                // Configuração salva pela tela de conexão (ProgramData);
                // tem precedência e sobrevive a atualizações do aplicativo.
                config.AddJsonFile(DatabaseConfigViewModel.OverridePath, optional: true);
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
                services.AddTransient<RequisitionsViewModel>();
                services.AddTransient<OrganizationViewModel>();
                services.AddTransient<UsersViewModel>();
                services.AddTransient<RolesViewModel>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<AuditViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<ChangePasswordViewModel>();
            })
            .Build();

    /// <summary>Aplica migrations e seed. Retorna null em sucesso ou a mensagem de erro.</summary>
    private static async Task<string?> InitializeDatabaseAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AlmoxProDbContext>();
            await context.Database.MigrateAsync();
            await DbSeeder.SeedAsync(context, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao inicializar o banco de dados");
            return ex.Message;
        }
    }

    private static string? ReadConnectionString()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(DatabaseConfigViewModel.OverridePath, optional: true)
            .Build();
        return config.GetConnectionString("Default");
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
