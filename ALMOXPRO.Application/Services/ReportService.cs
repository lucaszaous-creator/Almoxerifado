using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Shared.Pagination;

namespace ALMOXPRO.Application.Services;

public enum ReportKind
{
    Produtos,
    Estoque,
    Entradas,
    Saidas,
    Movimentacoes,
    Fornecedores,
    ProdutosVencidos,
    EstoqueMinimo,
    ValorEmEstoque,
    CurvaAbc,
    ConsumoPorSetor,
    ConsumoPorFuncionario
}

public interface IReportService
{
    Task<ReportTable> BuildAsync(ReportKind kind, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
}

/// <summary>Monta as tabelas de dados dos relatórios; a exportação é feita pelo IReportExporter.</summary>
public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockQueryService _stockQuery;

    public ReportService(IUnitOfWork uow, IStockQueryService stockQuery)
    {
        _uow = uow;
        _stockQuery = stockQuery;
    }

    public async Task<ReportTable> BuildAsync(ReportKind kind, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var period = from.HasValue || to.HasValue
            ? $"Período: {from?.ToString("dd/MM/yyyy") ?? "início"} a {to?.ToString("dd/MM/yyyy") ?? "hoje"}"
            : null;

        return kind switch
        {
            ReportKind.Produtos => await ProductsAsync(ct),
            ReportKind.Estoque or ReportKind.ValorEmEstoque => await StockAsync(ct),
            ReportKind.Entradas => await EntriesAsync(from, to, period, ct),
            ReportKind.Saidas => await ExitsAsync(from, to, period, ct),
            ReportKind.Movimentacoes => await MovementsAsync(from, to, period, ct),
            ReportKind.Fornecedores => await SuppliersAsync(ct),
            ReportKind.ProdutosVencidos => await ExpiredAsync(ct),
            ReportKind.EstoqueMinimo => await BelowMinimumAsync(ct),
            ReportKind.CurvaAbc => await AbcAsync(ct),
            ReportKind.ConsumoPorSetor => await ConsumptionAsync(bySector: true, from, to, period, ct),
            ReportKind.ConsumoPorFuncionario => await ConsumptionAsync(bySector: false, from, to, period, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private async Task<ReportTable> ProductsAsync(CancellationToken ct)
    {
        var products = await _uow.Products.SearchAsync(new PagedQuery { Page = 1, PageSize = 200 }, null, ct);
        var rows = products.Items.Select(p => (IReadOnlyList<string>)
        [
            p.InternalCode, p.Name, p.Category.Name, p.Unit,
            p.MinStock.ToString("N2"), p.MaxStock.ToString("N2"),
            p.Status == EntityStatus.Ativo ? "Ativo" : "Inativo"
        ]).ToList();
        return new ReportTable("Relatório de Produtos", null,
            ["Código", "Produto", "Categoria", "Un.", "Est. Mín.", "Est. Máx.", "Status"], rows);
    }

    private async Task<ReportTable> StockAsync(CancellationToken ct)
    {
        var balances = await _stockQuery.GetBalancesAsync(ct);
        var rows = balances.Where(b => b.Quantity != 0).Select(b => (IReadOnlyList<string>)
        [
            b.InternalCode, b.ProductName, b.WarehouseName, b.LotNumber ?? "-",
            b.Quantity.ToString("N2"), b.AverageCost.ToString("C2"), b.TotalValue.ToString("C2")
        ]).ToList();
        var total = balances.Sum(b => b.TotalValue);
        return new ReportTable("Relatório de Estoque", $"Valor total em estoque: {total:C2}",
            ["Código", "Produto", "Almoxarifado", "Lote", "Qtde", "Custo Médio", "Valor Total"], rows);
    }

    private async Task<ReportTable> EntriesAsync(DateTime? from, DateTime? to, string? period, CancellationToken ct)
    {
        var page = await _uow.Entries.SearchAsync(new PagedQuery { Page = 1, PageSize = 200 }, from, to, ct);
        var rows = page.Items.Select(e => (IReadOnlyList<string>)
        [
            e.Number, e.EntryDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), e.Type.ToString(),
            e.Supplier?.TradeName ?? e.Supplier?.CompanyName ?? "-",
            e.DocumentNumber ?? "-", e.Items.Count.ToString(), e.TotalValue.ToString("C2")
        ]).ToList();
        return new ReportTable("Relatório de Entradas", period,
            ["Número", "Data", "Tipo", "Fornecedor", "Documento", "Itens", "Valor"], rows);
    }

    private async Task<ReportTable> ExitsAsync(DateTime? from, DateTime? to, string? period, CancellationToken ct)
    {
        var page = await _uow.Exits.SearchAsync(new PagedQuery { Page = 1, PageSize = 200 }, from, to, ct);
        var rows = page.Items.Select(e => (IReadOnlyList<string>)
        [
            e.Number, e.ExitDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), e.Type.ToString(),
            e.Sector?.Name ?? "-", e.Employee?.Name ?? "-", e.CostCenter?.Name ?? "-",
            e.Items.Count.ToString(), e.TotalValue.ToString("C2")
        ]).ToList();
        return new ReportTable("Relatório de Saídas", period,
            ["Número", "Data", "Tipo", "Setor", "Funcionário", "Centro de Custo", "Itens", "Valor"], rows);
    }

    private async Task<ReportTable> MovementsAsync(DateTime? from, DateTime? to, string? period, CancellationToken ct)
    {
        var page = await _stockQuery.GetMovementsAsync(new PagedQuery { Page = 1, PageSize = 200, Descending = true }, null, from, to, ct);
        var rows = page.Items.Select(m => (IReadOnlyList<string>)
        [
            m.MovementDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), m.ProductName, m.WarehouseName,
            m.Type.ToString(), m.Quantity.ToString("N2"), m.BalanceAfter.ToString("N2"), m.DocumentNumber ?? "-"
        ]).ToList();
        return new ReportTable("Relatório de Movimentações", period,
            ["Data", "Produto", "Almoxarifado", "Tipo", "Qtde", "Saldo", "Documento"], rows);
    }

    private async Task<ReportTable> SuppliersAsync(CancellationToken ct)
    {
        var page = await _uow.Suppliers.SearchAsync(new PagedQuery { Page = 1, PageSize = 200 }, ct);
        var rows = page.Items.Select(s => (IReadOnlyList<string>)
        [
            s.CompanyName, s.TradeName ?? "-", FormatCnpj(s.Cnpj), s.City ?? "-", s.State ?? "-",
            s.Phone ?? "-", s.Email ?? "-"
        ]).ToList();
        return new ReportTable("Relatório de Fornecedores", null,
            ["Razão Social", "Fantasia", "CNPJ", "Cidade", "UF", "Telefone", "E-mail"], rows);
    }

    private async Task<ReportTable> ExpiredAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var balances = await _stockQuery.GetBalancesAsync(ct);
        var rows = balances
            .Where(b => b.Quantity > 0 && b.ExpirationDate.HasValue && b.ExpirationDate < today)
            .Select(b => (IReadOnlyList<string>)
            [
                b.InternalCode, b.ProductName, b.WarehouseName, b.LotNumber ?? "-",
                b.ExpirationDate!.Value.ToString("dd/MM/yyyy"), b.Quantity.ToString("N2")
            ]).ToList();
        return new ReportTable("Produtos Vencidos", $"Referência: {today:dd/MM/yyyy}",
            ["Código", "Produto", "Almoxarifado", "Lote", "Validade", "Qtde"], rows);
    }

    private async Task<ReportTable> BelowMinimumAsync(CancellationToken ct)
    {
        var balances = await _stockQuery.GetBalancesAsync(ct);
        var rows = balances
            .GroupBy(b => new { b.ProductId, b.InternalCode, b.ProductName, b.MinStock })
            .Select(g => new { g.Key, Quantity = g.Sum(x => x.Quantity) })
            .Where(x => x.Key.MinStock > 0 && x.Quantity < x.Key.MinStock)
            .Select(x => (IReadOnlyList<string>)
            [
                x.Key.InternalCode, x.Key.ProductName,
                x.Quantity.ToString("N2"), x.Key.MinStock.ToString("N2"),
                (x.Key.MinStock - x.Quantity).ToString("N2")
            ]).ToList();
        return new ReportTable("Produtos Abaixo do Estoque Mínimo", null,
            ["Código", "Produto", "Saldo", "Mínimo", "Repor"], rows);
    }

    private async Task<ReportTable> AbcAsync(CancellationToken ct)
    {
        var balances = await _stockQuery.GetBalancesAsync(ct);
        var ranked = balances
            .GroupBy(b => new { b.InternalCode, b.ProductName })
            .Select(g => new { g.Key, Value = g.Sum(x => x.TotalValue) })
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .ToList();
        var total = ranked.Sum(x => x.Value);
        decimal accumulated = 0;
        var rows = new List<IReadOnlyList<string>>();
        foreach (var item in ranked)
        {
            accumulated += item.Value;
            var percent = total == 0 ? 0 : accumulated / total * 100;
            var cls = percent <= 80 ? "A" : percent <= 95 ? "B" : "C";
            rows.Add([item.Key.InternalCode, item.Key.ProductName, item.Value.ToString("C2"), $"{percent:N1}%", cls]);
        }
        return new ReportTable("Curva ABC (valor em estoque)", $"Valor total: {total:C2}",
            ["Código", "Produto", "Valor", "% Acum.", "Classe"], rows);
    }

    private async Task<ReportTable> ConsumptionAsync(bool bySector, DateTime? from, DateTime? to, string? period, CancellationToken ct)
    {
        var page = await _uow.Exits.SearchAsync(new PagedQuery { Page = 1, PageSize = 200 }, from, to, ct);
        var rows = page.Items
            .GroupBy(e => bySector ? e.Sector?.Name ?? "(sem setor)" : e.Employee?.Name ?? "(sem funcionário)")
            .Select(g => new { Name = g.Key, Count = g.Count(), Value = g.Sum(e => e.TotalValue) })
            .OrderByDescending(x => x.Value)
            .Select(x => (IReadOnlyList<string>)[x.Name, x.Count.ToString(), x.Value.ToString("C2")])
            .ToList();
        return new ReportTable(
            bySector ? "Consumo por Setor" : "Consumo por Funcionário", period,
            [bySector ? "Setor" : "Funcionário", "Saídas", "Valor"], rows);
    }

    private static string FormatCnpj(string cnpj) =>
        cnpj.Length == 14
            ? $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}"
            : cnpj;
}
