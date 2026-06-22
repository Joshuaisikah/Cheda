namespace Cheda.Core.Budgets;

public sealed record BudgetStatus
{
    public required Budget Budget { get; init; }
    public decimal AmountSpent { get; init; }
    public decimal AmountRemaining { get; init; }
    public double ProgressPercent { get; init; }
    public AlertLevel AlertLevel { get; init; }
    public bool IsOverspent => AlertLevel == AlertLevel.Overspent;
}
