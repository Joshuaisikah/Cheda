using Cheda.App.Storage.Entities;
using Cheda.Core.Budgets;

namespace Cheda.App.Storage;

public sealed class SqliteBudgetStore : IBudgetStore
{
    private readonly DatabaseService _db;

    public SqliteBudgetStore(DatabaseService db) => _db = db;

    public IReadOnlyList<Budget> GetBudgets() =>
        _db.Db.Table<BudgetEntity>()
              .ToList()
              .Select(e => e.ToDomain())
              .ToList();

    public void Save(Budget budget)
    {
        var entity = BudgetEntity.From(budget);
        if (_db.Db.Table<BudgetEntity>().FirstOrDefault(e => e.Id == entity.Id) is null)
            _db.Db.Insert(entity);
        else
            _db.Db.Update(entity);
    }

    public void Delete(Guid budgetId) =>
        _db.Db.Delete<BudgetEntity>(budgetId.ToString());
}
