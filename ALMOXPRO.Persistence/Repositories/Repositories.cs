using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Fiscal;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Domain.Entities.Movements;
using ALMOXPRO.Domain.Entities.Organization;
using ALMOXPRO.Domain.Entities.Security;
using ALMOXPRO.Domain.Entities.Stock;
using ALMOXPRO.Persistence.Context;
using ALMOXPRO.Shared.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ALMOXPRO.Persistence.Repositories;

internal static class QueryExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PagedQuery paging, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(ct);
        return new PagedResult<T> { Items = items, TotalCount = total, Page = paging.Page, PageSize = paging.PageSize };
    }
}

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AlmoxProDbContext context) : base(context) { }

    public Task<User?> GetByLoginAsync(string login, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(u => u.Login == login, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetWithAccessAsync(int id, CancellationToken ct = default) =>
        Set.Include(u => u.Roles).ThenInclude(r => r.Role).ThenInclude(r => r.Permissions).ThenInclude(p => p.Permission)
           .Include(u => u.Permissions).ThenInclude(p => p.Permission)
           .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByResetTokenAsync(string token, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(u => u.PasswordResetToken == token, ct);

    public Task<PagedResult<User>> SearchAsync(PagedQuery query, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = $"%{query.Search.Trim()}%";
            q = q.Where(u => EF.Functions.ILike(u.Name, s) || EF.Functions.ILike(u.Login, s) || EF.Functions.ILike(u.Email, s));
        }
        return q.OrderBy(u => u.Name).ToPagedResultAsync(query, ct);
    }
}

public class RoleRepository : Repository<Role>, IRoleRepository
{
    public RoleRepository(AlmoxProDbContext context) : base(context) { }

    public Task<Role?> GetWithPermissionsAsync(int id, CancellationToken ct = default) =>
        Set.Include(r => r.Permissions).ThenInclude(p => p.Permission).FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<Role>> GetAllWithPermissionsAsync(CancellationToken ct = default) =>
        Set.Include(r => r.Permissions).ThenInclude(p => p.Permission).OrderBy(r => r.Name).ToListAsync(ct);
}

public class PermissionRepository : Repository<Permission>, IPermissionRepository
{
    public PermissionRepository(AlmoxProDbContext context) : base(context) { }

    public Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(p => p.Code == code, ct);
}

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AlmoxProDbContext context) : base(context) { }

    public Task<Product?> GetWithDetailsAsync(int id, CancellationToken ct = default) =>
        Set.Include(p => p.Category).Include(p => p.MainSupplier).Include(p => p.DefaultLocation)
           .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product?> GetByAnyCodeAsync(string code, CancellationToken ct = default) =>
        Set.Include(p => p.Category)
           .FirstOrDefaultAsync(p => p.InternalCode == code || p.Barcode == code || p.QrCode == code, ct);

    public Task<PagedResult<Product>> SearchAsync(PagedQuery query, int? categoryId = null, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking().Include(p => p.Category).AsQueryable();
        if (categoryId.HasValue)
            q = q.Where(p => p.CategoryId == categoryId || p.Category.ParentId == categoryId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = $"%{query.Search.Trim()}%";
            q = q.Where(p => EF.Functions.ILike(p.Name, s) || EF.Functions.ILike(p.InternalCode, s)
                          || (p.Barcode != null && EF.Functions.ILike(p.Barcode, s)));
        }
        return q.OrderBy(p => p.Name).ToPagedResultAsync(query, ct);
    }

    public async Task<string> GetNextInternalCodeAsync(CancellationToken ct = default)
    {
        var count = await Set.CountAsync(ct);
        string code;
        do
        {
            count++;
            code = $"P{count:D6}";
        } while (await Set.AnyAsync(p => p.InternalCode == code, ct));
        return code;
    }
}

public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(AlmoxProDbContext context) : base(context) { }

    public Task<List<Category>> GetAllWithParentAsync(CancellationToken ct = default) =>
        Set.Include(c => c.Parent).OrderBy(c => c.Name).ToListAsync(ct);

    public Task<bool> HasProductsAsync(int categoryId, CancellationToken ct = default) =>
        Context.Products.AnyAsync(p => p.CategoryId == categoryId, ct);
}

public class SupplierRepository : Repository<Supplier>, ISupplierRepository
{
    public SupplierRepository(AlmoxProDbContext context) : base(context) { }

    public Task<PagedResult<Supplier>> SearchAsync(PagedQuery query, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = $"%{query.Search.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.CompanyName, s)
                          || (x.TradeName != null && EF.Functions.ILike(x.TradeName, s))
                          || x.Cnpj.Contains(query.Search.Trim()));
        }
        return q.OrderBy(x => x.CompanyName).ToPagedResultAsync(query, ct);
    }
}

public class WarehouseRepository : Repository<Warehouse>, IWarehouseRepository
{
    public WarehouseRepository(AlmoxProDbContext context) : base(context) { }
}

public class LocationRepository : Repository<Location>, ILocationRepository
{
    public LocationRepository(AlmoxProDbContext context) : base(context) { }

    public Task<List<Location>> GetByWarehouseAsync(int warehouseId, CancellationToken ct = default) =>
        Set.Where(l => l.WarehouseId == warehouseId).ToListAsync(ct);
}

public class LotRepository : Repository<Lot>, ILotRepository
{
    public LotRepository(AlmoxProDbContext context) : base(context) { }

    public Task<Lot?> GetByNumberAsync(int productId, string lotNumber, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(l => l.ProductId == productId && l.LotNumber == lotNumber, ct);
}

public class StockRepository : IStockRepository
{
    private readonly AlmoxProDbContext _context;

    public StockRepository(AlmoxProDbContext context) => _context = context;

    public Task<StockItem?> GetItemAsync(int productId, int warehouseId, int? lotId, CancellationToken ct = default) =>
        _context.StockItems.Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId && s.LotId == lotId, ct);

    public Task<List<StockItem>> GetItemsWithLotsAsync(int productId, int warehouseId, CancellationToken ct = default) =>
        _context.StockItems.Include(s => s.Product).Include(s => s.Lot)
            .Where(s => s.ProductId == productId && s.WarehouseId == warehouseId && s.Quantity > 0)
            .ToListAsync(ct);

    public Task<List<StockItem>> GetByWarehouseAsync(int warehouseId, CancellationToken ct = default) =>
        _context.StockItems.Include(s => s.Product).Include(s => s.Lot)
            .Where(s => s.WarehouseId == warehouseId && s.Quantity != 0).ToListAsync(ct);

    public Task<List<StockItem>> GetByProductAsync(int productId, CancellationToken ct = default) =>
        _context.StockItems.Include(s => s.Warehouse).Include(s => s.Lot)
            .Where(s => s.ProductId == productId).ToListAsync(ct);

    public async Task<decimal> GetTotalQuantityAsync(int productId, int? warehouseId = null, CancellationToken ct = default)
    {
        var query = _context.StockItems.Where(s => s.ProductId == productId);
        if (warehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == warehouseId);
        // Soma no cliente: poucos registros por produto e o SQLite (testes)
        // não agrega decimal no servidor.
        return (await query.Select(s => s.Quantity).ToListAsync(ct)).Sum();
    }

    public Task<List<StockItem>> GetAllWithDetailsAsync(CancellationToken ct = default) =>
        _context.StockItems.AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Warehouse)
            .Include(s => s.Location)
            .Include(s => s.Lot)
            .ToListAsync(ct);

    public async Task AddItemAsync(StockItem item, CancellationToken ct = default) =>
        await _context.StockItems.AddAsync(item, ct);

    public async Task AddMovementAsync(StockMovement movement, CancellationToken ct = default) =>
        await _context.StockMovements.AddAsync(movement, ct);

    public Task<PagedResult<StockMovement>> GetMovementsAsync(PagedQuery query, int? productId = null,
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var q = _context.StockMovements.AsNoTracking()
            .Include(m => m.Product).Include(m => m.Warehouse).AsQueryable();
        if (productId.HasValue)
            q = q.Where(m => m.ProductId == productId);
        if (from.HasValue)
            q = q.Where(m => m.MovementDate >= from.Value.ToUniversalTime());
        if (to.HasValue)
            q = q.Where(m => m.MovementDate <= to.Value.ToUniversalTime());
        q = query.Descending ? q.OrderByDescending(m => m.MovementDate) : q.OrderBy(m => m.MovementDate);
        return q.ToPagedResultAsync(query, ct);
    }
}

public class MaterialEntryRepository : Repository<MaterialEntry>, IMaterialEntryRepository
{
    public MaterialEntryRepository(AlmoxProDbContext context) : base(context) { }

    public Task<MaterialEntry?> GetWithItemsAsync(int id, CancellationToken ct = default) =>
        Set.Include(e => e.Warehouse).Include(e => e.Supplier)
           .Include(e => e.Items).ThenInclude(i => i.Product)
           .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<PagedResult<MaterialEntry>> SearchAsync(PagedQuery query, DateTime? from = null,
        DateTime? to = null, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking()
            .Include(e => e.Warehouse).Include(e => e.Supplier).Include(e => e.Items)
            .AsQueryable();
        if (from.HasValue)
            q = q.Where(e => e.EntryDate >= from.Value.ToUniversalTime());
        if (to.HasValue)
            q = q.Where(e => e.EntryDate <= to.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = $"%{query.Search.Trim()}%";
            q = q.Where(e => EF.Functions.ILike(e.Number, s)
                          || (e.DocumentNumber != null && EF.Functions.ILike(e.DocumentNumber, s)));
        }
        return q.OrderByDescending(e => e.EntryDate).ToPagedResultAsync(query, ct);
    }
}

public class MaterialExitRepository : Repository<MaterialExit>, IMaterialExitRepository
{
    public MaterialExitRepository(AlmoxProDbContext context) : base(context) { }

    public Task<MaterialExit?> GetWithItemsAsync(int id, CancellationToken ct = default) =>
        Set.Include(e => e.Warehouse).Include(e => e.CostCenter).Include(e => e.Employee).Include(e => e.Sector)
           .Include(e => e.Items).ThenInclude(i => i.Product)
           .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<PagedResult<MaterialExit>> SearchAsync(PagedQuery query, DateTime? from = null,
        DateTime? to = null, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking()
            .Include(e => e.Warehouse).Include(e => e.CostCenter).Include(e => e.Employee)
            .Include(e => e.Sector).Include(e => e.Items)
            .AsQueryable();
        if (from.HasValue)
            q = q.Where(e => e.ExitDate >= from.Value.ToUniversalTime());
        if (to.HasValue)
            q = q.Where(e => e.ExitDate <= to.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = $"%{query.Search.Trim()}%";
            q = q.Where(e => EF.Functions.ILike(e.Number, s)
                          || (e.WorkOrder != null && EF.Functions.ILike(e.WorkOrder, s)));
        }
        return q.OrderByDescending(e => e.ExitDate).ToPagedResultAsync(query, ct);
    }
}

public class StockTransferRepository : Repository<StockTransfer>, IStockTransferRepository
{
    public StockTransferRepository(AlmoxProDbContext context) : base(context) { }

    public Task<StockTransfer?> GetWithItemsAsync(int id, CancellationToken ct = default) =>
        Set.Include(t => t.SourceWarehouse).Include(t => t.DestinationWarehouse)
           .Include(t => t.Items).ThenInclude(i => i.Product)
           .FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<PagedResult<StockTransfer>> SearchAsync(PagedQuery query, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking()
            .Include(t => t.SourceWarehouse).Include(t => t.DestinationWarehouse).Include(t => t.Items)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(t => EF.Functions.ILike(t.Number, $"%{query.Search.Trim()}%"));
        return q.OrderByDescending(t => t.TransferDate).ToPagedResultAsync(query, ct);
    }
}

public class InventoryRepository : Repository<InventoryCount>, IInventoryRepository
{
    public InventoryRepository(AlmoxProDbContext context) : base(context) { }

    public Task<InventoryCount?> GetWithItemsAsync(int id, CancellationToken ct = default) =>
        Set.Include(i => i.Warehouse)
           .Include(i => i.Items).ThenInclude(x => x.Product)
           .Include(i => i.Items).ThenInclude(x => x.Lot)
           .FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<PagedResult<InventoryCount>> SearchAsync(PagedQuery query, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking().Include(i => i.Warehouse).Include(i => i.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(i => EF.Functions.ILike(i.Number, $"%{query.Search.Trim()}%"));
        return q.OrderByDescending(i => i.StartedAt).ToPagedResultAsync(query, ct);
    }
}

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AlmoxProDbContext _context;

    public AuditLogRepository(AlmoxProDbContext context) => _context = context;

    public async Task AddAsync(AuditLog log, CancellationToken ct = default) =>
        await _context.AuditLogs.AddAsync(log, ct);

    public Task<PagedResult<AuditLog>> SearchAsync(PagedQuery query, DateTime? from = null, DateTime? to = null,
        string? tableName = null, CancellationToken ct = default)
    {
        var q = _context.AuditLogs.AsNoTracking().AsQueryable();
        if (from.HasValue)
            q = q.Where(a => a.OccurredAt >= from.Value.ToUniversalTime());
        if (to.HasValue)
            q = q.Where(a => a.OccurredAt <= to.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(tableName))
            q = q.Where(a => a.TableName == tableName);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = $"%{query.Search.Trim()}%";
            q = q.Where(a => EF.Functions.ILike(a.UserName, s) || EF.Functions.ILike(a.Action, s)
                          || EF.Functions.ILike(a.TableName, s));
        }
        return q.OrderByDescending(a => a.OccurredAt).ToPagedResultAsync(query, ct);
    }
}

public class AppSettingRepository : Repository<AppSetting>, IAppSettingRepository
{
    public AppSettingRepository(AlmoxProDbContext context) : base(context) { }

    public Task<AppSetting?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(s => s.Key == key, ct);
}

public class DocumentSequenceRepository : Repository<DocumentSequence>, IDocumentSequenceRepository
{
    public DocumentSequenceRepository(AlmoxProDbContext context) : base(context) { }

    public async Task<string> NextNumberAsync(string key, string prefix, CancellationToken ct = default)
    {
        var sequence = await Set.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (sequence is null)
        {
            sequence = new DocumentSequence { Key = key, Prefix = prefix };
            await Set.AddAsync(sequence, ct);
        }

        var number = sequence.TakeNext();
        await Context.SaveChangesAsync(ct);
        return number;
    }
}

public class CostCenterRepository : Repository<CostCenter>, ICostCenterRepository
{
    public CostCenterRepository(AlmoxProDbContext context) : base(context) { }
}

public class SectorRepository : Repository<Sector>, ISectorRepository
{
    public SectorRepository(AlmoxProDbContext context) : base(context) { }
}

public class EmployeeRepository : Repository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(AlmoxProDbContext context) : base(context) { }
}

public class RequisitionRepository : Repository<Requisition>, IRequisitionRepository
{
    public RequisitionRepository(AlmoxProDbContext context) : base(context) { }

    public Task<Requisition?> GetWithItemsAsync(int id, CancellationToken ct = default) =>
        Set.Include(r => r.Warehouse).Include(r => r.Sector).Include(r => r.Employee)
           .Include(r => r.Items).ThenInclude(i => i.Product)
           .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<PagedResult<Requisition>> SearchAsync(PagedQuery query, Domain.Common.RequisitionStatus? status = null,
        int? sectorId = null, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking()
            .Include(r => r.Warehouse).Include(r => r.Sector).Include(r => r.Employee).Include(r => r.Items)
            .AsQueryable();
        if (status.HasValue)
            q = q.Where(r => r.Status == status.Value);
        if (sectorId.HasValue)
            q = q.Where(r => r.SectorId == sectorId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = $"%{query.Search.Trim()}%";
            q = q.Where(r => EF.Functions.ILike(r.Number, s)
                          || (r.RequesterName != null && EF.Functions.ILike(r.RequesterName, s)));
        }
        return q.OrderByDescending(r => r.RequestDate).ToPagedResultAsync(query, ct);
    }

    public async Task<decimal> GetReservedQuantityAsync(int productId, int warehouseId,
        int? excludeRequisitionId = null, CancellationToken ct = default)
    {
        var open = new[] { Domain.Common.RequisitionStatus.Pendente, Domain.Common.RequisitionStatus.AtendidaParcial };
        var query = Context.Set<RequisitionItem>()
            .Where(i => i.ProductId == productId
                     && i.Requisition.WarehouseId == warehouseId
                     && open.Contains(i.Requisition.Status));
        if (excludeRequisitionId.HasValue)
            query = query.Where(i => i.RequisitionId != excludeRequisitionId.Value);
        return await query.SumAsync(i => (decimal?)(i.QuantityRequested - i.QuantityFulfilled), ct) ?? 0;
    }
}

public class FiscalDocumentRepository : Repository<FiscalDocument>, IFiscalDocumentRepository
{
    public FiscalDocumentRepository(AlmoxProDbContext context) : base(context) { }

    public Task<FiscalDocument?> GetByAccessKeyAsync(string accessKey, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(d => d.AccessKey == accessKey, ct);

    public Task<PagedResult<FiscalDocument>> SearchAsync(PagedQuery query,
        Domain.Common.FiscalDocumentStatus? status = null, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking().AsQueryable();
        if (status.HasValue)
            q = q.Where(d => d.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = $"%{query.Search.Trim()}%";
            q = q.Where(d => EF.Functions.ILike(d.EmitterName, s)
                          || d.AccessKey.Contains(query.Search.Trim())
                          || d.EmitterCnpj.Contains(query.Search.Trim()));
        }
        return q.OrderByDescending(d => d.IssuedAt).ToPagedResultAsync(query, ct);
    }
}

public class IssuedNfeRepository : Repository<IssuedNfe>, IIssuedNfeRepository
{
    public IssuedNfeRepository(AlmoxProDbContext context) : base(context) { }

    public async Task<int> GetLastNumberAsync(int series, CancellationToken ct = default) =>
        await Set.Where(n => n.Series == series).MaxAsync(n => (int?)n.Number, ct) ?? 0;

    public Task<PagedResult<IssuedNfe>> SearchAsync(PagedQuery query, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = $"%{query.Search.Trim()}%";
            q = q.Where(n => EF.Functions.ILike(n.RecipientName, s)
                          || n.AccessKey.Contains(query.Search.Trim())
                          || n.RecipientCnpjCpf.Contains(query.Search.Trim()));
        }
        return q.OrderByDescending(n => n.IssuedAt).ToPagedResultAsync(query, ct);
    }
}
