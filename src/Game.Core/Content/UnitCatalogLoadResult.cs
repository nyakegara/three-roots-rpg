namespace Game.Core.Content;

public sealed record UnitCatalogLoadResult(
    bool Success,
    UnitCatalog? Catalog,
    IReadOnlyList<ContentError> Errors);
