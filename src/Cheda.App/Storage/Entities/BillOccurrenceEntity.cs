using Cheda.Core.Bills;
using SQLite;

namespace Cheda.App.Storage.Entities;

[Table("BillOccurrences")]
internal sealed class BillOccurrenceEntity
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    [Indexed]
    public string BillId { get; set; } = "";
    public long DueDateTicks { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public int Status { get; set; }
    public long? PaidDateTicks { get; set; }
    public string? TransactionCode { get; set; }

    internal static BillOccurrenceEntity From(BillOccurrence o) => new()
    {
        Id              = o.Id.ToString(),
        BillId          = o.BillId.ToString(),
        DueDateTicks    = o.DueDate.UtcTicks,
        ExpectedAmount  = o.ExpectedAmount,
        ActualAmount    = o.ActualAmount,
        Status          = (int)o.Status,
        PaidDateTicks   = o.PaidDate?.UtcTicks,
        TransactionCode = o.TransactionCode,
    };

    internal BillOccurrence ToDomain() => new()
    {
        Id              = Guid.Parse(Id),
        BillId          = Guid.Parse(BillId),
        DueDate         = new DateTimeOffset(DueDateTicks, TimeSpan.Zero),
        ExpectedAmount  = ExpectedAmount,
        ActualAmount    = ActualAmount,
        Status          = (BillOccurrenceStatus)Status,
        PaidDate        = PaidDateTicks.HasValue ? new DateTimeOffset(PaidDateTicks.Value, TimeSpan.Zero) : null,
        TransactionCode = TransactionCode,
    };
}
