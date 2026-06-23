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

    // Links a reversal back to the transaction it cancels
    public string? ReversesTransactionCode { get; set; }

    // Dual-SIM: raw Android subscription_id of the SIM that received the message.
    // Null on single-SIM devices or when imported from sources other than live SMS.
    public int? SimSlot { get; set; }
}
