using System.Text.Json;
using Cheda.Core.Categorization;
using SQLite;

namespace Cheda.App.Storage.Entities;

[Table("RecipientRules")]
internal sealed class RecipientRuleEntity
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public int Priority { get; set; }
    public string Label { get; set; } = "";
    public string KeywordsJson { get; set; } = "[]";
    public string Category { get; set; } = "";
    public bool IsEnabled { get; set; } = true;

    internal static RecipientRuleEntity From(RecipientRule r) => new()
    {
        Id           = r.Id.ToString(),
        Priority     = r.Priority,
        Label        = r.Label,
        KeywordsJson = JsonSerializer.Serialize(r.Keywords),
        Category     = r.Category,
        IsEnabled    = r.IsEnabled,
    };

    internal RecipientRule ToDomain() => new()
    {
        Id        = Guid.Parse(Id),
        Priority  = Priority,
        Label     = Label,
        Keywords  = JsonSerializer.Deserialize<List<string>>(KeywordsJson) ?? [],
        Category  = Category,
        IsEnabled = IsEnabled,
    };
}
