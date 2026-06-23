namespace Cheda.Core.Security;

/// <summary>Result of a PIN operation (setup, verify, or unlock attempt).</summary>
public sealed class PinResult
{
    public static readonly PinResult Success  = new() { IsSuccess  = true };
    public static readonly PinResult NotSetUp = new() { ErrorCode  = "not_setup" };

    public static PinResult Invalid(string message) =>
        new() { ErrorCode = "invalid", ErrorMessage = message };

    public static PinResult Incorrect(LockoutInfo lockout) =>
        new() { ErrorCode = "incorrect", Lockout = lockout };

    public static PinResult LockedOut(LockoutInfo lockout) =>
        new() { ErrorCode = "locked_out", Lockout = lockout };

    public bool         IsSuccess     { get; private init; }
    public string?      ErrorCode     { get; private init; }
    public string?      ErrorMessage  { get; private init; }
    public LockoutInfo? Lockout       { get; private init; }

    public bool IsLockedOut => ErrorCode == "locked_out";
    public bool IsIncorrect => ErrorCode == "incorrect";
    public bool IsNotSetUp  => ErrorCode == "not_setup";
    public bool IsInvalid   => ErrorCode == "invalid";
}
