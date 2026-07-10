namespace ALMOXPRO.Application.DTOs;

public record DashboardDto(
    int TotalProducts,
    decimal StockTotalValue,
    int EntriesToday,
    int ExitsToday,
    int CriticalProducts,
    int ExpiredProducts,
    int BelowMinimum,
    IReadOnlyList<AbcItemDto> AbcCurve,
    IReadOnlyList<RecentMovementDto> RecentMovements,
    IReadOnlyList<ChartPointDto> MovementsLast30Days);

public record AbcItemDto(string ProductName, decimal Value, decimal AccumulatedPercent, char Class);

public record RecentMovementDto(DateTime Date, string Type, string ProductName, decimal Quantity, string User);

public record ChartPointDto(string Label, decimal Entries, decimal Exits);
