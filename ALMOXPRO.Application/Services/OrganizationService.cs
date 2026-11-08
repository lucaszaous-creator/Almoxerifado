using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Organization;
using ALMOXPRO.Shared.Results;

namespace ALMOXPRO.Application.Services;

/// <summary>CRUD de cadastros auxiliares: almoxarifados, centros de custo, setores e funcionários.</summary>
public interface IOrganizationService
{
    Task<List<WarehouseDto>> GetWarehousesAsync(CancellationToken ct = default);
    Task<Result<int>> SaveWarehouseAsync(int? id, string code, string name, string? description, EntityStatus status, CancellationToken ct = default);

    Task<List<CostCenter>> GetCostCentersAsync(CancellationToken ct = default);
    Task<Result<int>> SaveCostCenterAsync(int? id, string code, string name, EntityStatus status, CancellationToken ct = default);

    Task<List<Sector>> GetSectorsAsync(CancellationToken ct = default);
    Task<Result<int>> SaveSectorAsync(int? id, string name, EntityStatus status, CancellationToken ct = default);

    Task<List<Employee>> GetEmployeesAsync(CancellationToken ct = default);
    Task<Result<int>> SaveEmployeeAsync(int? id, string name, string? registration, int? sectorId, EntityStatus status, CancellationToken ct = default);
}

public class OrganizationService : IOrganizationService
{
    private readonly IUnitOfWork _uow;

    public OrganizationService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<WarehouseDto>> GetWarehousesAsync(CancellationToken ct = default) =>
        (await _uow.Warehouses.GetAllAsync(ct))
            .Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Description, w.Status))
            .OrderBy(w => w.Name).ToList();

    public async Task<Result<int>> SaveWarehouseAsync(int? id, string code, string name, string? description,
        EntityStatus status, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<int>("O nome do almoxarifado é obrigatório.");
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<int>("O código do almoxarifado é obrigatório.");

        Warehouse warehouse;
        if (id is null)
        {
            warehouse = new Warehouse();
            await _uow.Warehouses.AddAsync(warehouse, ct);
        }
        else
        {
            warehouse = await _uow.Warehouses.GetByIdAsync(id.Value, ct)
                ?? throw new InvalidOperationException("Almoxarifado não encontrado.");
        }

        warehouse.Code = code.Trim().ToUpperInvariant();
        warehouse.Name = name.Trim();
        warehouse.Description = description;
        warehouse.Status = status;
        await _uow.SaveChangesAsync(ct);
        return Result.Success(warehouse.Id);
    }

    public async Task<List<CostCenter>> GetCostCentersAsync(CancellationToken ct = default) =>
        (await _uow.CostCenters.GetAllAsync(ct)).OrderBy(c => c.Code).ToList();

    public async Task<Result<int>> SaveCostCenterAsync(int? id, string code, string name,
        EntityStatus status, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
            return Result.Failure<int>("Código e nome do centro de custo são obrigatórios.");

        CostCenter costCenter;
        if (id is null)
        {
            costCenter = new CostCenter();
            await _uow.CostCenters.AddAsync(costCenter, ct);
        }
        else
        {
            costCenter = await _uow.CostCenters.GetByIdAsync(id.Value, ct)
                ?? throw new InvalidOperationException("Centro de custo não encontrado.");
        }

        costCenter.Code = code.Trim();
        costCenter.Name = name.Trim();
        costCenter.Status = status;
        await _uow.SaveChangesAsync(ct);
        return Result.Success(costCenter.Id);
    }

    public async Task<List<Sector>> GetSectorsAsync(CancellationToken ct = default) =>
        (await _uow.Sectors.GetAllAsync(ct)).OrderBy(s => s.Name).ToList();

    public async Task<Result<int>> SaveSectorAsync(int? id, string name, EntityStatus status, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<int>("O nome do setor é obrigatório.");

        Sector sector;
        if (id is null)
        {
            sector = new Sector();
            await _uow.Sectors.AddAsync(sector, ct);
        }
        else
        {
            sector = await _uow.Sectors.GetByIdAsync(id.Value, ct)
                ?? throw new InvalidOperationException("Setor não encontrado.");
        }

        sector.Name = name.Trim();
        sector.Status = status;
        await _uow.SaveChangesAsync(ct);
        return Result.Success(sector.Id);
    }

    public async Task<List<Employee>> GetEmployeesAsync(CancellationToken ct = default) =>
        (await _uow.Employees.GetAllAsync(ct)).OrderBy(e => e.Name).ToList();

    public async Task<Result<int>> SaveEmployeeAsync(int? id, string name, string? registration,
        int? sectorId, EntityStatus status, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<int>("O nome do funcionário é obrigatório.");

        Employee employee;
        if (id is null)
        {
            employee = new Employee();
            await _uow.Employees.AddAsync(employee, ct);
        }
        else
        {
            employee = await _uow.Employees.GetByIdAsync(id.Value, ct)
                ?? throw new InvalidOperationException("Funcionário não encontrado.");
        }

        employee.Name = name.Trim();
        employee.RegistrationNumber = registration;
        employee.SectorId = sectorId;
        employee.Status = status;
        await _uow.SaveChangesAsync(ct);
        return Result.Success(employee.Id);
    }
}
