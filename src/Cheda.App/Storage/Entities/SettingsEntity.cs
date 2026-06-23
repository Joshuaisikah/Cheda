using SQLite;

namespace Cheda.App.Storage.Entities;

[Table("Settings")]
internal sealed class SettingsEntity
{
    [PrimaryKey]
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
