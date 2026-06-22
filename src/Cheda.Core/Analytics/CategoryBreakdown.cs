namespace Cheda.Core.Analytics;

public sealed record CategoryBreakdown
{
    public required string Category { get; init; }
    public decimal Total { get; init; }
    public double Percentage { get; init; }
    public int TransactionCount { get; init; }
}
