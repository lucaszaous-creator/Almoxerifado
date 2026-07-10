using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Domain.Entities.Catalog;

/// <summary>Endereçamento físico dentro de um almoxarifado.</summary>
public class Location : BaseEntity
{
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public string Building { get; set; } = string.Empty;   // Prédio
    public string? Floor { get; set; }                     // Andar
    public string? Corridor { get; set; }                  // Corredor
    public string? Shelf { get; set; }                     // Estante
    public string? Rack { get; set; }                      // Prateleira
    public string? Position { get; set; }                  // Posição
    public EntityStatus Status { get; set; } = EntityStatus.Ativo;

    /// <summary>Código legível usado no QR Code da localização.</summary>
    public string Code =>
        string.Join("-", new[] { Building, Floor, Corridor, Shelf, Rack, Position }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
}
