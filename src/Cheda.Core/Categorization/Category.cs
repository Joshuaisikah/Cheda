namespace Cheda.Core.Categorization;

public sealed record Category(
    string Name,
    CategoryGroup Group,
    bool IsSystem = true   // false for user-created categories
);
