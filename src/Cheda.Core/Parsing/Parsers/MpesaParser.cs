using System.Text.RegularExpressions;
using Cheda.Core.Models;

namespace Cheda.Core.Parsing.Parsers;

/// <summary>
/// Parses M-Pesa confirmation SMS messages.
/// One private static Regex per message type — add new patterns here to extend.
/// Only handles messages from the known M-Pesa sender; ignores OTP/marketing.
/// </summary>
public sealed partial class MpesaParser : ISourceParser
{
    private const string KnownSender = "MPESA";

    public TransactionSource Source => TransactionSource.Mpesa;

    public bool CanHandle(string sender, string body) =>
        sender.Equals(KnownSender, StringComparison.OrdinalIgnoreCase) &&
        IsTransactionMessage(body);

    public ParseResult Parse(string sender, string body, DateTimeOffset timestamp)
    {
        var tx = TryParseSent(body, timestamp)
            ?? TryParseReceived(body, timestamp)
            ?? TryParsePaidTill(body, timestamp)
            ?? TryParsePaidPaybill(body, timestamp)
            ?? TryParseWithdrawn(body, timestamp)
            ?? TryParseDeposit(body, timestamp)
            ?? TryParseAirtime(body, timestamp)
            ?? TryParseFuliza(body, timestamp)
            ?? TryParseMShwari(body, timestamp)
            ?? TryParseReversal(body, timestamp)
            ?? BuildUnknown(body, timestamp);

        return ParseResult.Ok(tx);
    }

    // ── Sentinel check ─────────────────────────────────────────────────────────
    // Reject OTP and marketing messages quickly before trying any regex.
    private static bool IsTransactionMessage(string body)
    {
        // Must contain an M-Pesa transaction code (e.g. "QGH2XK1A23")
        return TransactionCodeRegex().IsMatch(body);
    }

    // ── Sent money ─────────────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. Ksh1,000.00 sent to JOHN DOE 0712345678 on 15/6/25 at 10:30 AM. New M-PESA balance is Ksh4,500.00. Transaction cost, Ksh11.00."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\.\s+Ksh(?<amount>[\d,]+\.?\d*)\s+sent to\s+(?<counterparty>.+?)\s+on\s+\d",
        RegexOptions.IgnoreCase)]
    private static partial Regex SentRegex();

    private static Transaction? TryParseSent(string body, DateTimeOffset timestamp)
    {
        var m = SentRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = ParseAmount(m.Groups["amount"].Value),
            Type = TransactionType.Sent,
            Counterparty = m.Groups["counterparty"].Value.Trim(),
            BalanceAfter = ExtractBalance(body),
            TransactionCost = ExtractCost(body),
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── Received money ─────────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. You have received Ksh500.00 from JANE DOE 0712345678 on 15/6/25 at 2:00 PM. New M-PESA balance is Ksh5,000.00."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\.\s+You have received\s+Ksh(?<amount>[\d,]+\.?\d*)\s+from\s+(?<counterparty>.+?)\s+on\s+\d",
        RegexOptions.IgnoreCase)]
    private static partial Regex ReceivedRegex();

    private static Transaction? TryParseReceived(string body, DateTimeOffset timestamp)
    {
        var m = ReceivedRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = ParseAmount(m.Groups["amount"].Value),
            Type = TransactionType.Received,
            Counterparty = m.Groups["counterparty"].Value.Trim(),
            BalanceAfter = ExtractBalance(body),
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── Pay till (Buy Goods) ────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. Ksh250.00 paid to JAVA HOUSE Till 123456 on 15/6/25 at 1:15 PM. New M-PESA balance is Ksh3,250.00. Transaction cost, Ksh0.00."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\.\s+Ksh(?<amount>[\d,]+\.?\d*)\s+paid to\s+(?<counterparty>.+?)\s+Till\s+(?<till>\d+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex PaidTillRegex();

    private static Transaction? TryParsePaidTill(string body, DateTimeOffset timestamp)
    {
        var m = PaidTillRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = ParseAmount(m.Groups["amount"].Value),
            Type = TransactionType.PaidTill,
            Counterparty = $"{m.Groups["counterparty"].Value.Trim()} (Till {m.Groups["till"].Value})",
            BalanceAfter = ExtractBalance(body),
            TransactionCost = ExtractCost(body),
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── Pay paybill ─────────────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. Ksh2,500.00 paid to KPLC PREPAID Paybill 888880 account 54321 on 15/6/25 at 8:00 AM. New M-PESA balance is Ksh1,500.00. Transaction cost, Ksh33.00."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\.\s+Ksh(?<amount>[\d,]+\.?\d*)\s+paid to\s+(?<counterparty>.+?)\s+Paybill\s+(?<paybill>\d+)\s+account\s+(?<account>\S+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex PaidPaybillRegex();

    private static Transaction? TryParsePaidPaybill(string body, DateTimeOffset timestamp)
    {
        var m = PaidPaybillRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = ParseAmount(m.Groups["amount"].Value),
            Type = TransactionType.PaidPaybill,
            Counterparty = $"{m.Groups["counterparty"].Value.Trim()} ({m.Groups["paybill"].Value}/{m.Groups["account"].Value})",
            BalanceAfter = ExtractBalance(body),
            TransactionCost = ExtractCost(body),
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── Withdrawal (agent) ──────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. On 15/6/25 at 3:00 PM Withdraw Ksh3,000.00 from Agent 123456 - JOHN AGENT New M-PESA balance is Ksh2,000.00. Transaction cost, Ksh33.00."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\..+?Withdraw\s+Ksh(?<amount>[\d,]+\.?\d*)\s+from\s+(?<counterparty>.+?)\s+New M-PESA",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WithdrawnRegex();

    private static Transaction? TryParseWithdrawn(string body, DateTimeOffset timestamp)
    {
        var m = WithdrawnRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = ParseAmount(m.Groups["amount"].Value),
            Type = TransactionType.Withdrawn,
            Counterparty = m.Groups["counterparty"].Value.Trim(),
            BalanceAfter = ExtractBalance(body),
            TransactionCost = ExtractCost(body),
            Timestamp = timestamp,
            RawMessage = body,
            IsNonExpenseTransfer = true,
        };
    }

    // ── Deposit (agent) ─────────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. You have deposited Ksh1,000.00 to your M-PESA account. New M-PESA balance is Ksh6,000.00."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\.\s+You have deposited\s+Ksh(?<amount>[\d,]+\.?\d*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DepositRegex();

    private static Transaction? TryParseDeposit(string body, DateTimeOffset timestamp)
    {
        var m = DepositRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = ParseAmount(m.Groups["amount"].Value),
            Type = TransactionType.Deposit,
            BalanceAfter = ExtractBalance(body),
            Timestamp = timestamp,
            RawMessage = body,
            IsNonExpenseTransfer = true,
        };
    }

    // ── Airtime purchase ────────────────────────────────────────────────────────
    // "QGH2XK1A23 confirmed. You have bought Ksh50.00 of airtime on 15/6/25 at 7:00 AM. New M-PESA balance is Ksh950.00."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+confirmed\.\s+You have bought\s+Ksh(?<amount>[\d,]+\.?\d*)\s+of airtime",
        RegexOptions.IgnoreCase)]
    private static partial Regex AirtimeRegex();

    private static Transaction? TryParseAirtime(string body, DateTimeOffset timestamp)
    {
        var m = AirtimeRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = ParseAmount(m.Groups["amount"].Value),
            Type = TransactionType.Airtime,
            BalanceAfter = ExtractBalance(body),
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── Fuliza drawdown ─────────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. You have used Fuliza M-PESA for Ksh200.00. Repay by 22/6/25. Fuliza M-PESA balance is Ksh500.00."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\.\s+You have used Fuliza M-PESA for\s+Ksh(?<amount>[\d,]+\.?\d*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex FulizaRegex();

    private static Transaction? TryParseFuliza(string body, DateTimeOffset timestamp)
    {
        var m = FulizaRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = ParseAmount(m.Groups["amount"].Value),
            Type = TransactionType.Fuliza,
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── M-Shwari ────────────────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. Ksh1,000.00 transferred to M-Shwari Lock Savings Account on 15/6/25. New M-PESA balance is Ksh500.00."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\.\s+Ksh(?<amount>[\d,]+\.?\d*)\s+transferred to M-Shwari",
        RegexOptions.IgnoreCase)]
    private static partial Regex MShwariRegex();

    private static Transaction? TryParseMShwari(string body, DateTimeOffset timestamp)
    {
        var m = MShwariRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = ParseAmount(m.Groups["amount"].Value),
            Type = TransactionType.MShwari,
            Counterparty = "M-Shwari",
            BalanceAfter = ExtractBalance(body),
            Timestamp = timestamp,
            RawMessage = body,
            IsNonExpenseTransfer = true,
        };
    }

    // ── Reversal ─────────────────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. Your transaction of Ksh500.00 to JOHN DOE has been reversed. The reversal for ABC1234567 is complete. New M-PESA balance is Ksh5,500.00."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\..+?reversed.+?reversal for\s+(?<original>[A-Z0-9]{10})",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ReversalRegex();

    [GeneratedRegex(@"Ksh(?<amount>[\d,]+\.?\d*).+?reversed", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ReversalAmountRegex();

    private static Transaction? TryParseReversal(string body, DateTimeOffset timestamp)
    {
        var m = ReversalRegex().Match(body);
        if (!m.Success) return null;

        var amountMatch = ReversalAmountRegex().Match(body);
        var amount = amountMatch.Success ? ParseAmount(amountMatch.Groups["amount"].Value) : 0m;

        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = amount,
            Type = TransactionType.Reversal,
            ReversesTransactionCode = m.Groups["original"].Value,
            BalanceAfter = ExtractBalance(body),
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── Unknown (never dropped) ──────────────────────────────────────────────────
    private static Transaction BuildUnknown(string body, DateTimeOffset timestamp)
    {
        var code = TransactionCodeRegex().Match(body);
        return new Transaction
        {
            TransactionCode = code.Success ? code.Value : Guid.NewGuid().ToString("N")[..10].ToUpper(),
            Source = TransactionSource.Mpesa,
            Amount = 0m,
            Type = TransactionType.Unknown,
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── Shared helpers ───────────────────────────────────────────────────────────
    [GeneratedRegex(@"[A-Z0-9]{10}", RegexOptions.None)]
    private static partial Regex TransactionCodeRegex();

    [GeneratedRegex(@"balance is Ksh(?<bal>[\d,]+\.?\d*)", RegexOptions.IgnoreCase)]
    private static partial Regex BalanceRegex();

    [GeneratedRegex(@"Transaction cost,\s+Ksh(?<cost>[\d,]+\.?\d*)", RegexOptions.IgnoreCase)]
    private static partial Regex CostRegex();

    private static decimal ParseAmount(string raw) =>
        decimal.Parse(raw.Replace(",", ""), System.Globalization.CultureInfo.InvariantCulture);

    private static decimal? ExtractBalance(string body)
    {
        var m = BalanceRegex().Match(body);
        return m.Success ? ParseAmount(m.Groups["bal"].Value) : null;
    }

    private static decimal? ExtractCost(string body)
    {
        var m = CostRegex().Match(body);
        return m.Success ? ParseAmount(m.Groups["cost"].Value) : null;
    }
}
