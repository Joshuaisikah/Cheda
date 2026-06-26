namespace Cheda.Core.Models;

public sealed class Transaction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string TransactionCode { get; init; }
    public required TransactionSource Source { get; init; }
    public required decimal Amount { get; init; }
    public required TransactionType Type { get; init; }
    public string? Counterparty { get; init; }
    public decimal? BalanceAfter { get; init; }
    public decimal? TransactionCost { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string RawMessage { get; init; }

    // Categorization — set by ICategorizer, not the parser
    public string? Category { get; set; }
    public double CategoryConfidence { get; set; }
    public string? MatchedRule { get; set; }

    // True for withdrawals, savings moves, own-account transfers — excluded from expense analytics
    public bool IsNonExpenseTransfer { get; set; }

    // User-written note attached to this transaction
    public string? Note { get; set; }

    // Links a reversal back to the transaction it cancels
    public string? ReversesTransactionCode { get; set; }

    // Dual-SIM: raw Android subscription_id of the SIM that received the message.
    // Null on single-SIM devices or when imported from sources other than live SMS.
    public int? SimSlot { get; set; }

    // Self-transfers: when the same transaction code arrives on both SIMs, only the
    // first is stored. These fields capture the OTHER SIM's post-transfer balance so
    // the dashboard can show accurate balances for both slots.
    public int?     SelfTransferSimSlot      { get; set; }
    public decimal? SelfTransferBalanceAfter { get; set; }

    // Fuliza: total limit extracted from repayment confirmations.
    // Null on all non-Fuliza transactions.
    public decimal? FulizaLimit { get; set; }
}
