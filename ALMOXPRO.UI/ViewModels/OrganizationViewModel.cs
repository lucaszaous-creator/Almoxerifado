using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Organization;
using ALMOXPRO.Shared.Security;
using ALMOXPRO.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ALMOXPRO.UI.ViewModels;

/// <summary>Cadastro de setores consumidores, funcionários e centros de custo.</summary>
public partial class OrganizationViewModel : ViewModelBase
{
    private readonly ISessionService _session;

    // Setores
    [ObservableProperty]
    private Sector? _selectedSector;

    [ObservableProperty]
    private string _sectorName = string.Empty;

    [ObservableProperty]
    private bool _sectorActive = true;

    // Funcionários
    [ObservableProperty]
    private Employee? _selectedEmployee;

    [ObservableProperty]
    private string _employeeName = string.Empty;

    [ObservableProperty]
    private string? _employeeRegistration;

    [ObservableProperty]
    private Sector? _employeeSector;

    [ObservableProperty]
    private bool _employeeActive = true;

    // Centros de custo
    [ObservableProperty]
    private CostCenter? _selectedCostCenter;

    [ObservableProperty]
    private string _costCenterCode = string.Empty;

    [ObservableProperty]
    private string _costCenterName = string.Empty;

    [ObservableProperty]
    private bool _costCenterActive = true;

    public ObservableCollection<Sector> Sectors { get; } = [];
    public ObservableCollection<Employee> Employees { get; } = [];
    public ObservableCollection<CostCenter> CostCenters { get; } = [];

    public bool CanManage => _session.HasPermission(PermissionCodes.OrganizationManage);

    public override string Title => "Setores & Equipe";

    public OrganizationViewModel(IServiceScopeFactory scopeFactory, IDialogService dialog, ISessionService session)
        : base(scopeFactory, dialog)
    {
        _session = session;
    }

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadIntoAsync);

    partial void OnSelectedSectorChanged(Sector? value)
    {
        SectorName = value?.Name ?? string.Empty;
        SectorActive = value is null || value.Status == EntityStatus.Ativo;
    }

    partial void OnSelectedEmployeeChanged(Employee? value)
    {
        EmployeeName = value?.Name ?? string.Empty;
        EmployeeRegistration = value?.RegistrationNumber;
        EmployeeSector = value?.SectorId is null ? null : Sectors.FirstOrDefault(s => s.Id == value.SectorId);
        EmployeeActive = value is null || value.Status == EntityStatus.Ativo;
    }

    partial void OnSelectedCostCenterChanged(CostCenter? value)
    {
        CostCenterCode = value?.Code ?? string.Empty;
        CostCenterName = value?.Name ?? string.Empty;
        CostCenterActive = value is null || value.Status == EntityStatus.Ativo;
    }

    [RelayCommand]
    private void NewSector() => SelectedSector = null;

    [RelayCommand]
    private Task SaveSectorAsync() => RunAsync(async services =>
    {
        var organization = services.GetRequiredService<IOrganizationService>();
        var result = await organization.SaveSectorAsync(SelectedSector?.Id, SectorName,
            SectorActive ? EntityStatus.Ativo : EntityStatus.Inativo);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }
        await LoadIntoAsync(services);
        SelectedSector = null;
    });

    [RelayCommand]
    private void NewEmployee() => SelectedEmployee = null;

    [RelayCommand]
    private Task SaveEmployeeAsync() => RunAsync(async services =>
    {
        var organization = services.GetRequiredService<IOrganizationService>();
        var result = await organization.SaveEmployeeAsync(SelectedEmployee?.Id, EmployeeName,
            EmployeeRegistration, EmployeeSector?.Id,
            EmployeeActive ? EntityStatus.Ativo : EntityStatus.Inativo);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }
        await LoadIntoAsync(services);
        SelectedEmployee = null;
    });

    [RelayCommand]
    private void NewCostCenter() => SelectedCostCenter = null;

    [RelayCommand]
    private Task SaveCostCenterAsync() => RunAsync(async services =>
    {
        var organization = services.GetRequiredService<IOrganizationService>();
        var result = await organization.SaveCostCenterAsync(SelectedCostCenter?.Id, CostCenterCode,
            CostCenterName, CostCenterActive ? EntityStatus.Ativo : EntityStatus.Inativo);
        if (result.IsFailure)
        {
            Dialog.ShowError(string.Join("\n", result.Errors));
            return;
        }
        await LoadIntoAsync(services);
        SelectedCostCenter = null;
    });

    private async Task LoadIntoAsync(IServiceProvider services)
    {
        var organization = services.GetRequiredService<IOrganizationService>();

        Sectors.Clear();
        foreach (var sector in await organization.GetSectorsAsync())
            Sectors.Add(sector);

        Employees.Clear();
        foreach (var employee in await organization.GetEmployeesAsync())
            Employees.Add(employee);

        CostCenters.Clear();
        foreach (var costCenter in await organization.GetCostCentersAsync())
            CostCenters.Add(costCenter);
    }
}
