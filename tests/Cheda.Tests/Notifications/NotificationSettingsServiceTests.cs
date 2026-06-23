using Cheda.Core.Notifications;
using Cheda.Tests.Storage.InMemory;
using FluentAssertions;

namespace Cheda.Tests.Notifications;

public sealed class NotificationSettingsServiceTests
{
    private static NotificationSettingsService Build() =>
        new(new InMemorySettingsRepository());

    [Fact]
    public void Load_returns_defaults_when_no_value_stored()
    {
        var s = Build().Load();

        s.BudgetBreachEnabled.Should().BeTrue();
        s.LargeTransactionEnabled.Should().BeTrue();
        s.FulizaAlertEnabled.Should().BeTrue();
        s.NewTransactionEnabled.Should().BeFalse();
        s.DailyDigestEnabled.Should().BeTrue();
        s.WeeklyReportEnabled.Should().BeFalse();
        s.QuietHoursEnabled.Should().BeFalse();
        s.LargeTransactionThreshold.Should().Be(5_000m);
        s.DailyNotificationCap.Should().Be(5);
    }

    [Fact]
    public void Save_then_Load_roundtrips_all_fields()
    {
        var repo = new InMemorySettingsRepository();
        var svc  = new NotificationSettingsService(repo);

        svc.Save(new NotificationSettings
        {
            BudgetBreachEnabled       = false,
            LargeTransactionEnabled   = false,
            FulizaAlertEnabled        = false,
            NewTransactionEnabled     = true,
            DailyDigestEnabled        = false,
            WeeklyReportEnabled       = true,
            QuietHoursEnabled         = true,
            QuietStart                = new TimeOnly(23, 30),
            QuietEnd                  = new TimeOnly( 6, 0),
            LargeTransactionThreshold = 10_000m,
            DailyNotificationCap      = 2,
        });

        var s = svc.Load();

        s.BudgetBreachEnabled.Should().BeFalse();
        s.NewTransactionEnabled.Should().BeTrue();
        s.DailyDigestEnabled.Should().BeFalse();
        s.WeeklyReportEnabled.Should().BeTrue();
        s.QuietHoursEnabled.Should().BeTrue();
        s.QuietStart.Should().Be(new TimeOnly(23, 30));
        s.QuietEnd.Should().Be(new TimeOnly(6, 0));
        s.LargeTransactionThreshold.Should().Be(10_000m);
        s.DailyNotificationCap.Should().Be(2);
    }

    [Fact]
    public void Save_overwrites_previous_value()
    {
        var repo = new InMemorySettingsRepository();
        var svc  = new NotificationSettingsService(repo);

        svc.Save(new NotificationSettings { DailyNotificationCap = 10 });
        svc.Save(new NotificationSettings { DailyNotificationCap = 3  });

        svc.Load().DailyNotificationCap.Should().Be(3);
    }

    [Fact]
    public void Load_returns_defaults_on_corrupted_json()
    {
        var repo = new InMemorySettingsRepository();
        repo.Set("notification_settings", "not{valid}{json");
        var svc  = new NotificationSettingsService(repo);

        var s = svc.Load();

        s.DailyNotificationCap.Should().Be(5);
    }
}
