using Cheda.Core.Budgets;
using SQLite;

namespace Cheda.App.Storage.Entities;

[Table("Budgets")]
internal sealed class BudgetEntity
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal MonthlyLimit { get; set; }
    public double AmberThresholdPercent { get; set; }
    public double RedThresholdPercent { get; set; }
    public bool IsEnabled { get; set; } = true;

    internal static BudgetEntity From(Budget b) => new()
    {
        Id                   = b.Id.ToString(),
        Category             = b.Category,
        MonthlyLimit         = b.MonthlyLimit,
        AmberThresholdPercent = b.AmberThresholdPercent,
        RedThresholdPercent  = b.RedThresholdPercent,
        IsEnabled            = b.IsEnabled,
    };

    internal Budget ToDomain() => new()
    {
        Id                   = Guid.Parse(Id),
        Category             = Category,
        MonthlyLimit         = MonthlyLimit,
        AmberThresholdPercent = AmberThresholdPercent,
        RedThresholdPercent  = RedThresholdPercent,
        IsEnabled            = IsEnabled,
    };
}
