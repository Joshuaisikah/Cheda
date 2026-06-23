using Cheda.Core.Budgets;

namespace Cheda.Tests.Storage.InMemory;

public sealed class InMemoryBudgetStore : IBudgetStore
{
    private readonly List<Budget> _budgets;

    public InMemoryBudgetStore(IReadOnlyList<Budget>? initial = null) =>
        _budgets = [.. initial ?? []];

    public IReadOnlyList<Budget> GetBudgets() => _budgets;

    public void Save(Budget budget)
    {
        var idx = _budgets.FindIndex(b => b.Id == budget.Id);
        if (idx >= 0) _budgets[idx] = budget;
        else _budgets.Add(budget);
    }

    public void Delete(Guid budgetId) =>
        _budgets.RemoveAll(b => b.Id == budgetId);
}
