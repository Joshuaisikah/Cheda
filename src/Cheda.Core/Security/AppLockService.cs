using Cheda.Core.Storage;

namespace Cheda.Core.Security;

/// <summary>
/// Manages the app's locked/unlocked state, PIN verification, and failed-attempt lockout.
///
/// Lockout tiers (since last successful auth):
///   ≥ 3 failures → 30-second cooldown
///   ≥ 5 failures → 5-minute cooldown
///   ≥ 10 failures → 1-hour cooldown
///
/// The clock is injected so lockout tests run without sleeping.
/// </summary>
public sealed class AppLockService : IAppLockService
{
    private const string FailCountKey   = "pin_fail_count";
    private const string LockedUntilKey = "pin_locked_until";

    private readonly PinHashService       _hasher;
    private readonly IPinStore            _pinStore;
    private readonly ISettingsRepository  _settings;
    private readonly IDatabaseKeyProvider _keyProvider;
    private readonly Func<DateTimeOffset> _clock;

    private bool _isUnlocked;

    public AppLockService(
        PinHashService hasher,
        IPinStore pinStore,
        ISettingsRepository settings,
        IDatabaseKeyProvider keyProvider,
        Func<DateTimeOffset>? clock = null)
    {
        _hasher      = hasher;
        _pinStore    = pinStore;
        _settings    = settings;
        _keyProvider = keyProvider;
        _clock       = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public bool IsSetUp  => _pinStore.HasPin;
    // When no PIN is configured the app is always considered unlocked (no security layer active).
    public bool IsLocked => IsSetUp && !_isUnlocked;

    // ── Setup ─────────────────────────────────────────────────────────────────

    public async Task<PinResult> SetupPinAsync(string pin, CancellationToken ct = default)
    {
        if (!IsValidPin(pin))
            return PinResult.Invalid("PIN must be 4–6 digits (numbers only).");

        var (verifier, dbKey) = await Task.Run(
            () => _hasher.CreateVerifierAndDbKey(pin), ct);

        _pinStore.Save(verifier);
        _keyProvider.SetKey(dbKey);
        _isUnlocked = true;
        ClearFailCount();
        return PinResult.Success;
    }

    // ── Verification ──────────────────────────────────────────────────────────

    public async Task<PinResult> VerifyPinAsync(string pin, CancellationToken ct = default)
    {
        var lockout = GetLockoutInfo();
        if (lockout.IsLockedOut) return PinResult.LockedOut(lockout);

        var verifier = _pinStore.Load();
        if (verifier is null) return PinResult.NotSetUp;

        var (ok, dbKey) = await Task.Run(() => _hasher.Verify(pin, verifier), ct);

        if (!ok)
        {
            IncrementFailCount();
            return PinResult.Incorrect(GetLockoutInfo());
        }

        ClearFailCount();
        _keyProvider.SetKey(dbKey!);
        _isUnlocked = true;
        return PinResult.Success;
    }

    public void Unlock(byte[] dbKey)
    {
        _keyProvider.SetKey(dbKey);
        _isUnlocked = true;
    }

    public void Lock()
    {
        _keyProvider.ClearKey();
        _isUnlocked = false;
    }

    public void ClearPin()
    {
        _pinStore.Clear();
        _keyProvider.ClearKey();
        _isUnlocked = false;
        ClearFailCount();
    }

    // ── Lockout ───────────────────────────────────────────────────────────────

    public LockoutInfo GetLockoutInfo()
    {
        var count        = GetFailCount();
        var lockedUntil  = GetLockedUntil();
        var now          = _clock();
        var isLocked     = lockedUntil.HasValue && now < lockedUntil.Value;
        var remaining    = isLocked ? lockedUntil!.Value - now : (TimeSpan?)null;

        return new LockoutInfo
        {
            FailedAttempts    = count,
            IsLockedOut       = isLocked,
            LockedUntil       = isLocked ? lockedUntil : null,
            RemainingCooldown = remaining,
        };
    }

    private void IncrementFailCount()
    {
        var count = GetFailCount() + 1;
        _settings.Set(FailCountKey, count.ToString());

        var cooldown = count switch
        {
            >= 10 => TimeSpan.FromHours(1),
            >=  5 => TimeSpan.FromMinutes(5),
            >=  3 => TimeSpan.FromSeconds(30),
            _     => TimeSpan.Zero,
        };

        if (cooldown > TimeSpan.Zero)
            _settings.Set(LockedUntilKey, (_clock() + cooldown).ToString("O"));
    }

    private void ClearFailCount()
    {
        _settings.Remove(FailCountKey);
        _settings.Remove(LockedUntilKey);
    }

    private int GetFailCount() =>
        int.TryParse(_settings.Get(FailCountKey), out var n) ? n : 0;

    private DateTimeOffset? GetLockedUntil()
    {
        var s = _settings.Get(LockedUntilKey);
        return s is not null && DateTimeOffset.TryParse(s, out var dt) ? dt : null;
    }

    private static bool IsValidPin(string pin) =>
        pin.Length is >= 4 and <= 6 && pin.All(char.IsAsciiDigit);
}
