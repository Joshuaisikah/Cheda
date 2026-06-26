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
            Title = $"Large transaction — Ksh {tx.Amount:N0}",
            Body  = tx.Counterparty is not null
                ? $"Sent to {tx.Counterparty}. New balance: {FormatBalance(tx)}"
                : $"Transaction {tx.TransactionCode}. New balance: {FormatBalance(tx)}",
        };
    }

    private static AppAlert? CheckFuliza(Transaction tx)
    {
        if (tx.Type != TransactionType.Fuliza) return null;
        return new AppAlert
        {
            Type  = AlertType.FulizaDrawdown,
            Title = $"Fuliza — Ksh {tx.Amount:N0} borrowed",
            Body  = $"You used Fuliza M-PESA. Repay early to avoid interest charges.",
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

            var (levelText, emoji) = status.AlertLevel switch
            {
                AlertLevel.Overspent => ("exceeded",    "🔴"),
                AlertLevel.Red       => ("at 90%",      "🟠"),
                AlertLevel.Amber     => ("at 75%",      "🟡"),
                _                    => (null, null),
            };
            if (levelText is null) continue;

            var remaining = status.AmountRemaining >= 0
                ? $"Ksh {status.AmountRemaining:N0} left"
                : $"Ksh {-status.AmountRemaining:N0} over budget";

            yield return new AppAlert
            {
                Type     = AlertType.BudgetBreach,
                Title    = $"{emoji} {budget.Category} {levelText}",
                Body     = $"Spent Ksh {status.AmountSpent:N0} of Ksh {budget.MonthlyLimit:N0} — {remaining}.",
                Category = budget.Category,
            };
        }
    }

    private static AppAlert? CheckNewTransaction(Transaction tx)
    {
        var (verb, prep) = tx.Type switch
        {
            TransactionType.Received                                => ("received", "From"),
            TransactionType.Sent                                    => ("sent",     "To"),
            TransactionType.PaidTill or TransactionType.PaidPaybill => ("paid",     "To"),
            TransactionType.Airtime                                 => ("for airtime", ""),
            _ => (null, null),
        };
        if (verb is null) return null;

        var counterLine = tx.Counterparty is not null && prep != ""
            ? $"{prep} {tx.Counterparty}"
            : tx.Counterparty ?? tx.TransactionCode;
        var balanceLine = FormatBalance(tx);
        var body = balanceLine is not null
            ? $"{counterLine}. Balance: {balanceLine}"
            : counterLine;

        return new AppAlert
        {
            Type  = AlertType.NewTransaction,
            Title = $"Ksh {tx.Amount:N0} {verb}",
            Body  = body,
        };
    }

    private static string? FormatBalance(Transaction tx) =>
        tx.BalanceAfter.HasValue ? $"Ksh {tx.BalanceAfter.Value:N0}" : null;
}
