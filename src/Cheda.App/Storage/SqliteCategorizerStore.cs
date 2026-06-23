using Cheda.App.Storage.Entities;
using Cheda.Core.Categorization;
using SQLite;

namespace Cheda.App.Storage;

public sealed class SqliteCategorizerStore : ICategorizerStore
{
    private SQLiteConnection Db => _db.Db;
    private readonly DatabaseService _db;

    public SqliteCategorizerStore(DatabaseService db) => _db = db;

    public IReadOnlyList<RecipientRule> GetRecipientRules() =>
        Db.Table<RecipientRuleEntity>()
          .OrderBy(r => r.Priority)
          .ToList()
          .Select(e => e.ToDomain())
          .ToList();

    public IReadOnlyList<PatternRule> GetPatternRules() =>
        Db.Table<PatternRuleEntity>()
          .OrderBy(r => r.Priority)
          .ToList()
          .Select(e => e.ToDomain())
          .ToList();

    public IReadOnlyList<LearnedMapping> GetLearnedMappings() =>
        Db.Table<LearnedMappingEntity>()
          .ToList()
          .Select(e => e.ToDomain())
          .ToList();

    public void UpsertLearnedMapping(LearnedMapping mapping)
    {
        var entity = LearnedMappingEntity.From(mapping);
        var existing = Db.Table<LearnedMappingEntity>()
                         .FirstOrDefault(e => e.Key == entity.Key);
        if (existing is null)
            Db.Insert(entity);
        else
        {
            entity.Id = existing.Id;
            Db.Update(entity);
        }
    }

    public void SaveRecipientRules(IReadOnlyList<RecipientRule> rules)
    {
        Db.RunInTransaction(() =>
        {
            Db.DeleteAll<RecipientRuleEntity>();
            Db.InsertAll(rules.Select(RecipientRuleEntity.From));
        });
    }

    public void SavePatternRules(IReadOnlyList<PatternRule> rules)
    {
        Db.RunInTransaction(() =>
        {
            Db.DeleteAll<PatternRuleEntity>();
            Db.InsertAll(rules.Select(PatternRuleEntity.From));
        });
    }
}
