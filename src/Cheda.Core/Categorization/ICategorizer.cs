using Cheda.Core.Models;

namespace Cheda.Core.Categorization;

public interface ICategorizer
{
    CategorizationResult Categorize(Transaction transaction);

    /// <summary>
    /// Records a user correction and feeds it into learned memory so the same
    /// pattern is not flagged for review again.
    /// </summary>
    void LearnFromCorrection(Transaction transaction, string category);

    /// <summary>
    /// Records temporal and amount data from a high-confidence auto-categorized transaction.
    /// Updates the amount band and time-of-day fingerprint on the existing learned mapping
    /// for this counterparty. No-ops if no learned mapping exists yet.
    /// Call only when confidence is high enough to trust the existing categorization.
    /// </summary>
    void ObserveTransaction(Transaction transaction);
}
