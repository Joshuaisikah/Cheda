using Cheda.Core.Bills;
using Cheda.Core.Models;
using FluentAssertions;

namespace Cheda.Tests.Bills;

public class BillEngineTests
{
    private readonly BillEngine _engine = new();
    private static readonly TimeSpan Eat = TimeSpan.FromHours(3);

    private static DateTimeOffset Dt(int year, int month, int day) =>
        new(year, month, day, 10, 0, 0, Eat);

    private static RecurringBill KplcBill(int dueDay = 5) => new()
    {
        Label           = "KPLC Electricity",
        PaymentKey      = "888880",
        PaymentKeyType  = BillPaymentKeyType.Paybill,
        AccountReference = "54321",
        ExpectedAmount  = 2_000m,
        Schedule        = BillSchedule.Monthly,
        DayOfMonth      = dueDay,
        ReminderLeadDays = 3,
    };

    private static RecurringBill RentBill(int dueDay = 2) => new()
    {
        Label          = "Rent",
        PaymentKey     = "123456",
        PaymentKeyType = BillPaymentKeyType.Paybill,
        AccountReference = "RENT01",
        ExpectedAmount = 15_000m,
        Schedule       = BillSchedule.Monthly,
        DayOfMonth     = dueDay,
        ReminderLeadDays = 5,
    };

    private static Transaction KplcPayment(decimal amount = 2_000m) => new()
    {
        TransactionCode = "KPLCPAY001",
        Source          = TransactionSource.Mpesa,
        Amount          = amount,
        Type            = TransactionType.PaidPaybill,
        Counterparty    = "KPLC PREPAID (888880/54321)",
        Timestamp       = Dt(2025, 6, 4),
        RawMessage      = "",
    };

    // ── Payment matching ──────────────────────────────────────────────────────

    [Fact]
    public void IsPaymentForBill_MatchingPaybill_ReturnsTrue()
        => _engine.IsPaymentForBill(KplcBill(), KplcPayment()).Should().BeTrue();

    [Fact]
    public void IsPaymentForBill_WrongPaybillNumber_ReturnsFalse()
    {
        var bill = KplcBill();
        var tx = new Transaction
        {
            TransactionCode = "WRONGPAY01",
            Source          = TransactionSource.Mpesa,
            Amount          = 2_000m,
            Type            = TransactionType.PaidPaybill,
            Counterparty    = "NAIROBI WATER (999999/ACC77)",
            Timestamp       = Dt(2025, 6, 4),
            RawMessage      = "",
        };
        _engine.IsPaymentForBill(bill, tx).Should().BeFalse();
    }

    [Fact]
    public void IsPaymentForBill_WrongAccountReference_ReturnsFalse()
    {
        var bill = KplcBill();
        var tx = new Transaction
        {
            TransactionCode = "WRONGACC01",
            Source          = TransactionSource.Mpesa,
            Amount          = 2_000m,
            Type            = TransactionType.PaidPaybill,
            Counterparty    = "KPLC PREPAID (888880/99999)", // wrong account
            Timestamp       = Dt(2025, 6, 4),
            RawMessage      = "",
        };
        _engine.IsPaymentForBill(bill, tx).Should().BeFalse();
    }

    [Fact]
    public void IsPaymentForBill_TillMatch_ReturnsTrue()
    {
        var bill = new RecurringBill
        {
            Label          = "Java House",
            PaymentKey     = "123456",
            PaymentKeyType = BillPaymentKeyType.Till,
            ExpectedAmount = 500m,
            Schedule       = BillSchedule.Monthly,
        };
        var tx = new Transaction
        {
            TransactionCode = "JAVATXN001",
            Source          = TransactionSource.Mpesa,
            Amount          = 500m,
            Type            = TransactionType.PaidTill,
            Counterparty    = "JAVA HOUSE (Till 123456)",
            Timestamp       = Dt(2025, 6, 4),
            RawMessage      = "",
        };
        _engine.IsPaymentForBill(bill, tx).Should().BeTrue();
    }

    [Fact]
    public void IsPaymentForBill_CounterpartyMatch_ReturnsTrue()
    {
        var bill = new RecurringBill
        {
            Label          = "House Help",
            PaymentKey     = "MARY WANJIKU",
            PaymentKeyType = BillPaymentKeyType.Counterparty,
            ExpectedAmount = 8_000m,
            Schedule       = BillSchedule.Monthly,
        };
        var tx = new Transaction
        {
            TransactionCode = "MARYTXN001",
            Source          = TransactionSource.Mpesa,
            Amount          = 8_000m,
            Type            = TransactionType.Sent,
            Counterparty    = "MARY WANJIKU 0711222333",
            Timestamp       = Dt(2025, 6, 28),
            RawMessage      = "",
        };
        _engine.IsPaymentForBill(bill, tx).Should().BeTrue();
    }

    // ── Next due date ──────────────────────────────────────────────────────────

    [Fact]
    public void GetNextDueDate_BeforeDueDay_ReturnsSameMonth()
    {
        var bill = KplcBill(dueDay: 10);
        var from = Dt(2025, 6, 5); // before the 10th
        var due  = _engine.GetNextDueDate(bill, from);
        due.Month.Should().Be(6);
        due.Day.Should().Be(10);
    }

    [Fact]
    public void GetNextDueDate_AfterDueDay_ReturnsNextMonth()
    {
        var bill = KplcBill(dueDay: 5);
        var from = Dt(2025, 6, 6); // after the 5th
        var due  = _engine.GetNextDueDate(bill, from);
        due.Month.Should().Be(7);
        due.Day.Should().Be(5);
    }

    [Fact]
    public void GetNextDueDate_Day31InFebMonth_Clamped()
    {
        var bill = new RecurringBill
        {
            Label = "Test", PaymentKey = "1", ExpectedAmount = 1m,
            Schedule = BillSchedule.Monthly, DayOfMonth = 31,
        };
        var from = Dt(2025, 1, 31); // after the 31st → goes to Feb
        var due  = _engine.GetNextDueDate(bill, from);
        due.Month.Should().Be(2);
        due.Day.Should().Be(28); // Feb 2025 has 28 days
    }

    // ── Upcoming bills ─────────────────────────────────────────────────────────

    [Fact]
    public void GetUpcoming_BillDueWithinWindow_Included()
    {
        var bill  = KplcBill(dueDay: 10);
        var asOf  = Dt(2025, 6, 5);  // 5 days before due
        var upcoming = _engine.GetUpcoming([bill], [], asOf, 30);

        upcoming.Should().HaveCount(1);
        upcoming[0].Bill.Should().Be(bill);
        upcoming[0].DaysUntilDue.Should().Be(5);
    }

    [Fact]
    public void GetUpcoming_AlreadyPaidThisMonth_Excluded()
    {
        var bill = KplcBill(dueDay: 10);
        var paidOccurrence = new BillOccurrence
        {
            BillId         = bill.Id,
            DueDate        = Dt(2025, 6, 10),
            ExpectedAmount = 2_000m,
            Status         = BillOccurrenceStatus.Paid,
            PaidDate       = Dt(2025, 6, 9),
        };

        var upcoming = _engine.GetUpcoming([bill], [paidOccurrence], Dt(2025, 6, 5), 30);
        upcoming.Should().BeEmpty();
    }

    [Fact]
    public void GetUpcoming_DueBeyondWindow_Excluded()
    {
        var bill    = KplcBill(dueDay: 10);
        var asOf    = Dt(2025, 6, 5);
        var upcoming = _engine.GetUpcoming([bill], [], asOf, 3); // window only 3 days
        upcoming.Should().BeEmpty();  // due on 10th is 5 days away
    }

    [Fact]
    public void GetUpcoming_MultipleBills_SortedByDueDate()
    {
        var kplc = KplcBill(dueDay: 10);
        var rent = RentBill(dueDay: 2);
        var asOf = Dt(2025, 6, 1);

        var upcoming = _engine.GetUpcoming([kplc, rent], [], asOf, 30);
        upcoming.Should().HaveCount(2);
        upcoming[0].Bill.Should().Be(rent);   // due 2nd
        upcoming[1].Bill.Should().Be(kplc);   // due 10th
    }

    // ── Overdue bills ──────────────────────────────────────────────────────────

    [Fact]
    public void GetOverdue_UnpaidPastDue_Returned()
    {
        var bill = KplcBill(dueDay: 5);
        var asOf = Dt(2025, 6, 8); // past the 5th, not paid

        var overdue = _engine.GetOverdue([bill], [], asOf);
        overdue.Should().HaveCount(1);
        overdue[0].IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void GetOverdue_PaidOnTime_NotReturned()
    {
        var bill = KplcBill(dueDay: 5);
        var paid = new BillOccurrence
        {
            BillId         = bill.Id,
            DueDate        = Dt(2025, 6, 5),
            ExpectedAmount = 2_000m,
            Status         = BillOccurrenceStatus.Paid,
        };
        var asOf = Dt(2025, 6, 8);

        _engine.GetOverdue([bill], [paid], asOf).Should().BeEmpty();
    }

    // ── Reminders ──────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldRemind_WithinLeadTime_ReturnsTrue()
    {
        var bill = KplcBill(dueDay: 10); // 3-day lead
        var asOf = Dt(2025, 6, 8);       // 2 days before
        _engine.ShouldRemind(bill, null, asOf).Should().BeTrue();
    }

    [Fact]
    public void ShouldRemind_OutsideLeadTime_ReturnsFalse()
    {
        var bill = KplcBill(dueDay: 10);
        var asOf = Dt(2025, 6, 1);  // 9 days before, outside 3-day window
        _engine.ShouldRemind(bill, null, asOf).Should().BeFalse();
    }

    [Fact]
    public void ShouldRemind_AlreadyPaid_ReturnsFalse()
    {
        var bill = KplcBill(dueDay: 10);
        var paid = new BillOccurrence
        {
            BillId = bill.Id, DueDate = Dt(2025, 6, 10),
            ExpectedAmount = 2_000m, Status = BillOccurrenceStatus.Paid,
        };
        _engine.ShouldRemind(bill, paid, Dt(2025, 6, 8)).Should().BeFalse();
    }

    // ── Variance alert ─────────────────────────────────────────────────────────

    [Fact]
    public void GetVarianceAlert_LargeSpike_ReturnsAlert()
    {
        var bill = KplcBill();
        var history = new[]
        {
            Occurrence(bill, 2025, 3, amount: 2_000m),
            Occurrence(bill, 2025, 4, amount: 2_100m),
            Occurrence(bill, 2025, 5, amount: 1_900m),
        };

        var alert = _engine.GetVarianceAlert(bill, history, currentAmount: 3_200m);

        alert.Should().NotBeNull();
        alert!.IsHigher.Should().BeTrue();
        alert.VariancePercent.Should().BeGreaterThan(20.0);
    }

    [Fact]
    public void GetVarianceAlert_NormalVariation_ReturnsNull()
    {
        var bill = KplcBill();
        var history = new[]
        {
            Occurrence(bill, 2025, 3, amount: 2_000m),
            Occurrence(bill, 2025, 4, amount: 2_050m),
            Occurrence(bill, 2025, 5, amount: 1_980m),
        };

        _engine.GetVarianceAlert(bill, history, currentAmount: 2_100m).Should().BeNull();
    }

    [Fact]
    public void GetVarianceAlert_NoHistory_ReturnsNull()
        => _engine.GetVarianceAlert(KplcBill(), [], 3_000m).Should().BeNull();

    // ── Upcoming total ─────────────────────────────────────────────────────────

    [Fact]
    public void GetUpcomingTotal_SumsAllUnpaidInWindow()
    {
        var bills = new[] { KplcBill(dueDay: 10), RentBill(dueDay: 2) };
        var asOf  = Dt(2025, 6, 1);

        var total = _engine.GetUpcomingTotal(bills, [], asOf, 30);
        total.Should().Be(17_000m); // 2000 + 15000
    }

    // ── Occurrence on-time tracking ───────────────────────────────────────────

    [Fact]
    public void BillOccurrence_PaidBeforeDue_IsOnTime()
    {
        var occ = new BillOccurrence
        {
            BillId         = Guid.NewGuid(),
            DueDate        = Dt(2025, 6, 10),
            ExpectedAmount = 2_000m,
            Status         = BillOccurrenceStatus.Paid,
            PaidDate       = Dt(2025, 6, 8),
        };
        occ.IsOnTime.Should().BeTrue();
    }

    [Fact]
    public void BillOccurrence_PaidAfterDue_IsNotOnTime()
    {
        var occ = new BillOccurrence
        {
            BillId         = Guid.NewGuid(),
            DueDate        = Dt(2025, 6, 10),
            ExpectedAmount = 2_000m,
            Status         = BillOccurrenceStatus.Paid,
            PaidDate       = Dt(2025, 6, 15),
        };
        occ.IsOnTime.Should().BeFalse();
    }

    [Fact]
    public void BillOccurrence_AmountSpike_HasVariance()
    {
        var occ = new BillOccurrence
        {
            BillId         = Guid.NewGuid(),
            DueDate        = Dt(2025, 6, 5),
            ExpectedAmount = 2_000m,
            Status         = BillOccurrenceStatus.Paid,
            ActualAmount   = 3_500m, // >10% variance
        };
        occ.HasVariance.Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BillOccurrence Occurrence(RecurringBill bill, int year, int month, decimal amount) =>
        new()
        {
            BillId         = bill.Id,
            DueDate        = new DateTimeOffset(year, month, bill.DayOfMonth, 0, 0, 0, Eat),
            ExpectedAmount = bill.ExpectedAmount,
            Status         = BillOccurrenceStatus.Paid,
            ActualAmount   = amount,
            PaidDate       = new DateTimeOffset(year, month, bill.DayOfMonth - 1, 0, 0, 0, Eat),
        };
}
