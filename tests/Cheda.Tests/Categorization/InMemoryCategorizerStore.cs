using Cheda.Core.Categorization;

namespace Cheda.Tests.Categorization;

internal sealed class InMemoryCategorizerStore : ICategorizerStore
{
    private readonly List<RecipientRule> _recipientRules = [];
    private readonly List<PatternRule> _patternRules = [];
    private readonly Dictionary<string, LearnedMapping> _learnedMappings =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<RecipientRule> GetRecipientRules() => _recipientRules;
    public IReadOnlyList<PatternRule> GetPatternRules() => _patternRules;
    public IReadOnlyList<LearnedMapping> GetLearnedMappings() => [.. _learnedMappings.Values];

    public void UpsertLearnedMapping(LearnedMapping mapping) =>
        _learnedMappings[mapping.Key] = mapping;

    public void SaveRecipientRules(IReadOnlyList<RecipientRule> rules)
    {
        _recipientRules.Clear();
        _recipientRules.AddRange(rules);
    }

    public void SavePatternRules(IReadOnlyList<PatternRule> rules)
    {
        _patternRules.Clear();
        _patternRules.AddRange(rules);
    }

    public void Add(RecipientRule rule) => _recipientRules.Add(rule);
    public void Add(PatternRule rule) => _patternRules.Add(rule);
}
