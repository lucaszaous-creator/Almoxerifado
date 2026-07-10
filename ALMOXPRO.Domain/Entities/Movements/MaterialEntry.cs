using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Exceptions;

namespace ALMOXPRO.Domain.Entities.Movements;

/// <summary>Entrada de materiais (nota fiscal, compra, doação, devolução, ajuste...).</summary>
public class MaterialEntry : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public EntryType Type { get; set; }

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Número do documento de origem (ex.: nota fiscal).</summary>
    public string? DocumentNumber { get; set; }

    public int UserId { get; set; }
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public ICollection<MaterialEntryItem> Items { get; set; } = new List<MaterialEntryItem>();

    public decimal TotalValue => Items.Sum(i => i.Total);

    public void Validate()
    {
        if (Items.Count == 0)
            throw new DomainException("A entrada deve possuir ao menos um item.");
        if (Type is EntryType.NotaFiscal or EntryType.Compra && SupplierId is null)
            throw new DomainException("Entradas por nota fiscal ou compra exigem fornecedor.");
    }
}

public class MaterialEntryItem : BaseEntity
{
    public int MaterialEntryId { get; set; }
    public MaterialEntry MaterialEntry { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    public string? LotNumber { get; set; }
    public DateOnly? ExpirationDate { get; set; }

    public int? LocationId { get; set; }
    public Location? Location { get; set; }

    public string? Notes { get; set; }

    public decimal Total => Math.Round(Quantity * UnitCost, 2);
}
