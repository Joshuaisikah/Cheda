namespace Cheda.Core.Security;

public sealed record LockoutInfo
{
    public int             FailedAttempts   { get; init; }
    public bool            IsLockedOut      { get; init; }
    public DateTimeOffset? LockedUntil      { get; init; }
    public TimeSpan?       RemainingCooldown { get; init; }
}
