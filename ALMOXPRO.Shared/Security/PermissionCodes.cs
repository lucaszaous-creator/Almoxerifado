namespace ALMOXPRO.Shared.Security;

/// <summary>
/// Códigos de permissão do sistema (RBAC).
/// Cada tela/ação verifica um destes códigos antes de executar.
/// </summary>
public static class PermissionCodes
{
    // Dashboard
    public const string DashboardView = "dashboard.view";

    // Produtos
    public const string ProductsView = "products.view";
    public const string ProductsCreate = "products.create";
    public const string ProductsEdit = "products.edit";
    public const string ProductsDelete = "products.delete";
    public const string ProductsViewCosts = "products.view_costs";

    // Categorias
    public const string CategoriesView = "categories.view";
    public const string CategoriesManage = "categories.manage";

    // Fornecedores
    public const string SuppliersView = "suppliers.view";
    public const string SuppliersManage = "suppliers.manage";

    // Localizações
    public const string LocationsView = "locations.view";
    public const string LocationsManage = "locations.manage";

    // Movimentações
    public const string StockEntry = "stock.entry";
    public const string StockExit = "stock.exit";
    public const string StockTransfer = "stock.transfer";

    // Inventário
    public const string InventoryView = "inventory.view";
    public const string InventoryExecute = "inventory.execute";
    public const string InventoryAdjust = "inventory.adjust";

    // Requisições
    public const string RequisitionsView = "requisitions.view";
    public const string RequisitionsCreate = "requisitions.create";
    public const string RequisitionsFulfill = "requisitions.fulfill";

    // Setores, funcionários e centros de custo
    public const string OrganizationView = "organization.view";
    public const string OrganizationManage = "organization.manage";

    // Compras
    public const string PurchasesView = "purchases.view";
    public const string PurchasesManage = "purchases.manage";

    // Fiscal (NF-e)
    public const string FiscalView = "fiscal.view";
    public const string FiscalManifest = "fiscal.manifest";

    // Relatórios
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";

    // Usuários e permissões
    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";

    // Auditoria
    public const string AuditView = "audit.view";

    // Configurações
    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";

    // Backup
    public const string BackupExecute = "backup.execute";
    public const string BackupRestore = "backup.restore";

    /// <summary>Todas as permissões conhecidas, com descrição para seed e telas de administração.</summary>
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        [DashboardView] = "Visualizar dashboard",
        [ProductsView] = "Visualizar produtos",
        [ProductsCreate] = "Cadastrar produtos",
        [ProductsEdit] = "Editar produtos",
        [ProductsDelete] = "Excluir produtos",
        [ProductsViewCosts] = "Visualizar custos",
        [CategoriesView] = "Visualizar categorias",
        [CategoriesManage] = "Gerenciar categorias",
        [SuppliersView] = "Visualizar fornecedores",
        [SuppliersManage] = "Gerenciar fornecedores",
        [LocationsView] = "Visualizar localizações",
        [LocationsManage] = "Gerenciar localizações",
        [StockEntry] = "Entrada de materiais",
        [StockExit] = "Saída de materiais",
        [StockTransfer] = "Transferência entre almoxarifados",
        [InventoryView] = "Visualizar inventário",
        [InventoryExecute] = "Executar inventário",
        [InventoryAdjust] = "Ajustar inventário",
        [RequisitionsView] = "Visualizar requisições",
        [RequisitionsCreate] = "Criar requisições",
        [RequisitionsFulfill] = "Atender e cancelar requisições",
        [OrganizationView] = "Visualizar setores e equipe",
        [OrganizationManage] = "Gerenciar setores e equipe",
        [PurchasesView] = "Visualizar compras",
        [PurchasesManage] = "Gerenciar compras",
        [FiscalView] = "Visualizar notas fiscais (NF-e)",
        [FiscalManifest] = "Manifestar NF-e (ciência, confirmação, recusa)",
        [ReportsView] = "Visualizar relatórios",
        [ReportsExport] = "Exportar relatórios",
        [UsersView] = "Visualizar usuários",
        [UsersManage] = "Gerenciar usuários",
        [RolesManage] = "Gerenciar perfis e permissões",
        [AuditView] = "Visualizar auditoria",
        [SettingsView] = "Visualizar configurações",
        [SettingsManage] = "Gerenciar configurações",
        [BackupExecute] = "Executar backup",
        [BackupRestore] = "Restaurar backup",
    };
}
