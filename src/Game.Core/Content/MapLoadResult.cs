using Game.Core.World;

namespace Game.Core.Content;

public sealed record MapLoadResult(bool Success, WorldMap? Map, IReadOnlyList<ContentError> Errors);
