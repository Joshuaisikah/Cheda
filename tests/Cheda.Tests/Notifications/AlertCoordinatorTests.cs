using Cheda.Core.Budgets;
using Cheda.Core.Models;
using Cheda.Core.Notifications;
using Cheda.Core.Sms;
using Cheda.Tests.Storage.InMemory;
using FluentAssertions;

namespace Cheda.Tests.Notifications;

public sealed class AlertCoordinatorTests
{
    private static Transaction MakeTx(
        TransactionType type   = TransactionType.Sent,
        decimal         amount = 100m,
        string?         code   = null) => new()
    {
        TransactionCode = code ?? Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
        Source          = TransactionSource.Mpesa,
        Amount          = amount,
        Type            = type,
        Timestamp       = DateTimeOffset.UtcNow,
        RawMessage      = "test",
    };

    private static AlertCoordinator Build(
        FakeNotificationService notif,
        InMemoryTransactionRepository repo,
        IReadOnlyList<Budget>? budgets = null,
        NotificationSettings? settings = null)
    {
        var budgetStore  = new InMemoryBudgetStore(budgets ?? []);
        var settingsRepo = new InMemorySettingsRepository();
        var settingsSvc  = new NotificationSettingsService(settingsRepo);
        if (settings is not null) settingsSvc.Save(settings);
        return new AlertCoordinator(new AlertEvaluator(), notif, repo, budgetStore, settingsSvc);
    }

    [Fact]
    public async Task EvaluateAndAlertAsync_empty_inserted_list_fires_no_alerts()
    {
        var notif = new FakeNotificationService();
        var coord = Build(notif, new InMemoryTransactionRepository());

        await coord.EvaluateAndAlertAsync(new ImportResult());

        notif.SentAlerts.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAndAlertAsync_large_transaction_fires_large_alert()
    {
        var tx   = MakeTx(amount: 10_000m);
        var repo = new InMemoryTransactionRepository();
        repo.TryAdd(tx);

        var notif = new FakeNotificationService();
        var coord = Build(notif, repo,
            settings: new NotificationSettings { LargeTransactionThreshold = 5_000m });

        await coord.EvaluateAndAlertAsync(new ImportResult { Inserted = [tx] });

        notif.SentAlerts.Should().ContainSingle(a => a.Type == AlertType.LargeTransaction);
    }

    [Fact]
    public async Task EvaluateAndAlertAsync_fuliza_fires_fuliza_alert()
    {
        var tx   = MakeTx(type: TransactionType.Fuliza, amount: 300m);
        var repo = new InMemoryTransactionRepository();
        repo.TryAdd(tx);

        var notif = new FakeNotificationService();
        var coord = Build(notif, repo);

        await coord.EvaluateAndAlertAsync(new ImportResult { Inserted = [tx] });

        notif.SentAlerts.Should().ContainSingle(a => a.Type == AlertType.FulizaDrawdown);
    }

    [Fact]
    public async Task EvaluateAndAlertAsync_small_normal_transaction_fires_new_tx_alert()
    {
        // NewTransaction notifications are ON by default — even small sends fire them.
        var tx   = MakeTx(type: TransactionType.Sent, amount: 50m);
        var repo = new InMemoryTransactionRepository();
        repo.TryAdd(tx);

        var notif = new FakeNotificationService();
        var coord = Build(notif, repo);

        await coord.EvaluateAndAlertAsync(new ImportResult { Inserted = [tx] });

        notif.SentAlerts.Should().ContainSingle(a => a.Type == AlertType.NewTransaction);
    }

    [Fact]
    public async Task EvaluateAndAlertAsync_multiple_inserted_fires_alert_for_each_large_one()
    {
        var tx1  = MakeTx(amount: 50m);
        var tx2  = MakeTx(type: TransactionType.Fuliza, amount: 200m);
        var repo = new InMemoryTransactionRepository();
        repo.TryAdd(tx1);
        repo.TryAdd(tx2);

        var notif = new FakeNotificationService();
        var coord = Build(notif, repo);

        await coord.EvaluateAndAlertAsync(new ImportResult { Inserted = [tx1, tx2] });

        notif.SentAlerts.Should().ContainSingle(a => a.Type == AlertType.FulizaDrawdown);
        notif.SentAlerts.Should().NotContain(a => a.Type == AlertType.LargeTransaction);
    }
}
