using Cheda.Core.Models;

namespace Cheda.Core.Analytics;

public interface IAnalyticsEngine
{
    PeriodSummary GetSummary(IReadOnlyList<Transaction> transactions, DateRange range);

    IReadOnlyList<CategoryBreakdown> GetCategoryBreakdown(
        IReadOnlyList<Transaction> transactions, DateRange range);

    IReadOnlyList<TrendPoint> GetTrend(
        IReadOnlyList<Transaction> transactions, DateRange range, TrendGranularity granularity);

    FeeAnalytics GetFeeAnalytics(IReadOnlyList<Transaction> transactions, DateRange range);

    FulizaAnalytics GetFulizaAnalytics(IReadOnlyList<Transaction> transactions, DateRange range);

    IReadOnlyList<TopCounterparty> GetTopCounterparties(
        IReadOnlyList<Transaction> transactions, DateRange range, int top = 10);

    IReadOnlyList<Transaction> GetBiggestTransactions(
        IReadOnlyList<Transaction> transactions, DateRange range, int top = 10);

    PeriodComparison GetMonthOverMonth(
        IReadOnlyList<Transaction> transactions, int year, int month, TimeSpan offset = default);

    PeriodComparison GetWeekOverWeek(
        IReadOnlyList<Transaction> transactions, DateTimeOffset anyDayInWeek);

    decimal? GetCurrentBalance(IReadOnlyList<Transaction> transactions);
}
