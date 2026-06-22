using Cheda.Core.Analytics;
using Cheda.Core.Models;

namespace Cheda.Core.Budgets;

/// <summary>
/// Pure, stateless budget engine. Computes spend-vs-limit for each budget
/// by summing expense transactions in the given range that match the budget's category.
/// </summary>
public sealed class BudgetEngine : IBudgetEngine
{
    public BudgetStatus GetStatus(
        Budget budget, IReadOnlyList<Transaction> transactions, DateRange range)
    {
        var spent = transactions
            .Where(t =>
                range.Contains(t.Timestamp) &&
                !t.IsNonExpenseTransfer &&
                t.Type is TransactionType.Sent
                       or TransactionType.PaidTill
                       or TransactionType.PaidPaybill
                       or TransactionType.Airtime &&
                string.Equals(t.Category, budget.Category, StringComparison.OrdinalIgnoreCase))
            .Sum(t => t.Amount);

        var limit    = budget.MonthlyLimit;
        var progress = limit > 0 ? (double)(spent / limit * 100m) : 0.0;
        var remaining = limit - spent;

        var alert = progress switch
        {
            > 100.0 => AlertLevel.Overspent,
            _ when progress >= budget.RedThresholdPercent   => AlertLevel.Red,
            _ when progress >= budget.AmberThresholdPercent => AlertLevel.Amber,
            _                                               => AlertLevel.None,
        };

        return new BudgetStatus
        {
            Budget          = budget,
            AmountSpent     = spent,
            AmountRemaining = remaining,
            ProgressPercent = progress,
            AlertLevel      = alert,
        };
    }

    public IReadOnlyList<BudgetStatus> GetStatuses(
        IReadOnlyList<Budget> budgets, IReadOnlyList<Transaction> transactions, DateRange range) =>
        budgets
            .Where(b => b.IsEnabled)
            .Select(b => GetStatus(b, transactions, range))
            .ToList();

    public IReadOnlyList<BudgetStatus> GetBreachedBudgets(
        IReadOnlyList<Budget> budgets, IReadOnlyList<Transaction> transactions, DateRange range) =>
        GetStatuses(budgets, transactions, range)
            .Where(s => s.AlertLevel != AlertLevel.None)
            .OrderByDescending(s => s.AlertLevel)
            .ThenByDescending(s => s.ProgressPercent)
            .ToList();
}
