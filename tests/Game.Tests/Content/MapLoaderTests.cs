using Game.Core.Content;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Content;

public sealed class MapLoaderTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    [Fact]
    public void Load_ValidMap_ReturnsMetadataTilesStartAndPointsOfInterest()
    {
        var path = WriteMap("""
        {
          "id": "root-grove",
          "name": "Роща первого корня",
          "width": 3,
          "height": 2,
          "start": { "q": 0, "r": 0 },
          "tiles": [
            { "q": 0, "r": 0, "terrain": "meadow", "height": 0, "passable": true },
            { "q": 1, "r": 0, "terrain": "hill", "height": 2, "passable": true },
            { "q": 2, "r": 0, "terrain": "ruins", "height": 0, "passable": false }
          ],
          "pointsOfInterest": [
            { "id": "old-tree", "type": "quest", "name": "Старое дерево", "q": 1, "r": 0 }
          ]
        }
        """);

        var result = new MapLoader().Load(path);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Map);
        Assert.Equal("root-grove", result.Map.Id);
        Assert.Equal("Роща первого корня", result.Map.Name);
        Assert.Equal(3, result.Map.Width);
        Assert.Equal(2, result.Map.Height);
        Assert.Equal<HexCoord?>(new HexCoord(0, 0), result.Map.StartPosition);
        Assert.Equal("hill", result.Map.GetTile(new HexCoord(1, 0))!.TerrainId);
        Assert.Equal(2, result.Map.GetTile(new HexCoord(1, 0))!.Height);
        Assert.Single(result.Map.PointsOfInterest);
        Assert.Equal("old-tree", result.Map.PointsOfInterest.Single().Id);
    }

    [Fact]
    public void Load_MalformedJson_ReturnsFileAndLineError()
    {
        var path = WriteMap("""
        {
          "id": "broken",
          "tiles": [
        """);

        var result = new MapLoader().Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Map);
        var error = Assert.Single(result.Errors);
        Assert.Equal(path, error.FilePath);
        Assert.NotNull(error.LineNumber);
        Assert.Contains("JSON", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MissingRequiredStart_ReturnsValidationError()
    {
        var path = WriteMap("""
        {
          "id": "no-start",
          "name": "Без старта",
          "width": 1,
          "height": 1,
          "tiles": [{ "q": 0, "r": 0, "terrain": "meadow" }]
        }
        """);

        var result = new MapLoader().Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Map);
        Assert.Contains(result.Errors, error => error.Message.Contains("start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_NegativeTileHeight_ReturnsValidationError()
    {
        var path = WriteMap(ValidMap.Replace("\"height\":0,\"passable\":true",
            "\"height\":-1,\"passable\":true", StringComparison.Ordinal));

        var result = new MapLoader().Load(path);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains("height", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_DuplicateTileCoordinates_ReturnsValidationError()
    {
        var path = WriteMap("""
        { "id":"dup", "name":"dup", "width":1, "height":1, "start":{"q":0,"r":0},
          "tiles":[{"q":0,"r":0},{"q":0,"r":0}] }
        """);

        var result = new MapLoader().Load(path);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_UnknownJsonField_ReturnsFileAndLineError()
    {
        var path = WriteMap(ValidMap.Replace("\"terrain\":\"meadow\"",
            "\"terrian\":\"meadow\"", StringComparison.Ordinal));

        var result = new MapLoader().Load(path);

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Equal(path, error.FilePath);
        Assert.NotNull(error.LineNumber);
    }

    [Fact]
    public void Load_StartOutsideOrImpassable_ReturnsValidationError()
    {
        var path = WriteMap("""
        { "id":"blocked", "name":"blocked", "width":1, "height":1, "start":{"q":0,"r":0},
          "tiles":[{"q":0,"r":0,"passable":false}] }
        """);

        var result = new MapLoader().Load(path);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Message.Contains("start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_MissingFile_ReturnsDiagnosticInsteadOfThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        var result = new MapLoader().Load(path);

        Assert.False(result.Success);
        Assert.Null(result.Map);
        Assert.Contains(result.Errors, error => error.FilePath == path);
    }

    [Fact]
    public void Load_TutorialContentFile_ReturnsPlayableMap()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", "maps", "tutorial.json");

        var result = new MapLoader().Load(path);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.NotNull(result.Map);
        Assert.True(result.Map.Width > 0);
        Assert.True(result.Map.Height > 0);
        Assert.NotNull(result.Map.GetTile(result.Map.StartPosition!.Value));
    }

    private string WriteMap(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"three-roots-map-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        _temporaryFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            File.Delete(path);
        }
    }

    private const string ValidMap = """
        { "id":"valid", "name":"Valid", "width":2, "height":1, "start":{"q":0,"r":0},
          "tiles":[{"q":0,"r":0,"terrain":"meadow","height":0,"passable":true},
                   {"q":1,"r":0,"terrain":"hill","height":1,"passable":true}] }
        """;
}
