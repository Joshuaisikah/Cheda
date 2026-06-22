namespace Cheda.Core.Bills;

public sealed class RecurringBill
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Label { get; init; }

    /// Paybill number, till number, or counterparty keyword — interpreted by PaymentKeyType.
    public required string PaymentKey { get; init; }
    public BillPaymentKeyType PaymentKeyType { get; init; } = BillPaymentKeyType.Paybill;

    /// Account/reference for paybill payments (e.g. meter number).
    public string? AccountReference { get; init; }

    public required decimal ExpectedAmount { get; init; }
    public required BillSchedule Schedule { get; init; }

    /// Day of month the bill is due (1–28). Used for Monthly/Annual schedules.
    public int DayOfMonth { get; init; } = 1;

    /// Days before due date to show a reminder.
    public int ReminderLeadDays { get; set; } = 3;

    public string? Category { get; init; }
    public bool IsEnabled { get; set; } = true;
}
