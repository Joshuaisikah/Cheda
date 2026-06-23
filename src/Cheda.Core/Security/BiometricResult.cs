namespace Cheda.Core.Security;

public sealed class BiometricResult
{
    public static readonly BiometricResult Success   = new() { IsSuccess   = true };
    public static readonly BiometricResult Cancelled = new() { IsCancelled = true };

    public static BiometricResult Error(string message) =>
        new() { ErrorMessage = message };

    public bool    IsSuccess    { get; private init; }
    public bool    IsCancelled  { get; private init; }
    public string? ErrorMessage { get; private init; }
}
