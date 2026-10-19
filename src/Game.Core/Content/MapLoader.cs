using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Core.Geometry;
using Game.Core.World;

namespace Game.Core.Content;

/// <summary>Reads and validates an exploration map stored as a standalone JSON file.</summary>
public sealed class MapLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public MapLoadResult Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Failed(new ContentError(path ?? string.Empty, "A map file path is required."));
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failed(new ContentError(path, $"Could not read map file: {exception.Message}"));
        }

        MapDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<MapDocument>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            return Failed(new ContentError(path, $"Invalid JSON: {exception.Message}",
                exception.LineNumber + 1, exception.BytePositionInLine + 1));
        }

        if (document is null)
        {
            return Failed(new ContentError(path, "Map JSON must contain an object."));
        }

        var errors = new List<ContentError>();
        ValidateText(document.Id, "id", path, errors);
        ValidateText(document.Name, "name", path, errors);
        ValidatePositive(document.Width, "width", path, errors);
        ValidatePositive(document.Height, "height", path, errors);
        if (document.Start is null)
        {
            errors.Add(new ContentError(path, "Required field 'start' is missing."));
        }

        if (document.Tiles is null || document.Tiles.Count == 0)
        {
            errors.Add(new ContentError(path, "Map must define at least one tile in 'tiles'."));
        }

        if (errors.Count > 0)
        {
            return new MapLoadResult(false, null, errors.AsReadOnly());
        }

        var width = document.Width!.Value;
        var height = document.Height!.Value;
        var tiles = new List<WorldTile>();
        var coordinates = new HashSet<HexCoord>();
        for (var index = 0; index < document.Tiles!.Count; index++)
        {
            var tile = document.Tiles[index];
            if (tile is null || tile.Q is null || tile.R is null)
            {
                errors.Add(new ContentError(path, $"Tile {index} must define integer 'q' and 'r' coordinates."));
                continue;
            }

            var coordinate = new HexCoord(tile.Q.Value, tile.R.Value);
            if (!IsInside(coordinate, width, height))
            {
                errors.Add(new ContentError(path, $"Tile {index} coordinate ({coordinate.Q}, {coordinate.R}) is outside map dimensions."));
                continue;
            }

            if (!coordinates.Add(coordinate))
            {
                errors.Add(new ContentError(path, $"Map contains duplicate tile coordinate ({coordinate.Q}, {coordinate.R})."));
                continue;
            }

            if (tile.Height is < 0)
            {
                errors.Add(new ContentError(path, $"Tile at ({coordinate.Q}, {coordinate.R}) has negative height."));
                continue;
            }

            var terrainId = string.IsNullOrWhiteSpace(tile.Terrain) ? "grass" : tile.Terrain;
            tiles.Add(new WorldTile(coordinate, tile.Height ?? 0, tile.Passable ?? true, terrainId));
        }

        var start = new HexCoord(document.Start!.Q ?? int.MinValue, document.Start.R ?? int.MinValue);
        if (document.Start.Q is null || document.Start.R is null || !IsInside(start, width, height))
        {
            errors.Add(new ContentError(path, "Required 'start' must contain q/r coordinates inside the map."));
        }
        else
        {
            var startTile = tiles.FirstOrDefault(tile => tile.Coordinate == start);
            if (startTile is null || !startTile.IsPassable)
            {
                errors.Add(new ContentError(path, "The 'start' coordinate must refer to an existing passable tile."));
            }
        }

        var points = new List<WorldPointOfInterest>();
        var pointIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var point in document.PointsOfInterest ?? [])
        {
            if (point is null || string.IsNullOrWhiteSpace(point.Id) || string.IsNullOrWhiteSpace(point.Type) ||
                point.Q is null || point.R is null)
            {
                errors.Add(new ContentError(path, "Each point of interest must define id, type, q, and r."));
                continue;
            }

            var coordinate = new HexCoord(point.Q.Value, point.R.Value);
            if (!IsInside(coordinate, width, height) || !coordinates.Contains(coordinate))
            {
                errors.Add(new ContentError(path, $"Point of interest '{point.Id}' must refer to an existing map tile."));
                continue;
            }

            if (!pointIds.Add(point.Id))
            {
                errors.Add(new ContentError(path, $"Map contains duplicate point-of-interest id '{point.Id}'."));
                continue;
            }

            var pointName = string.IsNullOrWhiteSpace(point.Name) ? point.Id : point.Name;
            points.Add(new WorldPointOfInterest(point.Id, point.Type, pointName, coordinate));
        }

        if (errors.Count > 0)
        {
            return new MapLoadResult(false, null, errors.AsReadOnly());
        }

        var map = new WorldMap(document.Id!, document.Name!, width, height, start, tiles, points);
        return new MapLoadResult(true, map, Array.Empty<ContentError>());
    }

    private static bool IsInside(HexCoord coordinate, int width, int height) =>
        coordinate.Q >= 0 && coordinate.Q < width && coordinate.R >= 0 && coordinate.R < height;

    private static void ValidateText(string? value, string field, string path, ICollection<ContentError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ContentError(path, $"Required field '{field}' is missing or empty."));
        }
    }

    private static void ValidatePositive(int? value, string field, string path, ICollection<ContentError> errors)
    {
        if (value is null or <= 0)
        {
            errors.Add(new ContentError(path, $"Required field '{field}' must be a positive integer."));
        }
    }

    private static MapLoadResult Failed(ContentError error) => new(false, null, new[] { error });

    private sealed class MapDocument
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public int? Width { get; init; }
        public int? Height { get; init; }
        public CoordinateDocument? Start { get; init; }
        public List<TileDocument?>? Tiles { get; init; }
        public List<PointDocument?>? PointsOfInterest { get; init; }
    }

    private sealed class CoordinateDocument
    {
        public int? Q { get; init; }
        public int? R { get; init; }
    }

    private sealed class TileDocument
    {
        public int? Q { get; init; }
        public int? R { get; init; }
        public string? Terrain { get; init; }
        public int? Height { get; init; }
        public bool? Passable { get; init; }
    }

    private sealed class PointDocument
    {
        public string? Id { get; init; }
        public string? Type { get; init; }
        public string? Name { get; init; }
        public int? Q { get; init; }
        public int? R { get; init; }
    }
}
