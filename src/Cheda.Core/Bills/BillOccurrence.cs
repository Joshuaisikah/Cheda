namespace Cheda.Core.Bills;

public sealed class BillOccurrence
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid BillId { get; init; }
    public required DateTimeOffset DueDate { get; init; }
    public required decimal ExpectedAmount { get; init; }
    public decimal? ActualAmount { get; set; }
    public BillOccurrenceStatus Status { get; set; } = BillOccurrenceStatus.Pending;
    public DateTimeOffset? PaidDate { get; set; }
    public string? TransactionCode { get; set; }

    public bool IsOnTime => Status == BillOccurrenceStatus.Paid && PaidDate.HasValue && PaidDate <= DueDate.AddDays(1);
    public bool HasVariance =>
        ActualAmount.HasValue && ExpectedAmount > 0 &&
        Math.Abs(ActualAmount.Value - ExpectedAmount) / ExpectedAmount > 0.10m;
}
