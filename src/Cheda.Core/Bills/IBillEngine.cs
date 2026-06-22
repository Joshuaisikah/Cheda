using Cheda.Core.Models;

namespace Cheda.Core.Bills;

public interface IBillEngine
{
    /// Returns true if the transaction is a payment for the given bill.
    bool IsPaymentForBill(RecurringBill bill, Transaction transaction);

    /// Computes the next due date for the bill from the given reference point.
    DateTimeOffset GetNextDueDate(RecurringBill bill, DateTimeOffset from);

    /// Bills due (and unpaid) within the next <paramref name="days"/> days, sorted soonest first.
    IReadOnlyList<UpcomingBill> GetUpcoming(
        IReadOnlyList<RecurringBill> bills,
        IReadOnlyList<BillOccurrence> occurrences,
        DateTimeOffset asOf,
        int days = 30);

    /// Bills whose most recent due date has passed and has no paid occurrence.
    IReadOnlyList<UpcomingBill> GetOverdue(
        IReadOnlyList<RecurringBill> bills,
        IReadOnlyList<BillOccurrence> occurrences,
        DateTimeOffset asOf);

    /// True if the bill is due within its reminder window and not yet paid.
    bool ShouldRemind(RecurringBill bill, BillOccurrence? currentOccurrence, DateTimeOffset asOf);

    /// Returns a variance alert if the current payment differs from the typical amount by more than
    /// <paramref name="thresholdPercent"/>%. Uses average of recent paid occurrences as the baseline.
    VarianceAlert? GetVarianceAlert(
        RecurringBill bill,
        IReadOnlyList<BillOccurrence> history,
        decimal currentAmount,
        double thresholdPercent = 20.0);

    /// Sum of expected amounts for all unpaid bills due within the window.
    decimal GetUpcomingTotal(
        IReadOnlyList<RecurringBill> bills,
        IReadOnlyList<BillOccurrence> occurrences,
        DateTimeOffset asOf,
        int days);
}
