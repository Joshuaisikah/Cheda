using Cheda.App.Storage.Entities;
using Cheda.Core.Bills;

namespace Cheda.App.Storage;

public sealed class SqliteBillStore : IBillStore
{
    private readonly DatabaseService _db;

    public SqliteBillStore(DatabaseService db) => _db = db;

    public IReadOnlyList<RecurringBill> GetBills() =>
        _db.Db.Table<RecurringBillEntity>()
              .ToList()
              .Select(e => e.ToDomain())
              .ToList();

    public IReadOnlyList<BillOccurrence> GetOccurrences(Guid billId)
    {
        var idStr = billId.ToString();
        return _db.Db.Table<BillOccurrenceEntity>()
                     .Where(e => e.BillId == idStr)
                     .ToList()
                     .Select(e => e.ToDomain())
                     .ToList();
    }

    public IReadOnlyList<BillOccurrence> GetAllOccurrences() =>
        _db.Db.Table<BillOccurrenceEntity>()
              .ToList()
              .Select(e => e.ToDomain())
              .ToList();

    public void Save(RecurringBill bill)
    {
        var entity = RecurringBillEntity.From(bill);
        if (_db.Db.Table<RecurringBillEntity>().FirstOrDefault(e => e.Id == entity.Id) is null)
            _db.Db.Insert(entity);
        else
            _db.Db.Update(entity);
    }

    public void Save(BillOccurrence occurrence)
    {
        var entity = BillOccurrenceEntity.From(occurrence);
        if (_db.Db.Table<BillOccurrenceEntity>().FirstOrDefault(e => e.Id == entity.Id) is null)
            _db.Db.Insert(entity);
        else
            _db.Db.Update(entity);
    }

    public void Delete(Guid billId)
    {
        var idStr = billId.ToString();
        _db.Db.RunInTransaction(() =>
        {
            _db.Db.Delete<RecurringBillEntity>(idStr);
            var occurrences = _db.Db.Table<BillOccurrenceEntity>()
                                    .Where(e => e.BillId == idStr)
                                    .ToList();
            foreach (var o in occurrences)
                _db.Db.Delete<BillOccurrenceEntity>(o.Id);
        });
    }
}
