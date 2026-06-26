using Cheda.Core.Models;
using Cheda.Core.Storage;

namespace Cheda.Core.Sms;

/// <summary>
/// Identifies transfers between the user's own M-Pesa SIM lines.
///
/// Primary detection (real-time, zero false-positives):
///   M-Pesa sends the SAME transaction code to both the sender's SIM and the
///   receiver's SIM.  When the inbox is scanned, both messages are present.
///   The dedup logic stores whichever is processed first; when the second arrives
///   we detect the code-type mismatch and retroactively mark the stored record.
///
/// Retroactive detection (for data already in the database):
///   Match Sent/Received pairs with identical amounts, different SimSlots, and
///   timestamps within 10 minutes.  This is accurate when SimSlot is populated;
///   it falls back gracefully to a time+amount match when SimSlot is absent.
/// </summary>
public static class SelfTransferDetector
{
    public const string OwnTransferCategory = "Own Transfer";

    private static readonly TimeSpan MatchWindow = TimeSpan.FromMinutes(10);

    // ── Real-time detection ──────────────────────────────────────────────────

    /// <summary>
    /// Called by ImportService when a transaction is rejected as a duplicate.
    /// If the incoming transaction has a different type from the already-stored one
    /// (Sent vs Received), the pair is an inter-SIM transfer: mark the stored record.
    /// Returns true if the existing record was updated.
    /// </summary>
    public static bool TryMarkExistingAsOwnTransfer(
        Transaction incoming,
        ITransactionRepository repository)
    {
        if (incoming.Type is not (TransactionType.Sent or TransactionType.Received))
            return false;

        var existing = repository.GetByCode(incoming.TransactionCode, incoming.Source);
        if (existing is null) return false;

        // Only flag when the two sides are genuinely different types.
        if (existing.Type == incoming.Type) return false;
        if (existing.Type is not (TransactionType.Sent or TransactionType.Received)) return false;

        // Already tagged — but might still need to capture the other SIM's balance.
        if (existing.IsNonExpenseTransfer && existing.Category == OwnTransferCategory)
        {
            // If we don't yet have the other-SIM balance and the incoming has one, capture it now.
            if (!existing.SelfTransferSimSlot.HasValue
                && incoming.SimSlot.HasValue && incoming.BalanceAfter.HasValue
                && incoming.SimSlot != existing.SimSlot)
            {
                existing.SelfTransferSimSlot      = incoming.SimSlot;
                existing.SelfTransferBalanceAfter = incoming.BalanceAfter;
                repository.Update(existing);
            }
            return false;
        }

        existing.IsNonExpenseTransfer = true;
        existing.Category             = OwnTransferCategory;

        // Capture the other SIM's post-transfer balance so the dashboard shows
        // accurate balances for both SIM slots even though only one record is stored.
        if (incoming.SimSlot.HasValue && incoming.BalanceAfter.HasValue
            && incoming.SimSlot != existing.SimSlot)
        {
            existing.SelfTransferSimSlot      = incoming.SimSlot;
            existing.SelfTransferBalanceAfter = incoming.BalanceAfter;
        }

        repository.Update(existing);
        return true;
    }

    // ── Retroactive detection ────────────────────────────────────────────────

    /// <summary>
    /// Scans the full transaction history and returns all records that look like
    /// inter-SIM transfers.  Suitable for a one-shot "re-detect" pass over existing data.
    ///
    /// Logic:
    ///   1. Group Sent and Received transactions by amount.
    ///   2. For each Sent, find a Received within the time window.
    ///   3. Prefer pairs on different SimSlots; accept any pair when SimSlot is absent.
    ///   4. Return both members of each confirmed pair (excluding already-tagged ones).
    /// </summary>
    public static IReadOnlyList<Transaction> FindUntaggedOwnTransfers(
        IReadOnlyList<Transaction> all)
    {
        var sent     = all.Where(t => t.Type == TransactionType.Sent).ToList();
        var received = all.Where(t => t.Type == TransactionType.Received).ToList();

        var toTag   = new HashSet<Guid>();
        var matched = new HashSet<Guid>(); // prevent one record being used in multiple pairs

        foreach (var s in sent)
        {
            if (matched.Contains(s.Id)) continue;

            foreach (var r in received)
            {
                if (matched.Contains(r.Id)) continue;
                if (r.Amount != s.Amount) continue;

                var gap = (r.Timestamp - s.Timestamp).Duration();
                if (gap > MatchWindow) continue;

                // Require different SIM slots when both are known.
                if (s.SimSlot.HasValue && r.SimSlot.HasValue && s.SimSlot == r.SimSlot)
                    continue;

                // Confirmed pair — queue both for tagging if not already tagged.
                if (!(s.IsNonExpenseTransfer && s.Category == OwnTransferCategory))
                    toTag.Add(s.Id);
                if (!(r.IsNonExpenseTransfer && r.Category == OwnTransferCategory))
                    toTag.Add(r.Id);

                matched.Add(s.Id);
                matched.Add(r.Id);
                break; // each Sent matches at most one Received
            }
        }

        return all.Where(t => toTag.Contains(t.Id)).ToList();
    }

    /// <summary>
    /// Convenience: run retroactive detection and persist all tagged records.
    /// Returns the number of transactions updated.
    /// </summary>
    public static int RedetectAndPersist(ITransactionRepository repository)
    {
        var all    = repository.GetAll();
        var toTag  = FindUntaggedOwnTransfers(all);
        foreach (var tx in toTag)
        {
            tx.IsNonExpenseTransfer = true;
            tx.Category             = OwnTransferCategory;
            repository.Update(tx);
        }
        return toTag.Count;
    }
}
