namespace Cheda.Core.Notifications;

/// <summary>
/// User-configurable notification preferences, serialised to ISettingsRepository as JSON.
/// Defaults are intentionally restrained — only meaningful alerts are on out of the box.
/// </summary>
public sealed class NotificationSettings
{
    // Per-type toggles.
    public bool BudgetBreachEnabled     { get; set; } = true;
    public bool LargeTransactionEnabled { get; set; } = true;
    public bool FulizaAlertEnabled      { get; set; } = true;
    public bool NewTransactionEnabled   { get; set; } = true;
    public bool DailyDigestEnabled      { get; set; } = true;
    public bool WeeklyReportEnabled     { get; set; } = false;

    // Quiet hours — wrap-around midnight is supported (e.g. 22:00–07:00).
    public bool     QuietHoursEnabled   { get; set; } = false;
    public TimeOnly QuietStart          { get; set; } = new(22, 0);
    public TimeOnly QuietEnd            { get; set; } = new( 7, 0);

    // Thresholds.
    public decimal LargeTransactionThreshold { get; set; } = 5_000m;

    // Maximum alerts shown per calendar day across all types.
    // Reliable delivery on time-critical alerts (Fuliza, overspend) still happens;
    // lower-priority alerts are silently dropped once the cap is reached.
    public int DailyNotificationCap { get; set; } = 5;
}
