using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Domain.Entities.Organization;

/// <summary>Centro de custo para apropriação de saídas.</summary>
public class CostCenter : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public EntityStatus Status { get; set; } = EntityStatus.Ativo;
}

/// <summary>Setor consumidor de materiais.</summary>
public class Sector : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public EntityStatus Status { get; set; } = EntityStatus.Ativo;
}

/// <summary>Funcionário que retira materiais.</summary>
public class Employee : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public int? SectorId { get; set; }
    public Sector? Sector { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Ativo;
}
