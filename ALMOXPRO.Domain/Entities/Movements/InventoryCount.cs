using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Stock;
using ALMOXPRO.Domain.Exceptions;

namespace ALMOXPRO.Domain.Entities.Movements;

/// <summary>Inventário (geral ou rotativo) de um almoxarifado.</summary>
public class InventoryCount : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public InventoryType Type { get; set; }
    public InventoryStatus Status { get; set; } = InventoryStatus.Aberto;

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public int UserId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public string? Notes { get; set; }

    public ICollection<InventoryCountItem> Items { get; set; } = new List<InventoryCountItem>();

    public void Start()
    {
        if (Status != InventoryStatus.Aberto)
            throw new DomainException("Somente inventários abertos podem iniciar contagem.");
        Status = InventoryStatus.EmContagem;
    }

    public void Finish()
    {
        if (Status != InventoryStatus.EmContagem)
            throw new DomainException("Somente inventários em contagem podem ser concluídos.");
        Status = InventoryStatus.Concluido;
        FinishedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == InventoryStatus.Concluido)
            throw new DomainException("Inventários concluídos não podem ser cancelados.");
        Status = InventoryStatus.Cancelado;
        FinishedAt = DateTime.UtcNow;
    }
}

public class InventoryCountItem : BaseEntity
{
    public int InventoryCountId { get; set; }
    public InventoryCount InventoryCount { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? LotId { get; set; }
    public Lot? Lot { get; set; }

    /// <summary>Saldo do sistema no momento da abertura do inventário.</summary>
    public decimal SystemQuantity { get; set; }

    /// <summary>Quantidade contada fisicamente; nula enquanto não conferido.</summary>
    public decimal? CountedQuantity { get; set; }

    public DateTime? CountedAt { get; set; }
    public int? CountedByUserId { get; set; }

    /// <summary>Indica se a diferença já foi ajustada no estoque.</summary>
    public bool Adjusted { get; set; }

    public decimal Difference => (CountedQuantity ?? 0) - SystemQuantity;
    public bool HasDifference => CountedQuantity.HasValue && Difference != 0;

    public void RegisterCount(decimal quantity, int userId)
    {
        if (quantity < 0)
            throw new DomainException("A quantidade contada não pode ser negativa.");
        CountedQuantity = quantity;
        CountedAt = DateTime.UtcNow;
        CountedByUserId = userId;
    }
}
