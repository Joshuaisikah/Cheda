namespace Cheda.Core.Storage;

public interface ISettingsRepository
{
    string? Get(string key);
    void Set(string key, string value);
    void Remove(string key);
    bool Contains(string key);
}
