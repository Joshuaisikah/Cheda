using Cheda.Core.Budgets;
using Cheda.Core.Models;

namespace Cheda.Core.Notifications;

public interface IAlertEvaluator
{
    IReadOnlyList<AppAlert> Evaluate(
        Transaction newTx,
        IReadOnlyList<Transaction> allTransactions,
        IReadOnlyList<Budget> budgets,
        NotificationSettings settings,
        DateTimeOffset asOf);
}
