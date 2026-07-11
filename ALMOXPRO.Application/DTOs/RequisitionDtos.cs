using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Application.DTOs;

public record RequisitionListDto(
    int Id,
    string Number,
    RequisitionStatus Status,
    string WarehouseName,
    string SectorName,
    string? EmployeeName,
    string? RequesterName,
    DateTime RequestDate,
    DateTime? FulfilledAt,
    string? ExitNumber,
    int ItemCount);

public record RequisitionItemViewDto(
    int Id,
    int ProductId,
    string InternalCode,
    string ProductName,
    string Unit,
    decimal QuantityRequested,
    decimal QuantityFulfilled);

public class RequisitionCreateDto
{
    public int WarehouseId { get; set; }
    public int SectorId { get; set; }
    public int? EmployeeId { get; set; }
    public string? RequesterName { get; set; }
    public string? Notes { get; set; }
    public List<RequisitionItemCreateDto> Items { get; set; } = [];
}

public class RequisitionItemCreateDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}
