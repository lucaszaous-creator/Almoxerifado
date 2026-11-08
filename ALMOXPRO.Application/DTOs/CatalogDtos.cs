using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Application.DTOs;

public record LookupDto(int Id, string Name);

public record CategoryDto(int Id, string Name, string? Description, int? ParentId, string? ParentName, EntityStatus Status);

public class CategoryUpsertDto
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Ativo;
}

public record SupplierDto(
    int Id,
    string CompanyName,
    string? TradeName,
    string Cnpj,
    string? StateRegistration,
    string? Address,
    string? City,
    string? State,
    string? Phone,
    string? Email,
    string? ContactName,
    string? Notes,
    EntityStatus Status);

public class SupplierUpsertDto
{
    public int? Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? TradeName { get; set; }
    public string Cnpj { get; set; } = string.Empty;
    public string? StateRegistration { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ContactName { get; set; }
    public string? Notes { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Ativo;
}

public record WarehouseDto(int Id, string Code, string Name, string? Description, EntityStatus Status);

public record LocationDto(
    int Id,
    int WarehouseId,
    string WarehouseName,
    string Building,
    string? Floor,
    string? Corridor,
    string? Shelf,
    string? Rack,
    string? Position,
    string Code,
    EntityStatus Status);

public class LocationUpsertDto
{
    public int? Id { get; set; }
    public int WarehouseId { get; set; }
    public string Building { get; set; } = string.Empty;
    public string? Floor { get; set; }
    public string? Corridor { get; set; }
    public string? Shelf { get; set; }
    public string? Rack { get; set; }
    public string? Position { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Ativo;
}

public record ProductListDto(
    int Id,
    string InternalCode,
    string? Barcode,
    string Name,
    string CategoryName,
    string Unit,
    decimal MinStock,
    decimal MaxStock,
    decimal CurrentStock,
    decimal AverageCost,
    EntityStatus Status);

public class ProductUpsertDto
{
    public int? Id { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public string? Brand { get; set; }
    public string? Manufacturer { get; set; }
    public int? MainSupplierId { get; set; }
    public string Unit { get; set; } = "UN";
    public decimal? Weight { get; set; }
    public string? Dimensions { get; set; }
    public decimal MinStock { get; set; }
    public decimal MaxStock { get; set; }
    public int? DefaultLocationId { get; set; }
    public byte[]? Photo { get; set; }
    public string? Notes { get; set; }
    public bool IsControlled { get; set; }
    public bool IsPerishable { get; set; }
    public bool TracksExpiration { get; set; }
    public bool TracksLot { get; set; }
    public string? AssetNumber { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Ativo;
}
