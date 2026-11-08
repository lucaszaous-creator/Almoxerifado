using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Domain.Entities.Catalog;

public class Supplier : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;      // Razão Social
    public string? TradeName { get; set; }                       // Nome Fantasia
    public string Cnpj { get; set; } = string.Empty;
    public string? StateRegistration { get; set; }               // Inscrição Estadual
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ContactName { get; set; }
    public string? Notes { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Ativo;
}
