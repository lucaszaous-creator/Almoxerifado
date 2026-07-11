namespace ALMOXPRO.Application.Interfaces;

/// <summary>
/// Unidade de trabalho: agrega os repositórios e controla a transação.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }
    IPermissionRepository Permissions { get; }
    IProductRepository Products { get; }
    ICategoryRepository Categories { get; }
    ISupplierRepository Suppliers { get; }
    IWarehouseRepository Warehouses { get; }
    ILocationRepository Locations { get; }
    ILotRepository Lots { get; }
    IStockRepository Stock { get; }
    IMaterialEntryRepository Entries { get; }
    IMaterialExitRepository Exits { get; }
    IStockTransferRepository Transfers { get; }
    IInventoryRepository Inventories { get; }
    IRequisitionRepository Requisitions { get; }
    IFiscalDocumentRepository FiscalDocuments { get; }
    IAuditLogRepository AuditLogs { get; }
    IAppSettingRepository Settings { get; }
    IDocumentSequenceRepository Sequences { get; }
    ICostCenterRepository CostCenters { get; }
    ISectorRepository Sectors { get; }
    IEmployeeRepository Employees { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default);
}
