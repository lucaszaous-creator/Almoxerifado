using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Shared.Security;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ALMOXPRO.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISessionService _session;

    // Empresa
    [ObservableProperty]
    private string _companyName = string.Empty;

    [ObservableProperty]
    private string _companyCnpj = string.Empty;

    [ObservableProperty]
    private string _logoPath = string.Empty;

    // Backup
    [ObservableProperty]
    private string _backupDirectory = string.Empty;

    [ObservableProperty]
    private bool _backupCompress = true;

    [ObservableProperty]
    private bool _backupAutoEnabled;

    // Sessão
    [ObservableProperty]
    private string _sessionTimeoutMinutes = "15";

    [ObservableProperty]
    private string _pgDumpPath = string.Empty;

    [ObservableProperty]
    private string _pgRestorePath = string.Empty;

    // Fiscal (NF-e)
    [ObservableProperty]
    private string _fiscalCnpj = string.Empty;

    [ObservableProperty]
    private string _fiscalUf = string.Empty;

    [ObservableProperty]
    private bool _fiscalProduction = true;

    [ObservableProperty]
    private string _fiscalCertPassword = string.Empty;

    [ObservableProperty]
    private string _fiscalCertFileName = string.Empty;

    [ObservableProperty]
    private string _fiscalCertStatus = string.Empty;

    /// <summary>Modo demonstração: simula a SEFAZ com notas de exemplo, sem certificado.</summary>
    [ObservableProperty]
    private bool _fiscalDemoMode;

    private byte[]? _fiscalCertBytes;

    public string[] Ufs { get; } =
    [
        "AC","AL","AP","AM","BA","CE","DF","ES","GO","MA","MT","MS","MG","PA",
        "PB","PR","PE","PI","RJ","RN","RS","RO","RR","SC","SP","SE","TO"
    ];

    // Almoxarifados
    [ObservableProperty]
    private WarehouseDto? _selectedWarehouse;

    [ObservableProperty]
    private string _warehouseCode = string.Empty;

    [ObservableProperty]
    private string _warehouseName = string.Empty;

    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];

    public bool CanManage => _session.HasPermission(PermissionCodes.SettingsManage);
    public bool CanBackup => _session.HasPermission(PermissionCodes.BackupExecute);
    public bool CanRestore => _session.HasPermission(PermissionCodes.BackupRestore);

    public override string Title => "Configurações";

    public SettingsViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog, ISessionService session)
        : base(scopeFactory, dialog)
    {
        _session = session;
    }

    public override Task LoadAsync() => RunAsync(async services =>
    {
        var settings = services.GetRequiredService<ISettingsService>();
        var all = await settings.GetAllAsync();

        CompanyName = all.GetValueOrDefault(SettingKeys.CompanyName) ?? string.Empty;
        CompanyCnpj = all.GetValueOrDefault(SettingKeys.CompanyCnpj) ?? string.Empty;
        LogoPath = all.GetValueOrDefault(SettingKeys.CompanyLogoPath) ?? string.Empty;
        BackupDirectory = all.GetValueOrDefault(SettingKeys.BackupDirectory) ?? string.Empty;
        BackupCompress = all.GetValueOrDefault(SettingKeys.BackupCompress) != "false";
        BackupAutoEnabled = all.GetValueOrDefault(SettingKeys.BackupAutoEnabled) == "true";
        SessionTimeoutMinutes = all.GetValueOrDefault(SettingKeys.SessionTimeoutMinutes) ?? "15";
        PgDumpPath = all.GetValueOrDefault(SettingKeys.PgDumpPath) ?? string.Empty;
        PgRestorePath = all.GetValueOrDefault(SettingKeys.PgRestorePath) ?? string.Empty;

        FiscalCnpj = all.GetValueOrDefault(SettingKeys.FiscalCnpj) ?? string.Empty;
        FiscalUf = all.GetValueOrDefault(SettingKeys.FiscalUf) ?? string.Empty;
        FiscalProduction = all.GetValueOrDefault(SettingKeys.FiscalProduction) != "false";
        FiscalDemoMode = all.GetValueOrDefault(SettingKeys.FiscalDemoMode) == "true";

        var fiscal = services.GetRequiredService<IFiscalService>();
        var certificate = await fiscal.GetCertificateInfoAsync();
        FiscalCertStatus = certificate is null
            ? "Nenhum certificado configurado."
            : $"Certificado: {certificate.Subject}\nVálido até {certificate.NotAfter:dd/MM/yyyy}";

        await LoadWarehousesAsync(services);
    });

    [RelayCommand]
    private void SelectFiscalCertificate()
    {
        var path = Dialog.OpenFile("Certificado A1 (*.pfx;*.p12)|*.pfx;*.p12");
        if (path is null)
            return;
        _fiscalCertBytes = System.IO.File.ReadAllBytes(path);
        FiscalCertFileName = System.IO.Path.GetFileName(path);
    }

    [RelayCommand]
    private Task SaveFiscalConfigAsync() => RunAsync(async services =>
    {
        var settings = services.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.FiscalDemoMode, FiscalDemoMode ? "true" : "false");

        // No modo demonstração não é preciso certificado nem CNPJ/UF válidos.
        if (FiscalDemoMode)
        {
            Dialog.Notify("Modo demonstração ativado. Em Notas Fiscais, clique em SINCRONIZAR SEFAZ para carregar notas de exemplo.");
            return;
        }

        var fiscal = services.GetRequiredService<IFiscalService>();
        var result = await fiscal.SaveConfigurationAsync(
            _fiscalCertBytes, FiscalCertPassword, FiscalCnpj, FiscalUf, FiscalProduction);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }

        _fiscalCertBytes = null;
        FiscalCertPassword = string.Empty;
        FiscalCertFileName = string.Empty;
        FiscalCertStatus = $"Certificado: {result.Value.Subject}\nVálido até {result.Value.NotAfter:dd/MM/yyyy}";
        Dialog.Notify("Configuração fiscal salva. Use SINCRONIZAR SEFAZ na tela Notas Fiscais.");
    });

    [RelayCommand]
    private Task SaveSettingsAsync() => RunAsync(async services =>
    {
        var settings = services.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.CompanyName, CompanyName);
        await settings.SetAsync(SettingKeys.CompanyCnpj, CompanyCnpj);
        await settings.SetAsync(SettingKeys.CompanyLogoPath, LogoPath);
        await settings.SetAsync(SettingKeys.BackupDirectory, BackupDirectory);
        await settings.SetAsync(SettingKeys.BackupCompress, BackupCompress ? "true" : "false");
        await settings.SetAsync(SettingKeys.BackupAutoEnabled, BackupAutoEnabled ? "true" : "false");
        await settings.SetAsync(SettingKeys.SessionTimeoutMinutes,
            int.TryParse(SessionTimeoutMinutes, out var minutes) && minutes >= 0 ? minutes.ToString() : "15");
        await settings.SetAsync(SettingKeys.PgDumpPath, PgDumpPath);
        await settings.SetAsync(SettingKeys.PgRestorePath, PgRestorePath);
        Dialog.Notify("Configurações salvas.");
    });

    [RelayCommand]
    private Task BackupNowAsync() => RunAsync(async services =>
    {
        var backup = services.GetRequiredService<IBackupService>();
        var path = await backup.BackupAsync(
            string.IsNullOrWhiteSpace(BackupDirectory) ? null : BackupDirectory, BackupCompress);
        Dialog.ShowInfo($"Backup gerado com sucesso:\n{path}");
    });

    [RelayCommand]
    private Task RestoreAsync() => RunAsync(async services =>
    {
        var file = Dialog.OpenFile("Backup (*.backup;*.zip)|*.backup;*.zip|Todos (*.*)|*.*");
        if (file is null)
            return;

        if (!Dialog.Confirm("A restauração substitui os dados atuais do banco.\nDeseja continuar?", "Restaurar backup"))
            return;

        var backup = services.GetRequiredService<IBackupService>();
        await backup.RestoreAsync(file);
        Dialog.ShowInfo("Backup restaurado com sucesso. Reinicie a aplicação.");
    });

    [RelayCommand]
    private void ChangeDatabaseConnection()
    {
        var viewModel = new DatabaseConfigViewModel(App.ReadConnectionString());
        var window = new Views.DatabaseConfigWindow
        {
            DataContext = viewModel,
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (window.ShowDialog() == true)
            Dialog.ShowInfo("Conexão salva. Feche e abra o ALMOX PRO para aplicar a nova configuração.");
    }

    [RelayCommand]
    private void SelectWarehouse(WarehouseDto warehouse)
    {
        SelectedWarehouse = warehouse;
        WarehouseCode = warehouse.Code;
        WarehouseName = warehouse.Name;
    }

    [RelayCommand]
    private void NewWarehouse()
    {
        SelectedWarehouse = null;
        WarehouseCode = string.Empty;
        WarehouseName = string.Empty;
    }

    [RelayCommand]
    private Task SaveWarehouseAsync() => RunAsync(async services =>
    {
        var organization = services.GetRequiredService<IOrganizationService>();
        var result = await organization.SaveWarehouseAsync(
            SelectedWarehouse?.Id, WarehouseCode, WarehouseName, null, EntityStatus.Ativo);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }
        await LoadWarehousesAsync(services);
        NewWarehouse();
    });

    private async Task LoadWarehousesAsync(IServiceProvider services)
    {
        var organization = services.GetRequiredService<IOrganizationService>();
        Warehouses.Clear();
        foreach (var warehouse in await organization.GetWarehousesAsync())
            Warehouses.Add(warehouse);
    }
}
