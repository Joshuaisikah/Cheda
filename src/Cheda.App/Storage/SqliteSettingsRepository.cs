using Cheda.App.Storage.Entities;
using Cheda.Core.Storage;

namespace Cheda.App.Storage;

public sealed class SqliteSettingsRepository : ISettingsRepository
{
    private readonly DatabaseService _db;

    public SqliteSettingsRepository(DatabaseService db) => _db = db;

    public string? Get(string key) =>
        _db.Db.Table<SettingsEntity>()
              .FirstOrDefault(s => s.Key == key)
              ?.Value;

    public void Set(string key, string value)
    {
        var existing = _db.Db.Table<SettingsEntity>().FirstOrDefault(s => s.Key == key);
        if (existing is null)
            _db.Db.Insert(new SettingsEntity { Key = key, Value = value });
        else
        {
            existing.Value = value;
            _db.Db.Update(existing);
        }
    }

    public void Remove(string key) =>
        _db.Db.Delete<SettingsEntity>(key);

    public bool Contains(string key) =>
        _db.Db.Table<SettingsEntity>().FirstOrDefault(s => s.Key == key) is not null;
}
