using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Application.DTOs;

public class InventoryOpenDto
{
    public InventoryType Type { get; set; }
    public int WarehouseId { get; set; }
    public string? Notes { get; set; }
    /// <summary>No inventário rotativo, limita a contagem a estas categorias (vazio = todas).</summary>
    public List<int> CategoryIds { get; set; } = [];
}

public record InventoryListDto(
    int Id,
    string Number,
    InventoryType Type,
    InventoryStatus Status,
    string WarehouseName,
    DateTime StartedAt,
    DateTime? FinishedAt,
    int ItemCount,
    int CountedItems,
    int ItemsWithDifference);

public record InventoryItemDto(
    int Id,
    int ProductId,
    string InternalCode,
    string ProductName,
    string? Barcode,
    string? LotNumber,
    decimal SystemQuantity,
    decimal? CountedQuantity,
    decimal Difference,
    bool Adjusted);
