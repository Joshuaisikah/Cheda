using System.Text.Json;
using Cheda.Core.Categorization;
using Cheda.Core.Models;
using SQLite;

namespace Cheda.App.Storage.Entities;

[Table("PatternRules")]
internal sealed class PatternRuleEntity
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public int Priority { get; set; }
    public string Label { get; set; } = "";
    public string? TransactionTypesJson { get; set; }
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public int? TimeOfDayStartMinutes { get; set; }
    public int? TimeOfDayEndMinutes { get; set; }
    public string? DaysOfWeekJson { get; set; }
    public string Category { get; set; } = "";
    public bool IsEnabled { get; set; } = true;

    internal static PatternRuleEntity From(PatternRule r) => new()
    {
        Id                      = r.Id.ToString(),
        Priority                = r.Priority,
        Label                   = r.Label,
        TransactionTypesJson    = r.TransactionTypes is null ? null
            : JsonSerializer.Serialize(r.TransactionTypes.Select(t => (int)t).ToArray()),
        AmountMin               = r.AmountMin,
        AmountMax               = r.AmountMax,
        TimeOfDayStartMinutes   = r.TimeOfDayStart.HasValue
            ? r.TimeOfDayStart.Value.Hour * 60 + r.TimeOfDayStart.Value.Minute : null,
        TimeOfDayEndMinutes     = r.TimeOfDayEnd.HasValue
            ? r.TimeOfDayEnd.Value.Hour * 60 + r.TimeOfDayEnd.Value.Minute : null,
        DaysOfWeekJson          = r.DaysOfWeek is null ? null
            : JsonSerializer.Serialize(r.DaysOfWeek.Select(d => (int)d).ToArray()),
        Category                = r.Category,
        IsEnabled               = r.IsEnabled,
    };

    internal PatternRule ToDomain() => new()
    {
        Id               = Guid.Parse(Id),
        Priority         = Priority,
        Label            = Label,
        TransactionTypes = TransactionTypesJson is null ? null
            : JsonSerializer.Deserialize<int[]>(TransactionTypesJson)!.Select(i => (TransactionType)i).ToList(),
        AmountMin        = AmountMin,
        AmountMax        = AmountMax,
        TimeOfDayStart   = TimeOfDayStartMinutes.HasValue
            ? new TimeOnly(TimeOfDayStartMinutes.Value / 60, TimeOfDayStartMinutes.Value % 60) : null,
        TimeOfDayEnd     = TimeOfDayEndMinutes.HasValue
            ? new TimeOnly(TimeOfDayEndMinutes.Value / 60, TimeOfDayEndMinutes.Value % 60) : null,
        DaysOfWeek       = DaysOfWeekJson is null ? null
            : new HashSet<DayOfWeek>(JsonSerializer.Deserialize<int[]>(DaysOfWeekJson)!.Select(i => (DayOfWeek)i)),
        Category         = Category,
        IsEnabled        = IsEnabled,
    };
}
