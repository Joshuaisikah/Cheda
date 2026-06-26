using Cheda.Core.Analytics;
using Cheda.Core.Models;
using Cheda.Core.Storage;

namespace Cheda.Tests.Storage.InMemory;

/// <summary>
/// In-memory implementation used in unit tests. Mirrors the contract and dedup behaviour
/// of SqliteTransactionRepository without any SQLite dependency.
/// </summary>
public sealed class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly Dictionary<string, Transaction> _store = [];

    private static string DedupKey(string code, TransactionSource source) =>
        $"{code}:{(int)source}";

    public IReadOnlyList<Transaction> GetAll() =>
        [.. _store.Values.OrderByDescending(t => t.Timestamp)];

    public IReadOnlyList<Transaction> GetInRange(DateRange range) =>
        [.. _store.Values
               .Where(t => range.Contains(t.Timestamp))
               .OrderByDescending(t => t.Timestamp)];

    public Transaction? GetByCode(string transactionCode, TransactionSource source) =>
        _store.TryGetValue(DedupKey(transactionCode, source), out var t) ? t : null;

    public bool TryAdd(Transaction transaction)
    {
        var key = DedupKey(transaction.TransactionCode, transaction.Source);
        if (_store.ContainsKey(key)) return false;
        _store[key] = transaction;
        return true;
    }

    public int AddRange(IEnumerable<Transaction> transactions)
    {
        var inserted = 0;
        foreach (var t in transactions)
            if (TryAdd(t)) inserted++;
        return inserted;
    }

    public void Update(Transaction transaction)
    {
        var key = DedupKey(transaction.TransactionCode, transaction.Source);
        if (_store.ContainsKey(key))
            _store[key] = transaction;
    }

    public void Delete(Guid id)
    {
        var key = _store.FirstOrDefault(kv => kv.Value.Id == id).Key;
        if (key is not null)
            _store.Remove(key);
    }

    public void DeleteAll() => _store.Clear();

    public int Count() => _store.Count;
}
