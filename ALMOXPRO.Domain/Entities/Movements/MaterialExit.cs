using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Organization;
using ALMOXPRO.Domain.Entities.Stock;
using ALMOXPRO.Domain.Exceptions;

namespace ALMOXPRO.Domain.Entities.Movements;

/// <summary>Saída de materiais (consumo, ordem de serviço, descarte...).</summary>
public class MaterialExit : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public ExitType Type { get; set; }

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public int? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int? SectorId { get; set; }
    public Sector? Sector { get; set; }

    /// <summary>Número da ordem de serviço, quando aplicável.</summary>
    public string? WorkOrder { get; set; }

    public string? Reason { get; set; }

    /// <summary>Nome do responsável pelo recebimento.</summary>
    public string? ResponsibleName { get; set; }

    /// <summary>Assinatura capturada (imagem PNG).</summary>
    public byte[]? Signature { get; set; }

    public int UserId { get; set; }
    public DateTime ExitDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    /// <summary>Entrada de devolução que estornou esta saída, quando houver.</summary>
    public int? ReversedByEntryId { get; set; }
    public bool IsReversed => ReversedByEntryId.HasValue;

    public ICollection<MaterialExitItem> Items { get; set; } = new List<MaterialExitItem>();

    public decimal TotalValue => Items.Sum(i => Math.Round(i.Quantity * i.UnitCost, 2));

    public void Validate()
    {
        if (Items.Count == 0)
            throw new DomainException("A saída deve possuir ao menos um item.");
        if (Type == ExitType.OrdemServico && string.IsNullOrWhiteSpace(WorkOrder))
            throw new DomainException("Saídas por ordem de serviço exigem o número da OS.");
    }
}

public class MaterialExitItem : BaseEntity
{
    public int MaterialExitId { get; set; }
    public MaterialExit MaterialExit { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }

    /// <summary>Custo médio do produto no momento da saída.</summary>
    public decimal UnitCost { get; set; }

    public int? LotId { get; set; }
    public Lot? Lot { get; set; }

    public string? Notes { get; set; }
}
