using Cheda.Core.Budgets;
using Cheda.Core.Sms;
using Cheda.Core.Storage;

namespace Cheda.Core.Notifications;

/// <summary>
/// Ties together the import pipeline and the notification layer.
/// After an import result is produced, evaluates each newly-inserted transaction
/// and dispatches the appropriate alerts through INotificationService.
/// </summary>
public sealed class AlertCoordinator
{
    private readonly IAlertEvaluator         _evaluator;
    private readonly INotificationService    _notifications;
    private readonly ITransactionRepository  _transactions;
    private readonly IBudgetStore            _budgets;
    private readonly NotificationSettingsService _settings;

    public AlertCoordinator(
        IAlertEvaluator evaluator,
        INotificationService notifications,
        ITransactionRepository transactions,
        IBudgetStore budgets,
        NotificationSettingsService settings)
    {
        _evaluator     = evaluator;
        _notifications = notifications;
        _transactions  = transactions;
        _budgets       = budgets;
        _settings      = settings;
    }

    public async Task EvaluateAndAlertAsync(ImportResult result, CancellationToken ct = default)
    {
        if (result.Inserted.Count == 0) return;

        var settings = _settings.Load();
        var asOf     = DateTimeOffset.Now;
        var allTx    = _transactions.GetAll();
        var budgets  = _budgets.GetBudgets();

        foreach (var tx in result.Inserted)
        {
            var alerts = _evaluator.Evaluate(tx, allTx, budgets, settings, asOf);
            foreach (var alert in alerts)
                await _notifications.SendAlertAsync(alert, ct);
        }
    }
}
