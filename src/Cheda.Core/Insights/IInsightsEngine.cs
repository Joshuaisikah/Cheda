using Cheda.Core.Analytics;
using Cheda.Core.Bills;
using Cheda.Core.Budgets;
using Cheda.Core.Models;

namespace Cheda.Core.Insights;

public interface IInsightsEngine
{
    IReadOnlyList<Insight> Generate(
        IReadOnlyList<Transaction> transactions,
        DateRange currentPeriod,
        DateRange previousPeriod,
        IReadOnlyList<Budget> budgets,
        IReadOnlyList<RecurringBill> bills,
        IReadOnlyList<BillOccurrence> billOccurrences,
        DateTimeOffset asOf,
        string? currentPeriodLabel  = null,
        string? previousPeriodLabel = null);
}
