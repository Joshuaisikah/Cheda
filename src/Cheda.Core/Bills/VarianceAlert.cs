namespace Cheda.Core.Bills;

public sealed record VarianceAlert
{
    public required RecurringBill Bill { get; init; }
    public decimal CurrentAmount { get; init; }
    public decimal TypicalAmount { get; init; }
    public decimal Variance { get; init; }
    public double VariancePercent { get; init; }
    public bool IsHigher => Variance > 0;
}
