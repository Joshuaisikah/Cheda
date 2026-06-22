namespace Cheda.Core.Analytics;

public sealed record PeriodSummary
{
    public required DateRange Range { get; init; }
    public decimal TotalIncome { get; init; }
    public decimal TotalExpenses { get; init; }      // net of reversals
    public decimal TotalReversals { get; init; }
    public decimal Net { get; init; }                // Income − NetExpenses
    public decimal TotalFees { get; init; }
    public decimal AverageDailySpend { get; init; }
    public double SavingsRate { get; init; }         // % of income not spent
    public decimal? CurrentBalance { get; init; }    // most recent M-PESA balance in range
    public int TransactionCount { get; init; }
    public int IncomeTransactionCount { get; init; }
    public int ExpenseTransactionCount { get; init; }
}
