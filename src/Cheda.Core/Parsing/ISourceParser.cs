using Cheda.Core.Models;

namespace Cheda.Core.Parsing;

/// <summary>
/// Implemented once per financial source (M-Pesa, Equity, etc.).
/// The engine calls CanHandle first; if true, calls Parse.
/// </summary>
public interface ISourceParser
{
    TransactionSource Source { get; }

    /// <summary>Returns true if this parser recognises the sender + message body.</summary>
    bool CanHandle(string sender, string body);

    /// <summary>Parses the message. Never returns null; unknown formats → Type.Unknown.</summary>
    ParseResult Parse(string sender, string body, DateTimeOffset timestamp);
}
