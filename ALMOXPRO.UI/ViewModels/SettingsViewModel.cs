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
    private string _pgDumpPath = string.Empty;

    [ObservableProperty]
    private string _pgRestorePath = string.Empty;

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
        PgDumpPath = all.GetValueOrDefault(SettingKeys.PgDumpPath) ?? string.Empty;
        PgRestorePath = all.GetValueOrDefault(SettingKeys.PgRestorePath) ?? string.Empty;

        await LoadWarehousesAsync(services);
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
        await settings.SetAsync(SettingKeys.PgDumpPath, PgDumpPath);
        await settings.SetAsync(SettingKeys.PgRestorePath, PgRestorePath);
        Dialog.ShowInfo("Configurações salvas.");
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
