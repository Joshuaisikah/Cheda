namespace Cheda.Core.Analytics;

public sealed record FulizaAnalytics
{
    public int DrawdownCount { get; init; }
    public decimal TotalBorrowed { get; init; }
    public decimal TotalFees { get; init; }
    /// Most recent Fuliza balance reported in SMS (BalanceAfter on Fuliza transactions).
    /// Null if no Fuliza transactions exist.
    public decimal? EstimatedOutstanding { get; init; }
    /// Average drawdowns per month over the range.
    public double UsageFrequencyPerMonth { get; init; }
}
