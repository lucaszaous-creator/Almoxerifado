using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Results;
using FluentValidation;

namespace ALMOXPRO.Application.Services;

public interface IProductService
{
    Task<PagedResult<ProductListDto>> SearchAsync(PagedQuery query, int? categoryId = null, CancellationToken ct = default);
    Task<ProductUpsertDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<ProductListDto?> FindByCodeAsync(string code, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(ProductUpsertDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<string> SuggestInternalCodeAsync(CancellationToken ct = default);
}

public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<ProductUpsertDto> _validator;

    public ProductService(IUnitOfWork uow, IValidator<ProductUpsertDto> validator)
    {
        _uow = uow;
        _validator = validator;
    }

    public async Task<PagedResult<ProductListDto>> SearchAsync(PagedQuery query, int? categoryId = null, CancellationToken ct = default)
    {
        var page = await _uow.Products.SearchAsync(query, categoryId, ct);
        var items = new List<ProductListDto>(page.Items.Count);
        foreach (var p in page.Items)
        {
            var stock = await _uow.Stock.GetTotalQuantityAsync(p.Id, null, ct);
            items.Add(new ProductListDto(
                p.Id, p.InternalCode, p.Barcode, p.Name, p.Category.Name, p.Unit,
                p.MinStock, p.MaxStock, stock, p.AverageCost, p.Status));
        }

        return new PagedResult<ProductListDto>
        {
            Items = items,
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    public async Task<ProductUpsertDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var p = await _uow.Products.GetWithDetailsAsync(id, ct);
        if (p is null)
            return null;

        return new ProductUpsertDto
        {
            Id = p.Id,
            InternalCode = p.InternalCode,
            Barcode = p.Barcode,
            Name = p.Name,
            Description = p.Description,
            CategoryId = p.CategoryId,
            Brand = p.Brand,
            Manufacturer = p.Manufacturer,
            MainSupplierId = p.MainSupplierId,
            Unit = p.Unit,
            Weight = p.Weight,
            Dimensions = p.Dimensions,
            MinStock = p.MinStock,
            MaxStock = p.MaxStock,
            DefaultLocationId = p.DefaultLocationId,
            Photo = p.Photo,
            Notes = p.Notes,
            IsControlled = p.IsControlled,
            IsPerishable = p.IsPerishable,
            TracksExpiration = p.TracksExpiration,
            TracksLot = p.TracksLot,
            AssetNumber = p.AssetNumber,
            Status = p.Status
        };
    }

    public async Task<ProductListDto?> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        var p = await _uow.Products.GetByAnyCodeAsync(code.Trim(), ct);
        if (p is null)
            return null;

        var stock = await _uow.Stock.GetTotalQuantityAsync(p.Id, null, ct);
        return new ProductListDto(
            p.Id, p.InternalCode, p.Barcode, p.Name, p.Category.Name, p.Unit,
            p.MinStock, p.MaxStock, stock, p.AverageCost, p.Status);
    }

    public async Task<Result<int>> SaveAsync(ProductUpsertDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result.Failure<int>(validation.Errors.Select(e => e.ErrorMessage));

        var codeInUse = await _uow.Products.AnyAsync(
            p => p.InternalCode == dto.InternalCode && (dto.Id == null || p.Id != dto.Id), ct);
        if (codeInUse)
            return Result.Failure<int>("Já existe um produto com este código interno.");

        if (!string.IsNullOrWhiteSpace(dto.Barcode))
        {
            var barcodeInUse = await _uow.Products.AnyAsync(
                p => p.Barcode == dto.Barcode && (dto.Id == null || p.Id != dto.Id), ct);
            if (barcodeInUse)
                return Result.Failure<int>("Já existe um produto com este código de barras.");
        }

        Product product;
        if (dto.Id is null)
        {
            product = new Product();
            await _uow.Products.AddAsync(product, ct);
        }
        else
        {
            product = await _uow.Products.GetByIdAsync(dto.Id.Value, ct)
                ?? throw new InvalidOperationException("Produto não encontrado.");
        }

        product.InternalCode = dto.InternalCode.Trim();
        product.Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim();
        product.Name = dto.Name.Trim();
        product.Description = dto.Description;
        product.CategoryId = dto.CategoryId;
        product.Brand = dto.Brand;
        product.Manufacturer = dto.Manufacturer;
        product.MainSupplierId = dto.MainSupplierId;
        product.Unit = dto.Unit.Trim().ToUpperInvariant();
        product.Weight = dto.Weight;
        product.Dimensions = dto.Dimensions;
        product.MinStock = dto.MinStock;
        product.MaxStock = dto.MaxStock;
        product.DefaultLocationId = dto.DefaultLocationId;
        if (dto.Photo is not null)
            product.Photo = dto.Photo;
        product.Notes = dto.Notes;
        product.IsControlled = dto.IsControlled;
        product.IsPerishable = dto.IsPerishable;
        product.TracksExpiration = dto.TracksExpiration;
        product.TracksLot = dto.TracksLot;
        product.AssetNumber = dto.AssetNumber;
        product.Status = dto.Status;
        product.QrCode = $"ALMOXPRO:PRODUCT:{product.InternalCode}";

        await _uow.SaveChangesAsync(ct);
        return Result.Success(product.Id);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct);
        if (product is null)
            return Result.Failure("Produto não encontrado.");

        var hasStock = await _uow.Stock.GetTotalQuantityAsync(id, null, ct) > 0;
        if (hasStock)
            return Result.Failure("Não é possível excluir: o produto possui saldo em estoque. Inative-o.");

        var hasMovements = (await _uow.Stock.GetMovementsAsync(
            new PagedQuery { Page = 1, PageSize = 1 }, id, null, null, ct)).TotalCount > 0;
        if (hasMovements)
            return Result.Failure("Não é possível excluir: o produto possui movimentações. Inative-o.");

        _uow.Products.Remove(product);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public Task<string> SuggestInternalCodeAsync(CancellationToken ct = default) =>
        _uow.Products.GetNextInternalCodeAsync(ct);
}
