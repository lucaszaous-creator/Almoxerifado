using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Domain.Entities.Movements;
using ALMOXPRO.Domain.Entities.Organization;
using ALMOXPRO.Domain.Entities.Security;
using ALMOXPRO.Domain.Entities.Stock;
using Microsoft.EntityFrameworkCore;

namespace ALMOXPRO.Persistence.Context;

public class AlmoxProDbContext : DbContext
{
    public AlmoxProDbContext(DbContextOptions<AlmoxProDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<MaterialEntry> MaterialEntries => Set<MaterialEntry>();
    public DbSet<MaterialExit> MaterialExits => Set<MaterialExit>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();
    public DbSet<Requisition> Requisitions => Set<Requisition>();

    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlmoxProDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
