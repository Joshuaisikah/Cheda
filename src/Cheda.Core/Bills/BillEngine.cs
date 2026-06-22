using System.Globalization;
using System.Text.RegularExpressions;
using Cheda.Core.Models;

namespace Cheda.Core.Bills;

public sealed partial class BillEngine : IBillEngine
{
    // ── Payment matching ─────────────────────────────────────────────────────

    public bool IsPaymentForBill(RecurringBill bill, Transaction transaction)
    {
        if (transaction.Counterparty is null) return false;

        return bill.PaymentKeyType switch
        {
            BillPaymentKeyType.Paybill     => MatchesPaybill(bill, transaction),
            BillPaymentKeyType.Till        => MatchesTill(bill, transaction),
            BillPaymentKeyType.Counterparty => MatchesCounterparty(bill, transaction),
            _                              => false,
        };
    }

    [GeneratedRegex(@"\((\d+)/(\S+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex PaybillInCounterparty();

    [GeneratedRegex(@"\(Till\s+(\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex TillInCounterparty();

    private static bool MatchesPaybill(RecurringBill bill, Transaction tx)
    {
        if (tx.Type != TransactionType.PaidPaybill) return false;
        var m = PaybillInCounterparty().Match(tx.Counterparty!);
        if (!m.Success) return false;
        if (!m.Groups[1].Value.Equals(bill.PaymentKey, StringComparison.OrdinalIgnoreCase))
            return false;
        if (bill.AccountReference is not null &&
            !m.Groups[2].Value.Equals(bill.AccountReference, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static bool MatchesTill(RecurringBill bill, Transaction tx)
    {
        if (tx.Type != TransactionType.PaidTill) return false;
        var m = TillInCounterparty().Match(tx.Counterparty!);
        return m.Success &&
               m.Groups[1].Value.Equals(bill.PaymentKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesCounterparty(RecurringBill bill, Transaction tx) =>
        (tx.Counterparty ?? "").Contains(bill.PaymentKey, StringComparison.OrdinalIgnoreCase);

    // ── Scheduling ───────────────────────────────────────────────────────────

    public DateTimeOffset GetNextDueDate(RecurringBill bill, DateTimeOffset from)
    {
        return bill.Schedule switch
        {
            BillSchedule.Monthly   => NextMonthlyDue(bill.DayOfMonth, from),
            BillSchedule.Weekly    => from.AddDays(7 - (int)from.DayOfWeek + 1).Date.ToDateTimeOffset(from.Offset),
            BillSchedule.Quarterly => NextMonthlyDue(bill.DayOfMonth, from, monthStep: 3),
            BillSchedule.Annual    => NextMonthlyDue(bill.DayOfMonth, from, monthStep: 12),
            _                      => throw new ArgumentOutOfRangeException(nameof(bill.Schedule)),
        };
    }

    private static DateTimeOffset NextMonthlyDue(int dayOfMonth, DateTimeOffset from, int monthStep = 1)
    {
        var local = from.LocalDateTime;
        var day   = Math.Min(dayOfMonth, DateTime.DaysInMonth(local.Year, local.Month));
        var candidate = new DateTime(local.Year, local.Month, day, 0, 0, 0, DateTimeKind.Unspecified);

        // If today is already past the day, advance by monthStep
        if (local.Date >= candidate)
        {
            var next = candidate.AddMonths(monthStep);
            day      = Math.Min(dayOfMonth, DateTime.DaysInMonth(next.Year, next.Month));
            candidate = new DateTime(next.Year, next.Month, day, 0, 0, 0, DateTimeKind.Unspecified);
        }

        return new DateTimeOffset(candidate, from.Offset);
    }

    // ── Upcoming & overdue ───────────────────────────────────────────────────

    public IReadOnlyList<UpcomingBill> GetUpcoming(
        IReadOnlyList<RecurringBill> bills,
        IReadOnlyList<BillOccurrence> occurrences,
        DateTimeOffset asOf,
        int days = 30)
    {
        var cutoff = asOf.AddDays(days);
        var result = new List<UpcomingBill>();

        foreach (var bill in bills.Where(b => b.IsEnabled))
        {
            var due = GetNextDueDate(bill, asOf.AddDays(-1)); // include today
            if (due > cutoff) continue;
            if (IsPaidForPeriod(bill, occurrences, due)) continue;

            result.Add(MakeUpcomingBill(bill, due, asOf));
        }

        return result.OrderBy(u => u.DueDate).ToList();
    }

    public IReadOnlyList<UpcomingBill> GetOverdue(
        IReadOnlyList<RecurringBill> bills,
        IReadOnlyList<BillOccurrence> occurrences,
        DateTimeOffset asOf)
    {
        var result = new List<UpcomingBill>();

        foreach (var bill in bills.Where(b => b.IsEnabled))
        {
            // The most recent due date before asOf
            var due = MostRecentDueDate(bill, asOf);
            if (due is null) continue;
            if (asOf.LocalDateTime.Date <= due.Value.LocalDateTime.Date) continue;
            if (IsPaidForPeriod(bill, occurrences, due.Value)) continue;

            result.Add(MakeUpcomingBill(bill, due.Value, asOf));
        }

        return result.OrderBy(u => u.DueDate).ToList();
    }

    // ── Reminders ────────────────────────────────────────────────────────────

    public bool ShouldRemind(RecurringBill bill, BillOccurrence? currentOccurrence, DateTimeOffset asOf)
    {
        if (currentOccurrence?.Status == BillOccurrenceStatus.Paid) return false;
        var due = GetNextDueDate(bill, asOf.AddDays(-1));
        var daysUntil = (due.LocalDateTime.Date - asOf.LocalDateTime.Date).Days;
        return daysUntil >= 0 && daysUntil <= bill.ReminderLeadDays;
    }

    // ── Variance alert ───────────────────────────────────────────────────────

    public VarianceAlert? GetVarianceAlert(
        RecurringBill bill,
        IReadOnlyList<BillOccurrence> history,
        decimal currentAmount,
        double thresholdPercent = 20.0)
    {
        var paid = history
            .Where(o => o.Status == BillOccurrenceStatus.Paid && o.ActualAmount.HasValue)
            .OrderByDescending(o => o.DueDate)
            .Take(3)
            .ToList();

        if (paid.Count == 0) return null;

        var typical  = paid.Average(o => (double)o.ActualAmount!.Value);
        var variance = (double)currentAmount - typical;
        var pct      = typical > 0 ? Math.Abs(variance) / typical * 100.0 : 0;

        if (pct < thresholdPercent) return null;

        return new VarianceAlert
        {
            Bill            = bill,
            CurrentAmount   = currentAmount,
            TypicalAmount   = (decimal)typical,
            Variance        = currentAmount - (decimal)typical,
            VariancePercent = pct,
        };
    }

    // ── Upcoming total ───────────────────────────────────────────────────────

    public decimal GetUpcomingTotal(
        IReadOnlyList<RecurringBill> bills,
        IReadOnlyList<BillOccurrence> occurrences,
        DateTimeOffset asOf,
        int days) =>
        GetUpcoming(bills, occurrences, asOf, days).Sum(u => u.ExpectedAmount);

    // ── Private helpers ──────────────────────────────────────────────────────

    private static UpcomingBill MakeUpcomingBill(RecurringBill bill, DateTimeOffset due, DateTimeOffset asOf) =>
        new()
        {
            Bill           = bill,
            DueDate        = due,
            ExpectedAmount = bill.ExpectedAmount,
            DaysUntilDue   = (due.LocalDateTime.Date - asOf.LocalDateTime.Date).Days,
        };

    private static bool IsPaidForPeriod(
        RecurringBill bill, IReadOnlyList<BillOccurrence> occurrences, DateTimeOffset due)
    {
        return occurrences.Any(o =>
            o.BillId == bill.Id &&
            o.Status == BillOccurrenceStatus.Paid &&
            SamePeriod(bill.Schedule, o.DueDate, due));
    }

    private static bool SamePeriod(BillSchedule schedule, DateTimeOffset a, DateTimeOffset b)
    {
        var al = a.LocalDateTime;
        var bl = b.LocalDateTime;
        return schedule switch
        {
            BillSchedule.Monthly   => al.Year == bl.Year && al.Month == bl.Month,
            BillSchedule.Weekly    => al.Year == bl.Year && ISOWeek.GetWeekOfYear(al) == ISOWeek.GetWeekOfYear(bl),
            BillSchedule.Quarterly => al.Year == bl.Year && (al.Month - 1) / 3 == (bl.Month - 1) / 3,
            BillSchedule.Annual    => al.Year == bl.Year,
            _                      => al.Date == bl.Date,
        };
    }

    private static DateTimeOffset? MostRecentDueDate(RecurringBill bill, DateTimeOffset asOf)
    {
        var local = asOf.LocalDateTime;
        return bill.Schedule switch
        {
            BillSchedule.Monthly   => MostRecentMonthlyDue(bill.DayOfMonth, local, asOf.Offset),
            BillSchedule.Weekly    => asOf.AddDays(-(int)local.DayOfWeek - 6), // previous week
            BillSchedule.Quarterly => MostRecentMonthlyDue(bill.DayOfMonth, local, asOf.Offset, step: 3),
            BillSchedule.Annual    => MostRecentMonthlyDue(bill.DayOfMonth, local, asOf.Offset, step: 12),
            _                      => null,
        };
    }

    private static DateTimeOffset MostRecentMonthlyDue(
        int dayOfMonth, DateTime local, TimeSpan offset, int step = 1)
    {
        var day  = Math.Min(dayOfMonth, DateTime.DaysInMonth(local.Year, local.Month));
        var curr = new DateTimeOffset(local.Year, local.Month, day, 0, 0, 0, offset);

        if (curr.LocalDateTime.Date < local.Date)
            return curr; // this month's due date is already past

        // Due date is today or future — go back one period
        var prev = new DateTime(local.Year, local.Month, 1).AddMonths(-step);
        day = Math.Min(dayOfMonth, DateTime.DaysInMonth(prev.Year, prev.Month));
        return new DateTimeOffset(prev.Year, prev.Month, day, 0, 0, 0, offset);
    }

    private static DateTimeOffset ClampDay(DateTimeOffset month, int day)
    {
        var d = Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month));
        return new DateTimeOffset(month.Year, month.Month, d, 0, 0, 0, month.Offset);
    }
}

internal static class DateExtensions
{
    internal static DateTimeOffset ToDateTimeOffset(this DateTime dt, TimeSpan offset) =>
        new(dt, offset);
}
