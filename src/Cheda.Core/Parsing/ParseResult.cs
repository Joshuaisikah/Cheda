using Cheda.Core.Models;

namespace Cheda.Core.Parsing;

public sealed class ParseResult
{
    public bool Success { get; init; }
    public Transaction? Transaction { get; init; }

    public static ParseResult Ok(Transaction t) => new() { Success = true, Transaction = t };
    public static ParseResult Fail() => new() { Success = false };
}
