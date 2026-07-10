using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Domain.Entities.Catalog;

public class Product : BaseEntity
{
    public string InternalCode { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? QrCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public string? Brand { get; set; }
    public string? Manufacturer { get; set; }

    public int? MainSupplierId { get; set; }
    public Supplier? MainSupplier { get; set; }

    /// <summary>Unidade de medida (UN, CX, KG, L, M...).</summary>
    public string Unit { get; set; } = "UN";

    public decimal? Weight { get; set; }
    public string? Dimensions { get; set; }

    public decimal MinStock { get; set; }
    public decimal MaxStock { get; set; }

    public int? DefaultLocationId { get; set; }
    public Location? DefaultLocation { get; set; }

    public byte[]? Photo { get; set; }
    public string? Notes { get; set; }

    public bool IsControlled { get; set; }        // Produto controlado
    public bool IsPerishable { get; set; }        // Perecível
    public bool TracksExpiration { get; set; }    // Controla validade
    public bool TracksLot { get; set; }           // Controla lote
    public string? AssetNumber { get; set; }      // Patrimônio

    public EntityStatus Status { get; set; } = EntityStatus.Ativo;

    /// <summary>Custo médio ponderado, atualizado a cada entrada.</summary>
    public decimal AverageCost { get; set; }

    /// <summary>Custo da última entrada.</summary>
    public decimal LastCost { get; set; }

    /// <summary>
    /// Recalcula o custo médio ponderado após uma entrada.
    /// </summary>
    public void UpdateAverageCost(decimal currentStock, decimal entryQuantity, decimal entryUnitCost)
    {
        if (entryQuantity <= 0)
            return;

        var totalQuantity = currentStock + entryQuantity;
        AverageCost = totalQuantity <= 0
            ? entryUnitCost
            : ((currentStock * AverageCost) + (entryQuantity * entryUnitCost)) / totalQuantity;
        LastCost = entryUnitCost;
    }
}
