using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Organization;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Results;
using FluentValidation;

namespace ALMOXPRO.Application.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<int>> SaveAsync(CategoryUpsertDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CategoryUpsertDto> _validator;

    public CategoryService(IUnitOfWork uow, IValidator<CategoryUpsertDto> validator)
    {
        _uow = uow;
        _validator = validator;
    }

    public async Task<List<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _uow.Categories.GetAllWithParentAsync(ct);
        return categories
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.ParentId, c.Parent?.Name, c.Status))
            .OrderBy(c => c.ParentName ?? c.Name).ThenBy(c => c.Name)
            .ToList();
    }

    public async Task<Result<int>> SaveAsync(CategoryUpsertDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result.Failure<int>(validation.Errors.Select(e => e.ErrorMessage));

        Category category;
        if (dto.Id is null)
        {
            category = new Category();
            await _uow.Categories.AddAsync(category, ct);
        }
        else
        {
            category = await _uow.Categories.GetByIdAsync(dto.Id.Value, ct)
                ?? throw new InvalidOperationException("Categoria não encontrada.");
        }

        category.Name = dto.Name.Trim();
        category.Description = dto.Description;
        category.ParentId = dto.ParentId;
        category.Status = dto.Status;

        await _uow.SaveChangesAsync(ct);
        return Result.Success(category.Id);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await _uow.Categories.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure("Categoria não encontrada.");

        if (await _uow.Categories.HasProductsAsync(id, ct))
            return Result.Failure("Não é possível excluir: existem produtos nesta categoria.");
        if (await _uow.Categories.AnyAsync(c => c.ParentId == id, ct))
            return Result.Failure("Não é possível excluir: a categoria possui subcategorias.");

        _uow.Categories.Remove(category);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> SearchAsync(PagedQuery query, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(SupplierUpsertDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<SupplierUpsertDto> _validator;

    public SupplierService(IUnitOfWork uow, IValidator<SupplierUpsertDto> validator)
    {
        _uow = uow;
        _validator = validator;
    }

    public async Task<PagedResult<SupplierDto>> SearchAsync(PagedQuery query, CancellationToken ct = default)
    {
        var page = await _uow.Suppliers.SearchAsync(query, ct);
        return new PagedResult<SupplierDto>
        {
            Items = page.Items.Select(s => new SupplierDto(
                s.Id, s.CompanyName, s.TradeName, s.Cnpj, s.StateRegistration, s.Address,
                s.City, s.State, s.Phone, s.Email, s.ContactName, s.Notes, s.Status)).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<Result<int>> SaveAsync(SupplierUpsertDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result.Failure<int>(validation.Errors.Select(e => e.ErrorMessage));

        var cnpjDigits = new string(dto.Cnpj.Where(char.IsDigit).ToArray());
        var cnpjInUse = await _uow.Suppliers.AnyAsync(
            s => s.Cnpj == cnpjDigits && (dto.Id == null || s.Id != dto.Id), ct);
        if (cnpjInUse)
            return Result.Failure<int>("Já existe um fornecedor com este CNPJ.");

        Supplier supplier;
        if (dto.Id is null)
        {
            supplier = new Supplier();
            await _uow.Suppliers.AddAsync(supplier, ct);
        }
        else
        {
            supplier = await _uow.Suppliers.GetByIdAsync(dto.Id.Value, ct)
                ?? throw new InvalidOperationException("Fornecedor não encontrado.");
        }

        supplier.CompanyName = dto.CompanyName.Trim();
        supplier.TradeName = dto.TradeName;
        supplier.Cnpj = cnpjDigits;
        supplier.StateRegistration = dto.StateRegistration;
        supplier.Address = dto.Address;
        supplier.City = dto.City;
        supplier.State = dto.State?.ToUpperInvariant();
        supplier.ZipCode = dto.ZipCode;
        supplier.Phone = dto.Phone;
        supplier.Email = dto.Email;
        supplier.ContactName = dto.ContactName;
        supplier.Notes = dto.Notes;
        supplier.Status = dto.Status;

        await _uow.SaveChangesAsync(ct);
        return Result.Success(supplier.Id);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var supplier = await _uow.Suppliers.GetByIdAsync(id, ct);
        if (supplier is null)
            return Result.Failure("Fornecedor não encontrado.");

        if (await _uow.Products.AnyAsync(p => p.MainSupplierId == id, ct))
            return Result.Failure("Não é possível excluir: existem produtos vinculados a este fornecedor. Inative-o.");
        if (await _uow.Entries.AnyAsync(e => e.SupplierId == id, ct))
            return Result.Failure("Não é possível excluir: o fornecedor possui entradas registradas. Inative-o.");

        _uow.Suppliers.Remove(supplier);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public interface ILocationService
{
    Task<List<LocationDto>> GetAllAsync(int? warehouseId = null, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(LocationUpsertDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Conteúdo do QR Code da localização.</summary>
    string GetQrContent(LocationDto location);
}

public class LocationService : ILocationService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<LocationUpsertDto> _validator;

    public LocationService(IUnitOfWork uow, IValidator<LocationUpsertDto> validator)
    {
        _uow = uow;
        _validator = validator;
    }

    public async Task<List<LocationDto>> GetAllAsync(int? warehouseId = null, CancellationToken ct = default)
    {
        var locations = warehouseId is null
            ? await _uow.Locations.GetAllAsync(ct)
            : await _uow.Locations.GetByWarehouseAsync(warehouseId.Value, ct);

        var warehouses = (await _uow.Warehouses.GetAllAsync(ct)).ToDictionary(w => w.Id, w => w.Name);

        return locations.Select(l => new LocationDto(
            l.Id, l.WarehouseId, warehouses.GetValueOrDefault(l.WarehouseId, string.Empty),
            l.Building, l.Floor, l.Corridor, l.Shelf, l.Rack, l.Position, l.Code, l.Status)).ToList();
    }

    public async Task<Result<int>> SaveAsync(LocationUpsertDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result.Failure<int>(validation.Errors.Select(e => e.ErrorMessage));

        Location location;
        if (dto.Id is null)
        {
            location = new Location();
            await _uow.Locations.AddAsync(location, ct);
        }
        else
        {
            location = await _uow.Locations.GetByIdAsync(dto.Id.Value, ct)
                ?? throw new InvalidOperationException("Localização não encontrada.");
        }

        location.WarehouseId = dto.WarehouseId;
        location.Building = dto.Building.Trim();
        location.Floor = dto.Floor;
        location.Corridor = dto.Corridor;
        location.Shelf = dto.Shelf;
        location.Rack = dto.Rack;
        location.Position = dto.Position;
        location.Status = dto.Status;

        await _uow.SaveChangesAsync(ct);
        return Result.Success(location.Id);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var location = await _uow.Locations.GetByIdAsync(id, ct);
        if (location is null)
            return Result.Failure("Localização não encontrada.");

        if (await _uow.Products.AnyAsync(p => p.DefaultLocationId == id, ct))
            return Result.Failure("Não é possível excluir: existem produtos endereçados nesta localização.");

        _uow.Locations.Remove(location);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public string GetQrContent(LocationDto location) =>
        $"ALMOXPRO:LOCATION:{location.WarehouseId}:{location.Code}";
}

public interface ILookupService
{
    Task<List<LookupDto>> WarehousesAsync(CancellationToken ct = default);
    Task<List<LookupDto>> CategoriesAsync(CancellationToken ct = default);
    Task<List<LookupDto>> SuppliersAsync(CancellationToken ct = default);
    Task<List<LookupDto>> CostCentersAsync(CancellationToken ct = default);
    Task<List<LookupDto>> SectorsAsync(CancellationToken ct = default);
    Task<List<LookupDto>> EmployeesAsync(CancellationToken ct = default);
    Task<List<LookupDto>> RolesAsync(CancellationToken ct = default);
}

public class LookupService : ILookupService
{
    private readonly IUnitOfWork _uow;

    public LookupService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<LookupDto>> WarehousesAsync(CancellationToken ct = default) =>
        (await _uow.Warehouses.FindAsync(w => w.Status == EntityStatus.Ativo, ct))
            .Select(w => new LookupDto(w.Id, w.Name)).OrderBy(w => w.Name).ToList();

    public async Task<List<LookupDto>> CategoriesAsync(CancellationToken ct = default) =>
        (await _uow.Categories.FindAsync(c => c.Status == EntityStatus.Ativo, ct))
            .Select(c => new LookupDto(c.Id, c.Name)).OrderBy(c => c.Name).ToList();

    public async Task<List<LookupDto>> SuppliersAsync(CancellationToken ct = default) =>
        (await _uow.Suppliers.FindAsync(s => s.Status == EntityStatus.Ativo, ct))
            .Select(s => new LookupDto(s.Id, s.TradeName ?? s.CompanyName)).OrderBy(s => s.Name).ToList();

    public async Task<List<LookupDto>> CostCentersAsync(CancellationToken ct = default) =>
        (await _uow.CostCenters.FindAsync(c => c.Status == EntityStatus.Ativo, ct))
            .Select(c => new LookupDto(c.Id, $"{c.Code} - {c.Name}")).OrderBy(c => c.Name).ToList();

    public async Task<List<LookupDto>> SectorsAsync(CancellationToken ct = default) =>
        (await _uow.Sectors.FindAsync(s => s.Status == EntityStatus.Ativo, ct))
            .Select(s => new LookupDto(s.Id, s.Name)).OrderBy(s => s.Name).ToList();

    public async Task<List<LookupDto>> EmployeesAsync(CancellationToken ct = default) =>
        (await _uow.Employees.FindAsync(e => e.Status == EntityStatus.Ativo, ct))
            .Select(e => new LookupDto(e.Id, e.Name)).OrderBy(e => e.Name).ToList();

    public async Task<List<LookupDto>> RolesAsync(CancellationToken ct = default) =>
        (await _uow.Roles.GetAllAsync(ct))
            .Select(r => new LookupDto(r.Id, r.Name)).OrderBy(r => r.Name).ToList();
}
