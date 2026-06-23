using Cheda.Core.Analytics;
using Cheda.Core.Budgets;
using Cheda.Core.Models;

namespace Cheda.Core.Notifications;

/// <summary>
/// Pure, stateless evaluator — given a newly-inserted transaction and current app state,
/// returns the set of alerts that should be surfaced to the user.
/// Quiet hours and daily-cap enforcement are the responsibility of INotificationService.
/// </summary>
public sealed class AlertEvaluator : IAlertEvaluator
{
    private readonly BudgetEngine _budgetEngine = new();

    public IReadOnlyList<AppAlert> Evaluate(
        Transaction newTx,
        IReadOnlyList<Transaction> allTransactions,
        IReadOnlyList<Budget> budgets,
        NotificationSettings settings,
        DateTimeOffset asOf)
    {
        var alerts = new List<AppAlert>();

        if (settings.LargeTransactionEnabled)
        {
            var a = CheckLargeTransaction(newTx, settings);
            if (a is not null) alerts.Add(a);
        }

        if (settings.FulizaAlertEnabled)
        {
            var a = CheckFuliza(newTx);
            if (a is not null) alerts.Add(a);
        }

        if (settings.BudgetBreachEnabled && budgets.Count > 0)
            alerts.AddRange(CheckBudgetBreaches(newTx, allTransactions, budgets, asOf));

        if (settings.NewTransactionEnabled)
        {
            var a = CheckNewTransaction(newTx);
            if (a is not null) alerts.Add(a);
        }

        return alerts;
    }

    private static AppAlert? CheckLargeTransaction(Transaction tx, NotificationSettings s)
    {
        if (tx.IsNonExpenseTransfer) return null;
        if (tx.Amount < s.LargeTransactionThreshold) return null;
        return new AppAlert
        {
            Type  = AlertType.LargeTransaction,
            Title = "Large transaction",
            Body  = tx.Counterparty is not null
                ? $"Ksh {tx.Amount:N0} to {tx.Counterparty}"
                : $"Ksh {tx.Amount:N0} — {tx.TransactionCode}",
        };
    }

    private static AppAlert? CheckFuliza(Transaction tx)
    {
        if (tx.Type != TransactionType.Fuliza) return null;
        return new AppAlert
        {
            Type  = AlertType.FulizaDrawdown,
            Title = "Fuliza used",
            Body  = $"You borrowed Ksh {tx.Amount:N0} via Fuliza M-PESA.",
        };
    }

    private IEnumerable<AppAlert> CheckBudgetBreaches(
        Transaction newTx,
        IReadOnlyList<Transaction> allTx,
        IReadOnlyList<Budget> budgets,
        DateTimeOffset asOf)
    {
        if (newTx.Category is null) yield break;

        var range = DateRange.ForMonth(asOf.Year, asOf.Month, asOf.Offset);

        foreach (var budget in budgets.Where(b =>
            b.IsEnabled &&
            string.Equals(b.Category, newTx.Category, StringComparison.OrdinalIgnoreCase)))
        {
            var status = _budgetEngine.GetStatus(budget, allTx, range);
            if (status.AlertLevel == AlertLevel.None) continue;

            var levelText = status.AlertLevel switch
            {
                AlertLevel.Overspent => "exceeded",
                AlertLevel.Red       => "90% reached",
                AlertLevel.Amber     => "75% reached",
                _                    => null,
            };
            if (levelText is null) continue;

            yield return new AppAlert
            {
                Type     = AlertType.BudgetBreach,
                Title    = $"{budget.Category} budget {levelText}",
                Body     = $"Spent Ksh {status.AmountSpent:N0} of Ksh {budget.MonthlyLimit:N0} ({status.ProgressPercent:F0}%).",
                Category = budget.Category,
            };
        }
    }

    private static AppAlert? CheckNewTransaction(Transaction tx)
    {
        var verb = tx.Type switch
        {
            TransactionType.Received => "received",
            TransactionType.Sent
                or TransactionType.PaidTill
                or TransactionType.PaidPaybill
                or TransactionType.Airtime => "sent",
            _ => null,
        };
        if (verb is null) return null;

        return new AppAlert
        {
            Type  = AlertType.NewTransaction,
            Title = $"M-PESA Ksh {tx.Amount:N0} {verb}",
            Body  = tx.Counterparty is not null
                ? $"{(verb == "received" ? "From" : "To")} {tx.Counterparty}"
                : tx.TransactionCode,
        };
    }
}
