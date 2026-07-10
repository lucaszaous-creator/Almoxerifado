using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;

namespace ALMOXPRO.Domain.Entities.Stock;

/// <summary>Lote de um produto, com validade opcional.</summary>
public class Lot : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string LotNumber { get; set; } = string.Empty;
    public DateOnly? ExpirationDate { get; set; }

    public bool IsExpired(DateOnly referenceDate) =>
        ExpirationDate.HasValue && ExpirationDate.Value < referenceDate;
}
