using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Persistence.Context;
using ALMOXPRO.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ALMOXPRO.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AlmoxProDbContext _context;

    public UnitOfWork(AlmoxProDbContext context)
    {
        _context = context;
        Users = new UserRepository(context);
        Roles = new RoleRepository(context);
        Permissions = new PermissionRepository(context);
        Products = new ProductRepository(context);
        Categories = new CategoryRepository(context);
        Suppliers = new SupplierRepository(context);
        Warehouses = new WarehouseRepository(context);
        Locations = new LocationRepository(context);
        Lots = new LotRepository(context);
        Stock = new StockRepository(context);
        Entries = new MaterialEntryRepository(context);
        Exits = new MaterialExitRepository(context);
        Transfers = new StockTransferRepository(context);
        Inventories = new InventoryRepository(context);
        Requisitions = new RequisitionRepository(context);
        AuditLogs = new AuditLogRepository(context);
        Settings = new AppSettingRepository(context);
        Sequences = new DocumentSequenceRepository(context);
        CostCenters = new CostCenterRepository(context);
        Sectors = new SectorRepository(context);
        Employees = new EmployeeRepository(context);
    }

    public IUserRepository Users { get; }
    public IRoleRepository Roles { get; }
    public IPermissionRepository Permissions { get; }
    public IProductRepository Products { get; }
    public ICategoryRepository Categories { get; }
    public ISupplierRepository Suppliers { get; }
    public IWarehouseRepository Warehouses { get; }
    public ILocationRepository Locations { get; }
    public ILotRepository Lots { get; }
    public IStockRepository Stock { get; }
    public IMaterialEntryRepository Entries { get; }
    public IMaterialExitRepository Exits { get; }
    public IStockTransferRepository Transfers { get; }
    public IInventoryRepository Inventories { get; }
    public IRequisitionRepository Requisitions { get; }
    public IAuditLogRepository AuditLogs { get; }
    public IAppSettingRepository Settings { get; }
    public IDocumentSequenceRepository Sequences { get; }
    public ICostCenterRepository CostCenters { get; }
    public ISectorRepository Sectors { get; }
    public IEmployeeRepository Employees { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default)
    {
        // Se já houver transação ativa (operações aninhadas), apenas executa.
        if (_context.Database.CurrentTransaction is not null)
        {
            await operation();
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            await operation();
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }

    public void Dispose() => _context.Dispose();
}
