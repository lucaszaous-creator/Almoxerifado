using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Application.Services;
using ALMOXPRO.Domain.Common;
using ALMOXPRO.Domain.Entities.Catalog;
using ALMOXPRO.Domain.Entities.Stock;
using ALMOXPRO.Domain.Exceptions;
using ALMOXPRO.Persistence;
using ALMOXPRO.Persistence.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ALMOXPRO.Tests.Integration;

/// <summary>
/// Testes de integração da baixa de estoque com banco real (SQLite em memória),
/// cobrindo a regra FEFO: o lote que vence primeiro sai primeiro.
/// </summary>
public class StockFefoTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AlmoxProDbContext _context;
    private readonly UnitOfWork _uow;
    private readonly StockOperationService _stock;

    public StockFefoTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AlmoxProDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AlmoxProDbContext(options);
        _context.Database.EnsureCreated();

        _uow = new UnitOfWork(_context);
        _stock = new StockOperationService(_uow);

        Seed();
    }

    private Category _category = null!;
    private Product _product = null!;
    private Warehouse _warehouse = null!;

    private void Seed()
    {
        _category = new Category { Name = "Alimentos" };
        _warehouse = new Warehouse { Code = "ALM01", Name = "Central" };
        _product = new Product
        {
            InternalCode = "P000001",
            Name = "Leite integral",
            Category = _category,
            Unit = "L",
            TracksLot = true,
            TracksExpiration = true
        };
        _context.AddRange(_category, _warehouse, _product);
        _context.SaveChanges();
    }

    private async Task<int> AddLotStockAsync(string lotNumber, DateOnly expiration, decimal quantity)
    {
        var lot = await _stock.GetOrCreateLotAsync(_product.Id, lotNumber, expiration);
        await _stock.IncreaseAsync(_product.Id, _warehouse.Id, lot.Id, null,
            quantity, 5m, StockMovementType.Entrada, "Entrada", 1, "ENT-T", 1);
        await _uow.SaveChangesAsync();
        return lot.Id;
    }

    [Fact]
    public async Task Decrease_SemLoteInformado_ConsomePrimeiroOLoteQueVencePrimeiro()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var loteVenceLogo = await AddLotStockAsync("L-CEDO", hoje.AddDays(5), 10m);
        var loteVenceDepois = await AddLotStockAsync("L-TARDE", hoje.AddDays(60), 10m);

        // Baixa 6: deve sair tudo do lote que vence em 5 dias.
        await _stock.DecreaseAsync(_product.Id, _warehouse.Id, null, 6m, 5m,
            StockMovementType.Saida, "Saida", 1, "SAI-T", 1);
        await _uow.SaveChangesAsync();

        var saldoCedo = await _context.StockItems.SingleAsync(s => s.LotId == loteVenceLogo);
        var saldoTarde = await _context.StockItems.SingleAsync(s => s.LotId == loteVenceDepois);
        Assert.Equal(4m, saldoCedo.Quantity);
        Assert.Equal(10m, saldoTarde.Quantity);
    }

    [Fact]
    public async Task Decrease_QuantidadeAtravessaLotes_DivideNaOrdemDeValidade()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var loteVenceLogo = await AddLotStockAsync("L-CEDO", hoje.AddDays(5), 10m);
        var loteVenceDepois = await AddLotStockAsync("L-TARDE", hoje.AddDays(60), 10m);

        // Baixa 14: esvazia o lote de 5 dias (10) e tira 4 do seguinte.
        await _stock.DecreaseAsync(_product.Id, _warehouse.Id, null, 14m, 5m,
            StockMovementType.Saida, "Saida", 1, "SAI-T", 1);
        await _uow.SaveChangesAsync();

        var saldoCedo = await _context.StockItems.SingleAsync(s => s.LotId == loteVenceLogo);
        var saldoTarde = await _context.StockItems.SingleAsync(s => s.LotId == loteVenceDepois);
        Assert.Equal(0m, saldoCedo.Quantity);
        Assert.Equal(6m, saldoTarde.Quantity);

        // Kardex: uma movimentação de saída por lote consumido.
        var movimentos = await _context.StockMovements
            .Where(m => m.Type == StockMovementType.Saida).ToListAsync();
        Assert.Equal(2, movimentos.Count);
        Assert.Contains(movimentos, m => m.LotId == loteVenceLogo && m.Quantity == -10m);
        Assert.Contains(movimentos, m => m.LotId == loteVenceDepois && m.Quantity == -4m);
    }

    [Fact]
    public async Task Decrease_AcimaDoSaldoTotal_LancaInsufficientStock()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        await AddLotStockAsync("L-UNICO", hoje.AddDays(10), 5m);

        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            _stock.DecreaseAsync(_product.Id, _warehouse.Id, null, 8m, 5m,
                StockMovementType.Saida, "Saida", 1, "SAI-T", 1));
    }

    [Fact]
    public async Task Decrease_ProdutoSemLote_ContinuaFuncionando()
    {
        await _stock.IncreaseAsync(_product.Id, _warehouse.Id, null, null,
            20m, 3m, StockMovementType.Entrada, "Entrada", 1, "ENT-T", 1);
        await _uow.SaveChangesAsync();

        await _stock.DecreaseAsync(_product.Id, _warehouse.Id, null, 7m, 3m,
            StockMovementType.Saida, "Saida", 1, "SAI-T", 1);
        await _uow.SaveChangesAsync();

        var total = await _uow.Stock.GetTotalQuantityAsync(_product.Id, _warehouse.Id);
        Assert.Equal(13m, total);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
