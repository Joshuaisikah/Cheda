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
}
