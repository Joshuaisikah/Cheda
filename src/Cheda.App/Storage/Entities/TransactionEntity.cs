using Cheda.Core.Models;
using SQLite;

namespace Cheda.App.Storage.Entities;

[Table("Transactions")]
internal sealed class TransactionEntity
{
    [PrimaryKey]
    public string Id { get; set; } = "";

    // Deduplication key: "{TransactionCode}:{SourceInt}"
    [Unique]
    public string DedupKey { get; set; } = "";

    public string TransactionCode { get; set; } = "";
    public int Source { get; set; }
    public decimal Amount { get; set; }
    public int Type { get; set; }
    public string? Counterparty { get; set; }
    public decimal? BalanceAfter { get; set; }
    public decimal? TransactionCost { get; set; }
    public long TimestampTicks { get; set; }
    public int TimestampOffsetMinutes { get; set; }
    public string RawMessage { get; set; } = "";
    public string? Category { get; set; }
    public double CategoryConfidence { get; set; }
    public string? MatchedRule { get; set; }
    public bool IsNonExpenseTransfer { get; set; }
    public string? ReversesTransactionCode { get; set; }
    public int? SimSlot { get; set; }

    internal static TransactionEntity From(Transaction t) => new()
    {
        Id                      = t.Id.ToString(),
        DedupKey                = MakeDedupKey(t.TransactionCode, t.Source),
        TransactionCode         = t.TransactionCode,
        Source                  = (int)t.Source,
        Amount                  = t.Amount,
        Type                    = (int)t.Type,
        Counterparty            = t.Counterparty,
        BalanceAfter            = t.BalanceAfter,
        TransactionCost         = t.TransactionCost,
        TimestampTicks          = t.Timestamp.UtcTicks,
        TimestampOffsetMinutes  = (int)t.Timestamp.Offset.TotalMinutes,
        RawMessage              = t.RawMessage,
        Category                = t.Category,
        CategoryConfidence      = t.CategoryConfidence,
        MatchedRule             = t.MatchedRule,
        IsNonExpenseTransfer    = t.IsNonExpenseTransfer,
        ReversesTransactionCode = t.ReversesTransactionCode,
        SimSlot                 = t.SimSlot,
    };

    internal Transaction ToDomain() => new()
    {
        Id                      = Guid.Parse(Id),
        TransactionCode         = TransactionCode,
        Source                  = (TransactionSource)Source,
        Amount                  = Amount,
        Type                    = (TransactionType)Type,
        Counterparty            = Counterparty,
        BalanceAfter            = BalanceAfter,
        TransactionCost         = TransactionCost,
        Timestamp               = new DateTimeOffset(TimestampTicks, TimeSpan.FromMinutes(TimestampOffsetMinutes)),
        RawMessage              = RawMessage,
        Category                = Category,
        CategoryConfidence      = CategoryConfidence,
        MatchedRule             = MatchedRule,
        IsNonExpenseTransfer    = IsNonExpenseTransfer,
        ReversesTransactionCode = ReversesTransactionCode,
        SimSlot                 = SimSlot,
    };

    internal static string MakeDedupKey(string code, TransactionSource source) =>
        $"{code}:{(int)source}";
}
