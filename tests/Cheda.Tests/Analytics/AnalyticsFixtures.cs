using Cheda.Core.Categorization;
using Cheda.Core.Models;

namespace Cheda.Tests.Analytics;

/// <summary>
/// Two months of realistic M-Pesa transactions for analytics tests.
/// All timestamps are EAT (UTC+3).
/// </summary>
internal static class AnalyticsFixtures
{
    private static readonly TimeSpan Eat = TimeSpan.FromHours(3);

    private static Transaction T(
        TransactionType type, decimal amount, string? counterparty,
        int year, int month, int day, int hour = 12,
        decimal? balance = null, decimal? cost = null,
        string? category = null, bool nonExpense = false,
        string? reversesCode = null) => new()
    {
        TransactionCode      = Guid.NewGuid().ToString("N")[..10].ToUpper(),
        Source               = TransactionSource.Mpesa,
        Amount               = amount,
        Type                 = type,
        Counterparty         = counterparty,
        BalanceAfter         = balance,
        TransactionCost      = cost,
        Timestamp            = new DateTimeOffset(year, month, day, hour, 0, 0, Eat),
        RawMessage           = "",
        Category             = category,
        IsNonExpenseTransfer = nonExpense,
        ReversesTransactionCode = reversesCode,
    };

    // ── June 2025 ─────────────────────────────────────────────────────────────
    public static readonly IReadOnlyList<Transaction> June2025 =
    [
        // Income
        T(TransactionType.Received, 50_000m, "EMPLOYER LTD",               2025,6,1,  8,  balance:50_000m, category: DefaultCategories.Salary),
        T(TransactionType.Received,  2_000m, "JANE DOE 0722000000",         2025,6,10, 14, balance:47_000m, category: DefaultCategories.ReceivedPersonal),

        // Rent (paybill)
        T(TransactionType.PaidPaybill, 15_000m, "LANDLORD INC (123456/RENT01)", 2025,6,2,  9,  balance:32_000m, cost:33m,  category: DefaultCategories.Rent),

        // Electricity (paybill)
        T(TransactionType.PaidPaybill,  2_500m, "KPLC PREPAID (888880/54321)",  2025,6,3,  8,  balance:29_500m, cost:33m,  category: DefaultCategories.Electricity),

        // Groceries (till)
        T(TransactionType.PaidTill,     3_000m, "NAIVAS SUPERMARKET (Till 111111)", 2025,6,5, 10, balance:26_500m, cost:0m, category: DefaultCategories.Groceries),

        // Matatu fares (sent, small morning amounts)
        T(TransactionType.Sent,           50m, "CONDUCTOR 0799000001",       2025,6,2,  7,  balance:26_450m, cost:0m,  category: DefaultCategories.MatatuFare),
        T(TransactionType.Sent,           50m, "CONDUCTOR 0799000002",       2025,6,3,  7,  balance:26_400m, cost:0m,  category: DefaultCategories.MatatuFare),
        T(TransactionType.Sent,           50m, "CONDUCTOR 0799000003",       2025,6,4,  7,  balance:26_350m, cost:0m,  category: DefaultCategories.MatatuFare),
        T(TransactionType.Sent,           50m, "CONDUCTOR 0799000004",       2025,6,5,  7,  balance:26_300m, cost:0m,  category: DefaultCategories.MatatuFare),

        // Food (till)
        T(TransactionType.PaidTill,      800m, "JAVA HOUSE (Till 222222)",   2025,6,7,  13, balance:25_500m, cost:0m,  category: DefaultCategories.EatingOut),

        // Airtime
        T(TransactionType.Airtime,       200m, null,                         2025,6,8,  9,  balance:25_300m, cost:0m,  category: DefaultCategories.Airtime),

        // Sent to friend (later reversed)
        T(TransactionType.Sent,          500m, "WRONG PERSON 0700111222",    2025,6,9,  11, balance:24_800m, cost:11m),

        // Reversal of the above
        T(TransactionType.Reversal,      500m, null,                         2025,6,9,  11, balance:25_311m, category: DefaultCategories.RefundsReversals),

        // Withdrawal (non-expense)
        T(TransactionType.Withdrawn,   5_000m, "Agent 999 - JOHN AGENT",     2025,6,12, 10, balance:20_311m, cost:33m, nonExpense:true, category: DefaultCategories.Withdrawals),

        // Fuliza drawdown
        T(TransactionType.Fuliza,        200m, null,                         2025,6,20, 22, balance:500m,   category: DefaultCategories.Fuliza),

        // MShwari (non-expense)
        T(TransactionType.MShwari,     3_000m, "M-Shwari",                   2025,6,25, 9,  balance:17_311m, nonExpense:true, category: DefaultCategories.MShwari),
    ];

    // ── May 2025 (for month-over-month comparison) ────────────────────────────
    public static readonly IReadOnlyList<Transaction> May2025 =
    [
        T(TransactionType.Received, 48_000m, "EMPLOYER LTD",                2025,5,1,  8,  balance:48_000m, category: DefaultCategories.Salary),
        T(TransactionType.PaidPaybill, 15_000m, "LANDLORD INC (123456/RENT01)", 2025,5,2, 9, balance:33_000m, cost:33m, category: DefaultCategories.Rent),
        T(TransactionType.PaidPaybill,  2_500m, "KPLC PREPAID (888880/54321)",  2025,5,3, 8, balance:30_500m, cost:33m, category: DefaultCategories.Electricity),
        T(TransactionType.PaidTill,     2_500m, "NAIVAS SUPERMARKET (Till 111111)", 2025,5,6, 10, balance:28_000m, cost:0m, category: DefaultCategories.Groceries),
        T(TransactionType.Sent,           50m, "CONDUCTOR 0799000001",      2025,5,2,  7,  balance:27_950m, cost:0m,  category: DefaultCategories.MatatuFare),
        T(TransactionType.Sent,           50m, "CONDUCTOR 0799000002",      2025,5,3,  7,  balance:27_900m, cost:0m,  category: DefaultCategories.MatatuFare),
        T(TransactionType.PaidTill,      600m, "JAVA HOUSE (Till 222222)",  2025,5,10, 13, balance:27_300m, cost:0m,  category: DefaultCategories.EatingOut),
        T(TransactionType.Airtime,       150m, null,                        2025,5,8,  9,  balance:27_150m, cost:0m,  category: DefaultCategories.Airtime),
    ];

    // All transactions combined
    public static readonly IReadOnlyList<Transaction> All =
        [.. May2025, .. June2025];
}
