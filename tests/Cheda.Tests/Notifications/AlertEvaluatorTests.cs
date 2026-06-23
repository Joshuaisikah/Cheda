using Cheda.Core.Budgets;
using Cheda.Core.Models;
using Cheda.Core.Notifications;
using FluentAssertions;

namespace Cheda.Tests.Notifications;

public sealed class AlertEvaluatorTests
{
    private static readonly AlertEvaluator  Evaluator = new();
    private static readonly DateTimeOffset  Now       = DateTimeOffset.UtcNow;
    private static readonly NotificationSettings Defaults = new();

    private static Transaction MakeTx(
        TransactionType type  = TransactionType.Sent,
        decimal  amount       = 100m,
        string?  category     = null,
        bool     nonExpense   = false,
        string?  counterparty = null) => new()
    {
        TransactionCode      = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
        Source               = TransactionSource.Mpesa,
        Amount               = amount,
        Type                 = type,
        Category             = category,
        Counterparty         = counterparty,
        Timestamp            = Now,
        RawMessage           = "test",
        IsNonExpenseTransfer = nonExpense,
    };

    // ── Large transaction ─────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_large_transaction_fires_alert()
    {
        var tx = MakeTx(amount: 10_000m);
        var s  = new NotificationSettings { LargeTransactionThreshold = 5_000m };

        var alerts = Evaluator.Evaluate(tx, [tx], [], s, Now);

        alerts.Should().ContainSingle(a => a.Type == AlertType.LargeTransaction);
    }

    [Fact]
    public void Evaluate_small_transaction_no_large_alert()
    {
        var tx = MakeTx(amount: 200m);
        var s  = new NotificationSettings { LargeTransactionThreshold = 5_000m };

        Evaluator.Evaluate(tx, [tx], [], s, Now)
            .Should().NotContain(a => a.Type == AlertType.LargeTransaction);
    }

    [Fact]
    public void Evaluate_large_non_expense_transfer_no_alert()
    {
        var tx = MakeTx(amount: 50_000m, nonExpense: true);

        Evaluator.Evaluate(tx, [tx], [], Defaults, Now)
            .Should().NotContain(a => a.Type == AlertType.LargeTransaction);
    }

    [Fact]
    public void Evaluate_large_transaction_toggle_off_no_alert()
    {
        var tx = MakeTx(amount: 10_000m);
        var s  = new NotificationSettings { LargeTransactionEnabled = false };

        Evaluator.Evaluate(tx, [tx], [], s, Now)
            .Should().NotContain(a => a.Type == AlertType.LargeTransaction);
    }

    [Fact]
    public void Evaluate_large_transaction_body_includes_counterparty()
    {
        var tx = MakeTx(amount: 10_000m, counterparty: "JOHN DOE");
        var s  = new NotificationSettings { LargeTransactionThreshold = 5_000m };

        var alert = Evaluator.Evaluate(tx, [tx], [], s, Now)
            .Single(a => a.Type == AlertType.LargeTransaction);

        alert.Body.Should().Contain("JOHN DOE");
    }

    // ── Fuliza ────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_fuliza_transaction_fires_alert()
    {
        var tx = MakeTx(type: TransactionType.Fuliza, amount: 500m);

        Evaluator.Evaluate(tx, [tx], [], Defaults, Now)
            .Should().ContainSingle(a => a.Type == AlertType.FulizaDrawdown);
    }

    [Fact]
    public void Evaluate_non_fuliza_no_fuliza_alert()
    {
        var tx = MakeTx(type: TransactionType.Sent, amount: 500m);

        Evaluator.Evaluate(tx, [tx], [], Defaults, Now)
            .Should().NotContain(a => a.Type == AlertType.FulizaDrawdown);
    }

    [Fact]
    public void Evaluate_fuliza_toggle_off_no_alert()
    {
        var tx = MakeTx(type: TransactionType.Fuliza, amount: 500m);
        var s  = new NotificationSettings { FulizaAlertEnabled = false };

        Evaluator.Evaluate(tx, [tx], [], s, Now)
            .Should().NotContain(a => a.Type == AlertType.FulizaDrawdown);
    }

    // ── Budget breach ─────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_budget_at_amber_fires_breach_alert()
    {
        const string cat = "Food";
        var budget = new Budget { Category = cat, MonthlyLimit = 1_000m };
        // 80% spend → Amber (threshold 75%)
        var tx = MakeTx(type: TransactionType.Sent, amount: 800m, category: cat);

        Evaluator.Evaluate(tx, [tx], [budget], Defaults, Now)
            .Should().ContainSingle(a => a.Type == AlertType.BudgetBreach);
    }

    [Fact]
    public void Evaluate_budget_below_amber_no_breach_alert()
    {
        const string cat = "Food";
        var budget = new Budget { Category = cat, MonthlyLimit = 1_000m };
        // 30% spend → Green
        var tx = MakeTx(type: TransactionType.Sent, amount: 300m, category: cat);

        Evaluator.Evaluate(tx, [tx], [budget], Defaults, Now)
            .Should().NotContain(a => a.Type == AlertType.BudgetBreach);
    }

    [Fact]
    public void Evaluate_budget_breach_toggle_off_no_alert()
    {
        const string cat = "Food";
        var budget = new Budget { Category = cat, MonthlyLimit = 1_000m };
        var tx = MakeTx(type: TransactionType.Sent, amount: 950m, category: cat);
        var s  = new NotificationSettings { BudgetBreachEnabled = false };

        Evaluator.Evaluate(tx, [tx], [budget], s, Now)
            .Should().NotContain(a => a.Type == AlertType.BudgetBreach);
    }

    [Fact]
    public void Evaluate_uncategorised_transaction_no_budget_alert()
    {
        var budget = new Budget { Category = "Food", MonthlyLimit = 500m };
        var tx = MakeTx(type: TransactionType.Sent, amount: 500m, category: null);

        Evaluator.Evaluate(tx, [tx], [budget], Defaults, Now)
            .Should().NotContain(a => a.Type == AlertType.BudgetBreach);
    }

    // ── New transaction (off by default) ──────────────────────────────────────

    [Fact]
    public void Evaluate_new_transaction_enabled_fires_alert()
    {
        var tx = MakeTx(type: TransactionType.Received, amount: 1_000m);
        var s  = new NotificationSettings { NewTransactionEnabled = true };

        Evaluator.Evaluate(tx, [tx], [], s, Now)
            .Should().ContainSingle(a => a.Type == AlertType.NewTransaction);
    }

    [Fact]
    public void Evaluate_new_transaction_disabled_by_default_no_alert()
    {
        var tx = MakeTx(type: TransactionType.Received, amount: 1_000m);

        Evaluator.Evaluate(tx, [tx], [], Defaults, Now)
            .Should().NotContain(a => a.Type == AlertType.NewTransaction);
    }

    [Fact]
    public void Evaluate_deposit_no_new_transaction_alert_unrecognised_verb()
    {
        // Deposit / MShwari / Reversal don't produce NewTransaction alerts.
        var tx = MakeTx(type: TransactionType.Deposit, amount: 1_000m);
        var s  = new NotificationSettings { NewTransactionEnabled = true };

        Evaluator.Evaluate(tx, [tx], [], s, Now)
            .Should().NotContain(a => a.Type == AlertType.NewTransaction);
    }
}
