namespace Cheda.Core.Security;

public sealed class BiometricResult
{
    public static readonly BiometricResult Success     = new() { IsSuccess     = true };
    public static readonly BiometricResult Cancelled   = new() { IsCancelled   = true };
    // User explicitly tapped the negative button ("Use PIN instead") in the system prompt.
    public static readonly BiometricResult PinFallback = new() { IsPinFallback = true };

    public static BiometricResult Error(string message) =>
        new() { ErrorMessage = message };

    public bool    IsSuccess     { get; private init; }
    public bool    IsCancelled   { get; private init; }
    public bool    IsPinFallback { get; private init; }
    public string? ErrorMessage  { get; private init; }
}
