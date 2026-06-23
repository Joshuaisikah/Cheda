using Cheda.Core.Storage;

namespace Cheda.Tests.Storage.InMemory;

public sealed class InMemorySettingsRepository : ISettingsRepository
{
    private readonly Dictionary<string, string> _store = [];

    public string? Get(string key) =>
        _store.TryGetValue(key, out var v) ? v : null;

    public void Set(string key, string value) =>
        _store[key] = value;

    public void Remove(string key) =>
        _store.Remove(key);

    public bool Contains(string key) =>
        _store.ContainsKey(key);
}
