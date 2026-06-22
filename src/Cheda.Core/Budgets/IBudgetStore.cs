namespace Cheda.Core.Budgets;

/// <summary>Persistence contract — implemented in the MAUI/SQLite layer.</summary>
public interface IBudgetStore
{
    IReadOnlyList<Budget> GetBudgets();
    void Save(Budget budget);
    void Delete(Guid budgetId);
}
