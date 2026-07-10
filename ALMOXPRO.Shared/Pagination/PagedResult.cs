namespace ALMOXPRO.Shared.Pagination;

/// <summary>Resultado paginado para listagens.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>Parâmetros de consulta paginada.</summary>
public class PagedQuery
{
    private const int MaxPageSize = 200;
    private int _pageSize = 25;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    public string? Search { get; set; }
    public string? OrderBy { get; set; }
    public bool Descending { get; set; }
}
