using System.Text.RegularExpressions;
using Cheda.Core.Models;

namespace Cheda.Core.Parsing.Parsers;

/// <summary>
/// Parses Equity Bank confirmation SMS messages.
/// Format: "Confirmed, Bill payment to {merchant} of KES. {amount} for account {acc} and Ref. {ref} on {date} at {time}.Thank you."
/// </summary>
public sealed partial class EquityBankParser : ISourceParser
{
    private static readonly HashSet<string> KnownSenders =
        new(StringComparer.OrdinalIgnoreCase) { "Equity Bank", "EquityBank", "0763000000" };

    public TransactionSource Source => TransactionSource.Equity;

    public bool CanHandle(string sender, string body) =>
        KnownSenders.Contains(sender) &&
        (body.Contains("Confirmed,", StringComparison.OrdinalIgnoreCase) ||
         body.Contains("Transaction", StringComparison.OrdinalIgnoreCase));

    public ParseResult Parse(string sender, string body, DateTimeOffset timestamp)
    {
        var tx = TryParseBillPayment(body, timestamp)
            ?? TryParseTransfer(body, timestamp)
            ?? BuildUnknown(body, timestamp);
        return ParseResult.Ok(tx);
    }

    // ── Bill payment ─────────────────────────────────────────────────────────
    // "Confirmed, Bill payment to LUDA FITNESS GYM of KES. 150.00 for account 381122  and Ref. UAQ2Q4WFYS on 26-01-2026 at 19:09.Thank you."
    [GeneratedRegex(
        @"Bill payment to (?<merchant>.+?) of KES[.\s]+(?<amount>[\d,]+\.?\d*) for account (?<account>\S+)\s+and Ref\. (?<ref>\S+) on (?<date>[\d\-/]+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex BillPaymentRegex();

    private static Transaction? TryParseBillPayment(string body, DateTimeOffset timestamp)
    {
        var m = BillPaymentRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode = m.Groups["ref"].Value,
            Source          = TransactionSource.Equity,
            Amount          = ParseAmount(m.Groups["amount"].Value),
            Type            = TransactionType.PaidPaybill,
            Counterparty    = TitleCase(m.Groups["merchant"].Value.Trim()),
            Timestamp       = timestamp,
            RawMessage      = body,
        };
    }

    // ── Generic transfer / credit ────────────────────────────────────────────
    // "Your account ... has been credited with KES. 500.00 from ..."
    [GeneratedRegex(
        @"(?:credited|debited|received)\s+(?:with\s+)?KES[.\s]+(?<amount>[\d,]+\.?\d*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex TransferRegex();

    private static Transaction? TryParseTransfer(string body, DateTimeOffset timestamp)
    {
        var m = TransferRegex().Match(body);
        if (!m.Success) return null;
        var isCredit = body.Contains("credited", StringComparison.OrdinalIgnoreCase);
        return new Transaction
        {
            TransactionCode = "",
            Source          = TransactionSource.Equity,
            Amount          = ParseAmount(m.Groups["amount"].Value),
            Type            = isCredit ? TransactionType.Received : TransactionType.Sent,
            Counterparty    = "Equity Bank",
            Timestamp       = timestamp,
            RawMessage      = body,
        };
    }

    private static Transaction BuildUnknown(string body, DateTimeOffset timestamp) => new()
    {
        TransactionCode = "",
        Source          = TransactionSource.Equity,
        Amount          = 0,
        Type            = TransactionType.Unknown,
        Timestamp       = timestamp,
        RawMessage      = body,
    };

    private static decimal ParseAmount(string raw) =>
        decimal.TryParse(raw.Replace(",", ""), out var d) ? d : 0m;

    private static string TitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var info = System.Globalization.CultureInfo.InvariantCulture.TextInfo;
        return info.ToTitleCase(s.ToLowerInvariant());
    }
}
