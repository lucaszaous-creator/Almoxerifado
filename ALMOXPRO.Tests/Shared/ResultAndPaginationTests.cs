using ALMOXPRO.Shared.Pagination;
using ALMOXPRO.Shared.Results;
using Xunit;

namespace ALMOXPRO.Tests.Shared;

public class ResultTests
{
    [Fact]
    public void Success_NaoTemErros()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Empty(result.Errors);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Failure_ExpoePrimeiroErro()
    {
        var result = Result.Failure(["erro 1", "erro 2"]);

        Assert.True(result.IsFailure);
        Assert.Equal("erro 1", result.Error);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void SuccessGenerico_ExpoeValor()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void FailureGenerico_AcessarValor_LancaInvalidOperation()
    {
        var result = Result.Failure<int>("falhou");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}

public class PagedResultTests
{
    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(1, 25, 1)]
    [InlineData(25, 25, 1)]
    [InlineData(26, 25, 2)]
    [InlineData(100, 25, 4)]
    public void TotalPages_ArredondaParaCima(int total, int pageSize, int expected)
    {
        var result = new PagedResult<int> { TotalCount = total, PageSize = pageSize, Page = 1 };

        Assert.Equal(expected, result.TotalPages);
    }

    [Fact]
    public void HasPrevious_E_HasNext_RefletemPosicao()
    {
        var middle = new PagedResult<int> { TotalCount = 100, PageSize = 25, Page = 2 };

        Assert.True(middle.HasPrevious);
        Assert.True(middle.HasNext);

        var first = new PagedResult<int> { TotalCount = 100, PageSize = 25, Page = 1 };
        Assert.False(first.HasPrevious);

        var last = new PagedResult<int> { TotalCount = 100, PageSize = 25, Page = 4 };
        Assert.False(last.HasNext);
    }

    [Fact]
    public void PagedQuery_LimitaPageSize()
    {
        var query = new PagedQuery { PageSize = 10_000 };
        Assert.Equal(200, query.PageSize);

        query.PageSize = 0;
        Assert.Equal(1, query.PageSize);
    }
}
