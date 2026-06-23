using Cheda.Core.Bills;
using SQLite;

namespace Cheda.App.Storage.Entities;

[Table("RecurringBills")]
internal sealed class RecurringBillEntity
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string PaymentKey { get; set; } = "";
    public int PaymentKeyType { get; set; }
    public string? AccountReference { get; set; }
    public decimal ExpectedAmount { get; set; }
    public int Schedule { get; set; }
    public int DayOfMonth { get; set; }
    public int ReminderLeadDays { get; set; }
    public string? Category { get; set; }
    public bool IsEnabled { get; set; } = true;

    internal static RecurringBillEntity From(RecurringBill b) => new()
    {
        Id               = b.Id.ToString(),
        Label            = b.Label,
        PaymentKey       = b.PaymentKey,
        PaymentKeyType   = (int)b.PaymentKeyType,
        AccountReference = b.AccountReference,
        ExpectedAmount   = b.ExpectedAmount,
        Schedule         = (int)b.Schedule,
        DayOfMonth       = b.DayOfMonth,
        ReminderLeadDays = b.ReminderLeadDays,
        Category         = b.Category,
        IsEnabled        = b.IsEnabled,
    };

    internal RecurringBill ToDomain() => new()
    {
        Id               = Guid.Parse(Id),
        Label            = Label,
        PaymentKey       = PaymentKey,
        PaymentKeyType   = (BillPaymentKeyType)PaymentKeyType,
        AccountReference = AccountReference,
        ExpectedAmount   = ExpectedAmount,
        Schedule         = (BillSchedule)Schedule,
        DayOfMonth       = DayOfMonth,
        ReminderLeadDays = ReminderLeadDays,
        Category         = Category,
        IsEnabled        = IsEnabled,
    };
}
