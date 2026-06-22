namespace Cheda.Core.Categorization;

/// <summary>
/// Persistence contract for rules and learned mappings.
/// Implemented in the MAUI layer (SQLite); in tests use InMemoryCategorizerStore.
/// </summary>
public interface ICategorizerStore
{
    IReadOnlyList<RecipientRule> GetRecipientRules();
    IReadOnlyList<PatternRule> GetPatternRules();
    IReadOnlyList<LearnedMapping> GetLearnedMappings();
    void UpsertLearnedMapping(LearnedMapping mapping);
    void SaveRecipientRules(IReadOnlyList<RecipientRule> rules);
    void SavePatternRules(IReadOnlyList<PatternRule> rules);
}
