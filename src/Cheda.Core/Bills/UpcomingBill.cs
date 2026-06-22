namespace Cheda.Core.Bills;

public sealed record UpcomingBill
{
    public required RecurringBill Bill { get; init; }
    public required DateTimeOffset DueDate { get; init; }
    public decimal ExpectedAmount { get; init; }
    public int DaysUntilDue { get; init; }
    public bool IsDueToday => DaysUntilDue == 0;
    public bool IsOverdue => DaysUntilDue < 0;
}
