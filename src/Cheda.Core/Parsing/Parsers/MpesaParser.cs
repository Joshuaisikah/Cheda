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
        var tx = TryParseZidii(body, timestamp)         // savings products must match before generic Sent
            ?? TryParseKcbMpesa(body, timestamp)
            ?? TryParseMShwari(body, timestamp)
            ?? TryParseSentPaybill(body, timestamp)     // "sent to X for account Y" — match before generic Sent
            ?? TryParseSent(body, timestamp)
            ?? TryParseReceived(body, timestamp)
            ?? TryParseBuyGoods(body, timestamp)        // "paid to X. on" — real buy-goods format (no Till number in msg)
            ?? TryParsePaidTill(body, timestamp)         // legacy: "paid to X Till 123"
            ?? TryParsePaidPaybill(body, timestamp)      // legacy: "paid to X Paybill 123 account Y"
            ?? TryParseWithdrawn(body, timestamp)
            ?? TryParseDeposit(body, timestamp)
            ?? TryParseAirtime(body, timestamp)
            ?? TryParseFuliza(body, timestamp)
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

    // ── Sent to paybill ("sent to X for account Y") ────────────────────────────
    // Real format: "Ksh200.00 sent to KPLC PREPAID for account 45136199804 on 9/5/26"
    // Also matches data bundles: "sent to SAFARICOM DATA BUNDLES for account SAFARICOM DATA BUNDLES"
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\.\s+Ksh(?<amount>[\d,]+\.?\d*)\s+sent to\s+(?<counterparty>.+?)\s+for account\s+(?<account>.+?)\s+on\s+\d",
        RegexOptions.IgnoreCase)]
    private static partial Regex SentPaybillRegex();

    private static Transaction? TryParseSentPaybill(string body, DateTimeOffset timestamp)
    {
        var m = SentPaybillRegex().Match(body);
        if (!m.Success) return null;
        var biz     = Normalize(m.Groups["counterparty"].Value);
        var account = m.Groups["account"].Value.Trim();
        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = ParseAmount(m.Groups["amount"].Value),
            Type = TransactionType.PaidPaybill,
            Counterparty = account.Equals(biz, StringComparison.OrdinalIgnoreCase)
                ? biz                           // e.g. "SAFARICOM DATA BUNDLES for account SAFARICOM DATA BUNDLES"
                : $"{biz} ({account})",
            BalanceAfter = ExtractBalance(body),
            TransactionCost = ExtractCost(body),
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── Sent money ─────────────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. Ksh1,000.00 sent to JOHN DOE 0712345678 on 15/6/25 at 10:30 AM."
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
            Counterparty = Normalize(m.Groups["counterparty"].Value),
            BalanceAfter = ExtractBalance(body),
            TransactionCost = ExtractCost(body),
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── Received money ─────────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed.You have received Ksh500.00 from JANE DOE 0712345678 on 15/6/25 at 2:00 PM."
    // Note: real messages have no space between "Confirmed." and "You" — \s* handles both.
    // Masked phones (0716***698) are kept in counterparty as received from M-PESA.
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\.\s*You have received\s+Ksh(?<amount>[\d,]+\.?\d*)\s+from\s+(?<counterparty>.+?)\s+on\s+\d",
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
            Counterparty = Normalize(m.Groups["counterparty"].Value),
            BalanceAfter = ExtractBalance(body),
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── Buy Goods (real format — no Till number in message) ────────────────────
    // Real format: "Ksh100.00 paid to MAXWELL CHEMIST. on 6/5/26 at 5:48 PM."
    // The period after the merchant name is the discriminator. "via MPAYA" (mobile agent) included.
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed\.\s+Ksh(?<amount>[\d,]+\.?\d*)\s+paid to\s+(?<counterparty>.+?)\.\s+on\s+\d",
        RegexOptions.IgnoreCase)]
    private static partial Regex BuyGoodsRegex();

    private static Transaction? TryParseBuyGoods(string body, DateTimeOffset timestamp)
    {
        var m = BuyGoodsRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode = m.Groups["code"].Value,
            Source = TransactionSource.Mpesa,
            Amount = ParseAmount(m.Groups["amount"].Value),
            Type = TransactionType.PaidTill,
            Counterparty = Normalize(m.Groups["counterparty"].Value),
            BalanceAfter = ExtractBalance(body),
            TransactionCost = ExtractCost(body),
            Timestamp = timestamp,
            RawMessage = body,
        };
    }

    // ── Pay till — legacy (explicit Till number in message) ────────────────────
    // "QGH2XK1A23 Confirmed. Ksh250.00 paid to JAVA HOUSE Till 123456 on 15/6/25 at 1:15 PM."
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
    // Real: "confirmed.You bought Ksh5.00 of airtime" (no space, no "have")
    // Also: "confirmed. You have bought Ksh50.00 of airtime" (space + "have")
    // \s* and (?:have )? handle both variants.
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+confirmed\.\s*You (?:have )?bought\s+Ksh(?<amount>[\d,]+\.?\d*)\s+of airtime",
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

    // ── Zidii savings ───────────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. Ksh1,000.00 saved to Zidii Goal: Holiday Fund. New M-PESA balance is Ksh500.00."
    // Also: "sent to ZIDII SAVINGS for account..."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed[^.]*?\.\s+Ksh(?<amount>[\d,]+\.?\d*)\s+(saved to Zidii|sent to ZIDII)",
        RegexOptions.IgnoreCase)]
    private static partial Regex ZidiiRegex();

    private static Transaction? TryParseZidii(string body, DateTimeOffset timestamp)
    {
        var m = ZidiiRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode      = m.Groups["code"].Value,
            Source               = TransactionSource.Mpesa,
            Amount               = ParseAmount(m.Groups["amount"].Value),
            Type                 = TransactionType.Zidii,
            Counterparty         = "Zidii Savings",
            BalanceAfter         = ExtractBalance(body),
            Timestamp            = timestamp,
            RawMessage           = body,
            IsNonExpenseTransfer = true,
        };
    }

    // ── KCB M-Pesa savings ───────────────────────────────────────────────────────
    // "QGH2XK1A23 Confirmed. Ksh500.00 sent to KCB MPESA SAVINGS ACCOUNT on 10/6/26..."
    // "QGH2XK1A23 Confirmed. Ksh500.00 sent to KCB M-PESA for account..."
    [GeneratedRegex(
        @"(?<code>[A-Z0-9]{10})\s+Confirmed[^.]*?\.\s+Ksh(?<amount>[\d,]+\.?\d*)\s+sent to KCB\s*M-?PESA",
        RegexOptions.IgnoreCase)]
    private static partial Regex KcbMpesaRegex();

    private static Transaction? TryParseKcbMpesa(string body, DateTimeOffset timestamp)
    {
        var m = KcbMpesaRegex().Match(body);
        if (!m.Success) return null;
        return new Transaction
        {
            TransactionCode      = m.Groups["code"].Value,
            Source               = TransactionSource.Mpesa,
            Amount               = ParseAmount(m.Groups["amount"].Value),
            Type                 = TransactionType.KcbMpesa,
            Counterparty         = "KCB M-Pesa",
            BalanceAfter         = ExtractBalance(body),
            Timestamp            = timestamp,
            RawMessage           = body,
            IsNonExpenseTransfer = true,
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

    // Collapses double-spaces that M-PESA includes between first name and surname.
    private static string Normalize(string raw) =>
        string.Join(' ', raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

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
