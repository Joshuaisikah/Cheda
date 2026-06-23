using Cheda.Core.Analytics;
using Cheda.Core.Models;

namespace Cheda.Core.Storage;

public interface ITransactionRepository
{
    IReadOnlyList<Transaction> GetAll();
    IReadOnlyList<Transaction> GetInRange(DateRange range);
    Transaction? GetByCode(string transactionCode, TransactionSource source);

    /// <summary>
    /// Inserts the transaction. Returns false without inserting if a record with the same
    /// TransactionCode + Source already exists (deduplication).
    /// </summary>
    bool TryAdd(Transaction transaction);

    /// <summary>
    /// Bulk-inserts, deduplicating by TransactionCode + Source.
    /// Returns the count of newly inserted records.
    /// </summary>
    int AddRange(IEnumerable<Transaction> transactions);

    void Update(Transaction transaction);
    void Delete(Guid id);
    int Count();
}
