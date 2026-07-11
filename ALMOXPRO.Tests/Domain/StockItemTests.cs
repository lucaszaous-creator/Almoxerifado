using ALMOXPRO.Domain.Entities.Stock;
using ALMOXPRO.Domain.Exceptions;
using Xunit;

namespace ALMOXPRO.Tests.Domain;

public class StockItemTests
{
    [Fact]
    public void Increase_SomaQuantidadeAoSaldo()
    {
        var item = new StockItem();

        item.Increase(10m);
        item.Increase(2.5m);

        Assert.Equal(12.5m, item.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Increase_QuantidadeInvalida_LancaDomainException(decimal quantity)
    {
        var item = new StockItem();

        Assert.Throws<DomainException>(() => item.Increase(quantity));
    }

    [Fact]
    public void Decrease_SubtraiQuantidadeDoSaldo()
    {
        var item = new StockItem();
        item.Increase(10m);

        item.Decrease(4m);

        Assert.Equal(6m, item.Quantity);
    }

    [Fact]
    public void Decrease_MaiorQueSaldo_LancaInsufficientStockException()
    {
        var item = new StockItem();
        item.Increase(3m);

        Assert.Throws<InsufficientStockException>(() => item.Decrease(5m));
        Assert.Equal(3m, item.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Decrease_QuantidadeInvalida_LancaDomainException(decimal quantity)
    {
        var item = new StockItem();
        item.Increase(10m);

        Assert.Throws<DomainException>(() => item.Decrease(quantity));
    }

    [Fact]
    public void SetQuantity_DefineSaldoDiretamente()
    {
        var item = new StockItem();
        item.Increase(10m);

        item.SetQuantity(7m);

        Assert.Equal(7m, item.Quantity);
    }

    [Fact]
    public void SetQuantity_Negativo_LancaDomainException()
    {
        var item = new StockItem();

        Assert.Throws<DomainException>(() => item.SetQuantity(-1m));
    }

    [Fact]
    public void SetQuantity_Zero_EhPermitido()
    {
        var item = new StockItem();
        item.Increase(5m);

        item.SetQuantity(0m);

        Assert.Equal(0m, item.Quantity);
    }
}
