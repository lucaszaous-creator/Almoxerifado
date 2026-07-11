using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Configuration;
using ALMOXPRO.Domain.Entities.Fiscal;
using ALMOXPRO.Domain.Entities.Movements;
using ALMOXPRO.Domain.Entities.Organization;
using ALMOXPRO.Domain.Entities.Security;
using ALMOXPRO.Domain.Entities.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ALMOXPRO.Persistence.Context;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.Property(u => u.Name).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Login).HasMaxLength(50).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.Phone).HasMaxLength(30);
        builder.Property(u => u.Department).HasMaxLength(100);
        builder.Property(u => u.JobTitle).HasMaxLength(100);
        builder.Property(u => u.PasswordResetToken).HasMaxLength(64);
        builder.HasIndex(u => u.Login).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Ignore(u => u.IsLockedOut);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(300);
        builder.HasIndex(r => r.Name).IsUnique();
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.Property(p => p.Code).HasMaxLength(60).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(200).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });
        builder.HasOne(rp => rp.Role).WithMany(r => r.Permissions).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(rp => rp.Permission).WithMany(p => p.Roles).HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });
        builder.HasOne(ur => ur.User).WithMany(u => u.Roles).HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ur => ur.Role).WithMany(r => r.Users).HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("user_permissions");
        builder.HasKey(up => new { up.UserId, up.PermissionId });
        builder.HasOne(up => up.User).WithMany(u => u.Permissions).HasForeignKey(up => up.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(up => up.Permission).WithMany().HasForeignKey(up => up.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.Property(a => a.UserName).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Computer).HasMaxLength(100).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(45).IsRequired();
        builder.Property(a => a.Screen).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(50).IsRequired();
        builder.Property(a => a.TableName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.RecordId).HasMaxLength(50).IsRequired();
        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.TableName, a.RecordId });
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(300);
        builder.HasOne(c => c.Parent).WithMany(c => c.Children).HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.Name, c.ParentId }).IsUnique();
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");
        builder.Property(s => s.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.TradeName).HasMaxLength(200);
        builder.Property(s => s.Cnpj).HasMaxLength(14).IsRequired();
        builder.Property(s => s.StateRegistration).HasMaxLength(30);
        builder.Property(s => s.Address).HasMaxLength(300);
        builder.Property(s => s.City).HasMaxLength(100);
        builder.Property(s => s.State).HasMaxLength(2);
        builder.Property(s => s.ZipCode).HasMaxLength(10);
        builder.Property(s => s.Phone).HasMaxLength(30);
        builder.Property(s => s.Email).HasMaxLength(150);
        builder.Property(s => s.ContactName).HasMaxLength(150);
        builder.HasIndex(s => s.Cnpj).IsUnique();
    }
}

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses");
        builder.Property(w => w.Code).HasMaxLength(20).IsRequired();
        builder.Property(w => w.Name).HasMaxLength(100).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(300);
        builder.HasIndex(w => w.Code).IsUnique();
    }
}

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");
        builder.Property(l => l.Building).HasMaxLength(50).IsRequired();
        builder.Property(l => l.Floor).HasMaxLength(20);
        builder.Property(l => l.Corridor).HasMaxLength(20);
        builder.Property(l => l.Shelf).HasMaxLength(20);
        builder.Property(l => l.Rack).HasMaxLength(20);
        builder.Property(l => l.Position).HasMaxLength(20);
        builder.HasOne(l => l.Warehouse).WithMany().HasForeignKey(l => l.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(l => l.Code);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.Property(p => p.InternalCode).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Barcode).HasMaxLength(50);
        builder.Property(p => p.QrCode).HasMaxLength(100);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.Brand).HasMaxLength(100);
        builder.Property(p => p.Manufacturer).HasMaxLength(150);
        builder.Property(p => p.Unit).HasMaxLength(10).IsRequired();
        builder.Property(p => p.Dimensions).HasMaxLength(60);
        builder.Property(p => p.AssetNumber).HasMaxLength(50);
        builder.Property(p => p.Notes).HasMaxLength(1000);
        builder.Property(p => p.Weight).HasPrecision(18, 4);
        builder.Property(p => p.MinStock).HasPrecision(18, 4);
        builder.Property(p => p.MaxStock).HasPrecision(18, 4);
        builder.Property(p => p.AverageCost).HasPrecision(18, 4);
        builder.Property(p => p.LastCost).HasPrecision(18, 4);
        builder.HasIndex(p => p.InternalCode).IsUnique();
        builder.HasIndex(p => p.Barcode).IsUnique().HasFilter("\"Barcode\" IS NOT NULL");
        builder.HasIndex(p => p.Name);
        builder.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.MainSupplier).WithMany().HasForeignKey(p => p.MainSupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.DefaultLocation).WithMany().HasForeignKey(p => p.DefaultLocationId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable("lots");
        builder.Property(l => l.LotNumber).HasMaxLength(50).IsRequired();
        builder.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.ProductId, l.LotNumber }).IsUnique();
    }
}

public class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items");
        builder.Property(s => s.Quantity).HasPrecision(18, 4);
        builder.HasOne(s => s.Product).WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Warehouse).WithMany().HasForeignKey(s => s.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Location).WithMany().HasForeignKey(s => s.LocationId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(s => s.Lot).WithMany().HasForeignKey(s => s.LotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.ProductId, s.WarehouseId, s.LotId }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("ck_stock_items_quantity", "\"Quantity\" >= 0"));
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");
        builder.Property(m => m.Quantity).HasPrecision(18, 4);
        builder.Property(m => m.UnitCost).HasPrecision(18, 4);
        builder.Property(m => m.BalanceAfter).HasPrecision(18, 4);
        builder.Property(m => m.DocumentType).HasMaxLength(30).IsRequired();
        builder.Property(m => m.DocumentNumber).HasMaxLength(30);
        builder.Property(m => m.Notes).HasMaxLength(500);
        builder.HasOne(m => m.Product).WithMany().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Warehouse).WithMany().HasForeignKey(m => m.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Lot).WithMany().HasForeignKey(m => m.LotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => m.MovementDate);
        builder.HasIndex(m => new { m.ProductId, m.MovementDate });
    }
}

public class MaterialEntryConfiguration : IEntityTypeConfiguration<MaterialEntry>
{
    public void Configure(EntityTypeBuilder<MaterialEntry> builder)
    {
        builder.ToTable("material_entries");
        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.DocumentNumber).HasMaxLength(60);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.HasIndex(e => e.Number).IsUnique();
        builder.HasIndex(e => e.EntryDate);
        builder.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(e => e.TotalValue);
    }
}

public class MaterialEntryItemConfiguration : IEntityTypeConfiguration<MaterialEntryItem>
{
    public void Configure(EntityTypeBuilder<MaterialEntryItem> builder)
    {
        builder.ToTable("material_entry_items");
        builder.Property(i => i.Quantity).HasPrecision(18, 4);
        builder.Property(i => i.UnitCost).HasPrecision(18, 4);
        builder.Property(i => i.LotNumber).HasMaxLength(50);
        builder.Property(i => i.Notes).HasMaxLength(500);
        builder.HasOne(i => i.MaterialEntry).WithMany(e => e.Items).HasForeignKey(i => i.MaterialEntryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Location).WithMany().HasForeignKey(i => i.LocationId).OnDelete(DeleteBehavior.SetNull);
        builder.Ignore(i => i.Total);
    }
}

public class MaterialExitConfiguration : IEntityTypeConfiguration<MaterialExit>
{
    public void Configure(EntityTypeBuilder<MaterialExit> builder)
    {
        builder.ToTable("material_exits");
        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.WorkOrder).HasMaxLength(60);
        builder.Property(e => e.Reason).HasMaxLength(300);
        builder.Property(e => e.ResponsibleName).HasMaxLength(150);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.HasIndex(e => e.Number).IsUnique();
        builder.HasIndex(e => e.ExitDate);
        builder.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Sector).WithMany().HasForeignKey(e => e.SectorId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(e => e.TotalValue);
        builder.Ignore(e => e.IsReversed);
    }
}

public class MaterialExitItemConfiguration : IEntityTypeConfiguration<MaterialExitItem>
{
    public void Configure(EntityTypeBuilder<MaterialExitItem> builder)
    {
        builder.ToTable("material_exit_items");
        builder.Property(i => i.Quantity).HasPrecision(18, 4);
        builder.Property(i => i.UnitCost).HasPrecision(18, 4);
        builder.Property(i => i.Notes).HasMaxLength(500);
        builder.HasOne(i => i.MaterialExit).WithMany(e => e.Items).HasForeignKey(i => i.MaterialExitId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Lot).WithMany().HasForeignKey(i => i.LotId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("stock_transfers");
        builder.Property(t => t.Number).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(1000);
        builder.HasIndex(t => t.Number).IsUnique();
        builder.HasOne(t => t.SourceWarehouse).WithMany().HasForeignKey(t => t.SourceWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.DestinationWarehouse).WithMany().HasForeignKey(t => t.DestinationWarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockTransferItemConfiguration : IEntityTypeConfiguration<StockTransferItem>
{
    public void Configure(EntityTypeBuilder<StockTransferItem> builder)
    {
        builder.ToTable("stock_transfer_items");
        builder.Property(i => i.Quantity).HasPrecision(18, 4);
        builder.HasOne(i => i.StockTransfer).WithMany(t => t.Items).HasForeignKey(i => i.StockTransferId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Lot).WithMany().HasForeignKey(i => i.LotId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryCountConfiguration : IEntityTypeConfiguration<InventoryCount>
{
    public void Configure(EntityTypeBuilder<InventoryCount> builder)
    {
        builder.ToTable("inventory_counts");
        builder.Property(i => i.Number).HasMaxLength(20).IsRequired();
        builder.Property(i => i.Notes).HasMaxLength(1000);
        builder.HasIndex(i => i.Number).IsUnique();
        builder.HasOne(i => i.Warehouse).WithMany().HasForeignKey(i => i.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryCountItemConfiguration : IEntityTypeConfiguration<InventoryCountItem>
{
    public void Configure(EntityTypeBuilder<InventoryCountItem> builder)
    {
        builder.ToTable("inventory_count_items");
        builder.Property(i => i.SystemQuantity).HasPrecision(18, 4);
        builder.Property(i => i.CountedQuantity).HasPrecision(18, 4);
        builder.HasOne(i => i.InventoryCount).WithMany(c => c.Items).HasForeignKey(i => i.InventoryCountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Lot).WithMany().HasForeignKey(i => i.LotId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(i => i.Difference);
        builder.Ignore(i => i.HasDifference);
    }
}

public class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.ToTable("cost_centers");
        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();
    }
}

public class SectorConfiguration : IEntityTypeConfiguration<Sector>
{
    public void Configure(EntityTypeBuilder<Sector> builder)
    {
        builder.ToTable("sectors");
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.Name).IsUnique();
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.RegistrationNumber).HasMaxLength(30);
        builder.HasOne(e => e.Sector).WithMany().HasForeignKey(e => e.SectorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("app_settings");
        builder.Property(s => s.Key).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Value).HasMaxLength(2000);
        builder.Property(s => s.Description).HasMaxLength(300);
        builder.HasIndex(s => s.Key).IsUnique();
    }
}

public class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("document_sequences");
        builder.Property(s => s.Key).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Prefix).HasMaxLength(10).IsRequired();
        builder.HasIndex(s => s.Key).IsUnique();
    }
}

public class RequisitionConfiguration : IEntityTypeConfiguration<Requisition>
{
    public void Configure(EntityTypeBuilder<Requisition> builder)
    {
        builder.ToTable("requisitions");
        builder.Property(r => r.Number).HasMaxLength(20).IsRequired();
        builder.Property(r => r.RequesterName).HasMaxLength(150);
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.HasIndex(r => r.Number).IsUnique();
        builder.HasIndex(r => r.RequestDate);
        builder.HasIndex(r => r.Status);
        builder.HasOne(r => r.Warehouse).WithMany().HasForeignKey(r => r.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Sector).WithMany().HasForeignKey(r => r.SectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.MaterialExit).WithMany().HasForeignKey(r => r.MaterialExitId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(r => r.IsOpen);
    }
}

public class RequisitionItemConfiguration : IEntityTypeConfiguration<RequisitionItem>
{
    public void Configure(EntityTypeBuilder<RequisitionItem> builder)
    {
        builder.ToTable("requisition_items");
        builder.Property(i => i.QuantityRequested).HasPrecision(18, 4);
        builder.Property(i => i.QuantityFulfilled).HasPrecision(18, 4);
        builder.Property(i => i.Notes).HasMaxLength(500);
        builder.HasOne(i => i.Requisition).WithMany(r => r.Items).HasForeignKey(i => i.RequisitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FiscalDocumentConfiguration : IEntityTypeConfiguration<FiscalDocument>
{
    public void Configure(EntityTypeBuilder<FiscalDocument> builder)
    {
        builder.ToTable("fiscal_documents");
        builder.Property(d => d.AccessKey).HasMaxLength(44).IsRequired();
        builder.Property(d => d.Nsu).HasMaxLength(15).IsRequired();
        builder.Property(d => d.EmitterCnpj).HasMaxLength(14).IsRequired();
        builder.Property(d => d.EmitterName).HasMaxLength(200).IsRequired();
        builder.Property(d => d.TotalValue).HasPrecision(18, 2);
        builder.Property(d => d.ManifestJustification).HasMaxLength(500);
        builder.Property(d => d.Xml).HasColumnType("text");
        builder.HasIndex(d => d.AccessKey).IsUnique();
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.IssuedAt);
    }
}
