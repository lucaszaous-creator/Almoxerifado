using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Domain.Entities.Catalog;

/// <summary>Almoxarifado (depósito físico).</summary>
public class Warehouse : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Ativo;
}
