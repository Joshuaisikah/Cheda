namespace Cheda.Core.Analytics;

public sealed record FeeAnalytics
{
    public decimal TotalFees { get; init; }
    public required IReadOnlyList<FeeBreakdownItem> ByType { get; init; }
}

public sealed record FeeBreakdownItem(string Label, decimal Total, int TransactionCount);
