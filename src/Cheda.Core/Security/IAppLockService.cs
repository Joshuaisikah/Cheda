namespace Cheda.Core.Security;

public interface IAppLockService
{
    /// <summary>True if a PIN has been configured.</summary>
    bool IsSetUp { get; }

    /// <summary>True when the user has not yet authenticated this session.</summary>
    bool IsLocked { get; }

    LockoutInfo GetLockoutInfo();

    /// <summary>First-time PIN setup. Derives and caches the DB key.</summary>
    Task<PinResult> SetupPinAsync(string pin, CancellationToken ct = default);

    /// <summary>Verifies the PIN; on success unlocks the app and caches the DB key.</summary>
    Task<PinResult> VerifyPinAsync(string pin, CancellationToken ct = default);

    /// <summary>
    /// Unlocks the app using a pre-authenticated biometric path — caller provides
    /// the DB key (retrieved from SecureStorage after the biometric prompt succeeded).
    /// </summary>
    void Unlock(byte[] dbKey);

    /// <summary>Locks the app and clears the cached DB key from memory.</summary>
    void Lock();

    /// <summary>Removes the PIN and any cached key. Nuclear option — requires new setup.</summary>
    void ClearPin();
}
