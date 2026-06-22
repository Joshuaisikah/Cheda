namespace Cheda.Core.Analytics;

public sealed record TrendPoint
{
    public required DateTimeOffset PeriodStart { get; init; }
    public decimal Income { get; init; }
    public decimal Expenses { get; init; }
    public decimal Net { get; init; }
    public decimal Fees { get; init; }
}
