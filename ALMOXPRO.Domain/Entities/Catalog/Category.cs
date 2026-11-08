using ALMOXPRO.Domain.Common;

namespace ALMOXPRO.Domain.Entities.Catalog;

/// <summary>Categoria de produtos. Subcategorias são categorias com ParentId preenchido.</summary>
public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Ativo;

    public ICollection<Category> Children { get; set; } = new List<Category>();
}
