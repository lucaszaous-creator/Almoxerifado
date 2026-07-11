using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Application.DTOs;

public class EntryCreateDto
{
    public EntryType Type { get; set; }
    public int WarehouseId { get; set; }
    public int? SupplierId { get; set; }
    public string? DocumentNumber { get; set; }
    public DateTime EntryDate { get; set; } = DateTime.Now;
    public string? Notes { get; set; }
    public List<EntryItemDto> Items { get; set; } = [];
}

public class EntryItemDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? LotNumber { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public int? LocationId { get; set; }
    public string? Notes { get; set; }
}

public record EntryListDto(
    int Id,
    string Number,
    EntryType Type,
    string WarehouseName,
    string? SupplierName,
    string? DocumentNumber,
    DateTime EntryDate,
    decimal TotalValue,
    int ItemCount);

public class ExitCreateDto
{
    public ExitType Type { get; set; }
    public int WarehouseId { get; set; }
    public int? CostCenterId { get; set; }
    public int? EmployeeId { get; set; }
    public int? SectorId { get; set; }
    public string? WorkOrder { get; set; }
    public string? Reason { get; set; }
    public string? ResponsibleName { get; set; }
    public byte[]? Signature { get; set; }
    public DateTime ExitDate { get; set; } = DateTime.Now;
    public string? Notes { get; set; }
    public List<ExitItemDto> Items { get; set; } = [];
}

public class ExitItemDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public int? LotId { get; set; }
    public string? Notes { get; set; }
}

public record ExitListDto(
    int Id,
    string Number,
    ExitType Type,
    string WarehouseName,
    string? CostCenterName,
    string? EmployeeName,
    string? SectorName,
    DateTime ExitDate,
    decimal TotalValue,
    int ItemCount,
    bool Reversed);

public class TransferCreateDto
{
    public int SourceWarehouseId { get; set; }
    public int DestinationWarehouseId { get; set; }
    public DateTime TransferDate { get; set; } = DateTime.Now;
    public string? Notes { get; set; }
    public List<TransferItemDto> Items { get; set; } = [];
}

public class TransferItemDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public int? LotId { get; set; }
}

public record TransferListDto(
    int Id,
    string Number,
    string SourceWarehouseName,
    string DestinationWarehouseName,
    DateTime TransferDate,
    int ItemCount);

public record StockBalanceDto(
    int ProductId,
    string InternalCode,
    string ProductName,
    string Unit,
    string WarehouseName,
    string? LocationCode,
    string? LotNumber,
    DateOnly? ExpirationDate,
    decimal Quantity,
    decimal AverageCost,
    decimal TotalValue,
    decimal MinStock);

public record StockMovementDto(
    long Id,
    DateTime MovementDate,
    string ProductName,
    string WarehouseName,
    StockMovementType Type,
    decimal Quantity,
    decimal UnitCost,
    decimal BalanceAfter,
    string DocumentType,
    string? DocumentNumber);
