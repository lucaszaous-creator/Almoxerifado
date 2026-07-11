using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Organization;
using ALMOXPRO.Domain.Exceptions;

namespace ALMOXPRO.Domain.Entities.Movements;

/// <summary>
/// Requisição de materiais de um setor. O responsável do almoxarifado
/// imprime o documento, quem retira assina, e o atendimento gera a saída
/// de estoque automaticamente, mantendo o lastro requisição → saída.
/// </summary>
public class Requisition : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public RequisitionStatus Status { get; set; } = RequisitionStatus.Pendente;

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public int SectorId { get; set; }
    public Sector Sector { get; set; } = null!;

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    /// <summary>Nome de quem solicitou/retira, quando não é funcionário cadastrado.</summary>
    public string? RequesterName { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;

    public int? FulfilledByUserId { get; set; }
    public DateTime? FulfilledAt { get; set; }

    /// <summary>Saída de estoque gerada no atendimento (lastro da movimentação).</summary>
    public int? MaterialExitId { get; set; }
    public MaterialExit? MaterialExit { get; set; }

    public string? Notes { get; set; }

    public ICollection<RequisitionItem> Items { get; set; } = new List<RequisitionItem>();

    public void Validate()
    {
        if (Items.Count == 0)
            throw new DomainException("A requisição deve possuir ao menos um item.");
        if (SectorId <= 0)
            throw new DomainException("A requisição deve indicar o setor solicitante.");
    }

    public void MarkFulfilled(int userId, int materialExitId)
    {
        if (Status != RequisitionStatus.Pendente)
            throw new DomainException("Apenas requisições pendentes podem ser atendidas.");
        Status = RequisitionStatus.Atendida;
        FulfilledByUserId = userId;
        FulfilledAt = DateTime.UtcNow;
        MaterialExitId = materialExitId;
    }

    public void Cancel()
    {
        if (Status != RequisitionStatus.Pendente)
            throw new DomainException("Apenas requisições pendentes podem ser canceladas.");
        Status = RequisitionStatus.Cancelada;
    }
}

public class RequisitionItem : BaseEntity
{
    public int RequisitionId { get; set; }
    public Requisition Requisition { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal QuantityRequested { get; set; }
    public decimal QuantityFulfilled { get; set; }

    public string? Notes { get; set; }
}
