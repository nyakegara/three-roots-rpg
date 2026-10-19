using Game.Core.Geometry;

namespace Game.Core.World;

/// <summary>A named location on the exploration map, such as a quest, treasure, or recruitment site.</summary>
public sealed class WorldPointOfInterest
{
    public string Id { get; }
    public string Type { get; }
    public string Name { get; }
    public HexCoord Coordinate { get; }

    public WorldPointOfInterest(string id, string type, string name, HexCoord coordinate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Type = type;
        Name = name;
        Coordinate = coordinate;
    }
}
