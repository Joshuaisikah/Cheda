namespace Cheda.Core.Bills;

/// <summary>Persistence contract — implemented in the MAUI/SQLite layer.</summary>
public interface IBillStore
{
    IReadOnlyList<RecurringBill> GetBills();
    IReadOnlyList<BillOccurrence> GetOccurrences(Guid billId);
    IReadOnlyList<BillOccurrence> GetAllOccurrences();
    void Save(RecurringBill bill);
    void Save(BillOccurrence occurrence);
    void Delete(Guid billId);
}
