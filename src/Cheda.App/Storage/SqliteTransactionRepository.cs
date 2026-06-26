using Cheda.App.Storage.Entities;
using Cheda.Core.Analytics;
using Cheda.Core.Models;
using Cheda.Core.Storage;
using SQLite;

namespace Cheda.App.Storage;

public sealed class SqliteTransactionRepository : ITransactionRepository
{
    private SQLiteConnection Db => _db.Db;
    private readonly DatabaseService _db;

    public SqliteTransactionRepository(DatabaseService db) => _db = db;

    public IReadOnlyList<Transaction> GetAll() =>
        Db.Table<TransactionEntity>()
          .OrderByDescending(e => e.TimestampTicks)
          .ToList()
          .Select(e => e.ToDomain())
          .ToList();

    public IReadOnlyList<Transaction> GetInRange(DateRange range)
    {
        var startTicks = range.Start.UtcTicks;
        var endTicks   = range.End.UtcTicks;

        return Db.Table<TransactionEntity>()
                 .Where(e => e.TimestampTicks >= startTicks && e.TimestampTicks < endTicks)
                 .OrderByDescending(e => e.TimestampTicks)
                 .ToList()
                 .Select(e => e.ToDomain())
                 .ToList();
    }

    public Transaction? GetByCode(string transactionCode, TransactionSource source)
    {
        var key = TransactionEntity.MakeDedupKey(transactionCode, source);
        return Db.Table<TransactionEntity>()
                 .FirstOrDefault(e => e.DedupKey == key)
                 ?.ToDomain();
    }

    public bool TryAdd(Transaction transaction)
    {
        var entity = TransactionEntity.From(transaction);
        var existing = Db.Table<TransactionEntity>()
                         .FirstOrDefault(e => e.DedupKey == entity.DedupKey);
        if (existing is not null) return false;
        Db.Insert(entity);
        return true;
    }

    public int AddRange(IEnumerable<Transaction> transactions)
    {
        var inserted = 0;
        Db.RunInTransaction(() =>
        {
            foreach (var t in transactions)
            {
                var entity = TransactionEntity.From(t);
                var exists = Db.Table<TransactionEntity>()
                               .FirstOrDefault(e => e.DedupKey == entity.DedupKey) is not null;
                if (exists) continue;
                Db.Insert(entity);
                inserted++;
            }
        });
        return inserted;
    }

    public void Update(Transaction transaction) =>
        Db.Update(TransactionEntity.From(transaction));

    public void Delete(Guid id) =>
        Db.Delete<TransactionEntity>(id.ToString());

    public void DeleteAll() =>
        Db.DeleteAll<TransactionEntity>();

    public int Count() =>
        Db.Table<TransactionEntity>().Count();
}
