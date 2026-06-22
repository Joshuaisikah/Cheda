using Cheda.Core.Analytics;
using Cheda.Core.Models;

namespace Cheda.Core.Budgets;

public interface IBudgetEngine
{
    BudgetStatus GetStatus(Budget budget, IReadOnlyList<Transaction> transactions, DateRange range);

    IReadOnlyList<BudgetStatus> GetStatuses(
        IReadOnlyList<Budget> budgets, IReadOnlyList<Transaction> transactions, DateRange range);

    IReadOnlyList<BudgetStatus> GetBreachedBudgets(
        IReadOnlyList<Budget> budgets, IReadOnlyList<Transaction> transactions, DateRange range);
}
