namespace Cheda.Core.Budgets;

public sealed class Budget
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Category { get; init; }
    public required decimal MonthlyLimit { get; set; }
    /// Percentage of limit at which the amber warning fires (default 75%).
    public double AmberThresholdPercent { get; set; } = 75.0;
    /// Percentage of limit at which the red warning fires (default 90%).
    public double RedThresholdPercent { get; set; } = 90.0;
    public bool IsEnabled { get; set; } = true;
}
