using System.Text.Json;
using Cheda.Core.Storage;

namespace Cheda.Core.Notifications;

/// <summary>Persists NotificationSettings as JSON in ISettingsRepository.</summary>
public sealed class NotificationSettingsService
{
    private const string Key = "notification_settings";
    private readonly ISettingsRepository _repo;

    public NotificationSettingsService(ISettingsRepository repo) => _repo = repo;

    public NotificationSettings Load()
    {
        var json = _repo.Get(Key);
        if (json is null) return new NotificationSettings();
        try
        {
            return JsonSerializer.Deserialize<NotificationSettings>(json)
                   ?? new NotificationSettings();
        }
        catch (JsonException)
        {
            return new NotificationSettings();
        }
    }

    public void Save(NotificationSettings settings) =>
        _repo.Set(Key, JsonSerializer.Serialize(settings));
}
