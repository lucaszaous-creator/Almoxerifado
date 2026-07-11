using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Domain.Entities.Movements;
using ALMOXPRO.Domain.Entities.Organization;
using ALMOXPRO.Domain.Entities.Security;
using ALMOXPRO.Domain.Entities.Stock;
using ALMOXPRO.Shared.Pagination;

namespace ALMOXPRO.Application.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByLoginAsync(string login, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetWithAccessAsync(int id, CancellationToken ct = default);
    Task<User?> GetByResetTokenAsync(string token, CancellationToken ct = default);
    Task<PagedResult<User>> SearchAsync(PagedQuery query, CancellationToken ct = default);
}

public interface IRoleRepository : IRepository<Role>
{
    Task<Role?> GetWithPermissionsAsync(int id, CancellationToken ct = default);
    Task<List<Role>> GetAllWithPermissionsAsync(CancellationToken ct = default);
}

public interface IPermissionRepository : IRepository<Permission>
{
    Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default);
}

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetWithDetailsAsync(int id, CancellationToken ct = default);
    Task<Product?> GetByAnyCodeAsync(string code, CancellationToken ct = default);
    Task<PagedResult<Product>> SearchAsync(PagedQuery query, int? categoryId = null, CancellationToken ct = default);
    Task<string> GetNextInternalCodeAsync(CancellationToken ct = default);
}

public interface ICategoryRepository : IRepository<Category>
{
    Task<List<Category>> GetAllWithParentAsync(CancellationToken ct = default);
    Task<bool> HasProductsAsync(int categoryId, CancellationToken ct = default);
}

public interface ISupplierRepository : IRepository<Supplier>
{
    Task<PagedResult<Supplier>> SearchAsync(PagedQuery query, CancellationToken ct = default);
}

public interface IWarehouseRepository : IRepository<Warehouse>;

public interface ILocationRepository : IRepository<Location>
{
    Task<List<Location>> GetByWarehouseAsync(int warehouseId, CancellationToken ct = default);
}

public interface ILotRepository : IRepository<Lot>
{
    Task<Lot?> GetByNumberAsync(int productId, string lotNumber, CancellationToken ct = default);
}

public interface IStockRepository
{
    Task<StockItem?> GetItemAsync(int productId, int warehouseId, int? lotId, CancellationToken ct = default);
    Task<List<StockItem>> GetByWarehouseAsync(int warehouseId, CancellationToken ct = default);
    Task<List<StockItem>> GetByProductAsync(int productId, CancellationToken ct = default);
    Task<decimal> GetTotalQuantityAsync(int productId, int? warehouseId = null, CancellationToken ct = default);
    Task<List<StockItem>> GetAllWithDetailsAsync(CancellationToken ct = default);
    Task AddItemAsync(StockItem item, CancellationToken ct = default);
    Task AddMovementAsync(StockMovement movement, CancellationToken ct = default);
    Task<PagedResult<StockMovement>> GetMovementsAsync(PagedQuery query, int? productId = null,
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
}

public interface IMaterialEntryRepository : IRepository<MaterialEntry>
{
    Task<MaterialEntry?> GetWithItemsAsync(int id, CancellationToken ct = default);
    Task<PagedResult<MaterialEntry>> SearchAsync(PagedQuery query, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
}

public interface IMaterialExitRepository : IRepository<MaterialExit>
{
    Task<MaterialExit?> GetWithItemsAsync(int id, CancellationToken ct = default);
    Task<PagedResult<MaterialExit>> SearchAsync(PagedQuery query, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
}

public interface IStockTransferRepository : IRepository<StockTransfer>
{
    Task<StockTransfer?> GetWithItemsAsync(int id, CancellationToken ct = default);
    Task<PagedResult<StockTransfer>> SearchAsync(PagedQuery query, CancellationToken ct = default);
}

public interface IInventoryRepository : IRepository<InventoryCount>
{
    Task<InventoryCount?> GetWithItemsAsync(int id, CancellationToken ct = default);
    Task<PagedResult<InventoryCount>> SearchAsync(PagedQuery query, CancellationToken ct = default);
}

public interface IRequisitionRepository : IRepository<Requisition>
{
    Task<Requisition?> GetWithItemsAsync(int id, CancellationToken ct = default);
    Task<PagedResult<Requisition>> SearchAsync(PagedQuery query, RequisitionStatus? status = null,
        int? sectorId = null, CancellationToken ct = default);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct = default);
    Task<PagedResult<AuditLog>> SearchAsync(PagedQuery query, DateTime? from = null, DateTime? to = null,
        string? tableName = null, CancellationToken ct = default);
}

public interface IAppSettingRepository : IRepository<AppSetting>
{
    Task<AppSetting?> GetByKeyAsync(string key, CancellationToken ct = default);
}

public interface IDocumentSequenceRepository : IRepository<DocumentSequence>
{
    /// <summary>Obtém e incrementa a sequência com bloqueio, garantindo numeração única.</summary>
    Task<string> NextNumberAsync(string key, string prefix, CancellationToken ct = default);
}

public interface ICostCenterRepository : IRepository<CostCenter>;
public interface ISectorRepository : IRepository<Sector>;
public interface IEmployeeRepository : IRepository<Employee>;
