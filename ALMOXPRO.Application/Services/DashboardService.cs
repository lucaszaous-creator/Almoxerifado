using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Shared.Pagination;

namespace ALMOXPRO.Application.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _uow;

    public DashboardService(IUnitOfWork uow) => _uow = uow;

    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var totalProducts = await _uow.Products.CountAsync(p => p.Status == EntityStatus.Ativo, ct);
        var entriesToday = await _uow.Entries.CountAsync(e => e.EntryDate >= today, ct);
        var exitsToday = await _uow.Exits.CountAsync(e => e.ExitDate >= today, ct);

        var balances = await _uow.Stock.GetAllWithDetailsAsync(ct);
        var todayDate = DateOnly.FromDateTime(DateTime.Now);

        var stockTotalValue = balances.Sum(b => b.Quantity * b.Product.AverageCost);

        var byProduct = balances
            .GroupBy(b => b.Product)
            .Select(g => new { Product = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        var belowMinimum = byProduct.Count(p => p.Product.MinStock > 0 && p.Quantity < p.Product.MinStock);
        var critical = byProduct.Count(p => p.Product.MinStock > 0 && p.Quantity <= p.Product.MinStock / 2);

        var expired = balances
            .Where(b => b.Quantity > 0 && b.Lot?.ExpirationDate is not null && b.Lot.ExpirationDate < todayDate)
            .Select(b => b.ProductId)
            .Distinct()
            .Count();

        // Lotes que vencem nos próximos 30 dias (alerta antecipado de validade).
        var expiringLimit = todayDate.AddDays(30);
        var expiringSoon = balances
            .Where(b => b.Quantity > 0 && b.Lot?.ExpirationDate is not null
                     && b.Lot.ExpirationDate >= todayDate && b.Lot.ExpirationDate <= expiringLimit)
            .Select(b => b.ProductId)
            .Distinct()
            .Count();

        // Curva ABC pelo valor em estoque.
        var abc = new List<AbcItemDto>();
        var ranked = byProduct
            .Select(p => new { p.Product.Name, Value = p.Quantity * p.Product.AverageCost })
            .Where(p => p.Value > 0)
            .OrderByDescending(p => p.Value)
            .ToList();
        var totalValue = ranked.Sum(p => p.Value);
        decimal accumulated = 0;
        foreach (var item in ranked.Take(50))
        {
            accumulated += item.Value;
            var percent = totalValue == 0 ? 0 : accumulated / totalValue * 100;
            var cls = percent <= 80 ? 'A' : percent <= 95 ? 'B' : 'C';
            abc.Add(new AbcItemDto(item.Name, Math.Round(item.Value, 2), Math.Round(percent, 1), cls));
        }

        var recentPage = await _uow.Stock.GetMovementsAsync(
            new PagedQuery { Page = 1, PageSize = 10, Descending = true }, null, null, null, ct);
        var users = (await _uow.Users.GetAllAsync(ct)).ToDictionary(u => u.Id, u => u.Name);
        var recent = recentPage.Items.Select(m => new RecentMovementDto(
            m.MovementDate, m.Type.ToString(), m.Product.Name, m.Quantity,
            users.GetValueOrDefault(m.UserId, "-"))).ToList();

        // Entradas x saídas dos últimos 30 dias para o gráfico.
        var from = DateTime.UtcNow.AddDays(-30);
        var movementsPage = await _uow.Stock.GetMovementsAsync(
            new PagedQuery { Page = 1, PageSize = 200 }, null, from, null, ct);
        var chart = movementsPage.Items
            .GroupBy(m => m.MovementDate.ToLocalTime().Date)
            .OrderBy(g => g.Key)
            .Select(g => new ChartPointDto(
                g.Key.ToString("dd/MM"),
                g.Where(m => m.Quantity > 0).Sum(m => m.Quantity),
                Math.Abs(g.Where(m => m.Quantity < 0).Sum(m => m.Quantity))))
            .ToList();

        return new DashboardDto(
            totalProducts,
            Math.Round(stockTotalValue, 2),
            entriesToday,
            exitsToday,
            critical,
            expired,
            expiringSoon,
            belowMinimum,
            abc,
            recent,
            chart);
    }
}
