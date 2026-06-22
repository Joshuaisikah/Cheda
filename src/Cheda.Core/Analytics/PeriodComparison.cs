namespace Cheda.Core.Analytics;

public sealed record PeriodComparison
{
    public required PeriodSummary Current { get; init; }
    public required PeriodSummary Previous { get; init; }
    public decimal ExpenseChange { get; init; }
    public double ExpenseChangePercent { get; init; }
    public decimal IncomeChange { get; init; }
    public double IncomeChangePercent { get; init; }
}
