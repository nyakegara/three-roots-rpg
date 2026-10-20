using System.Numerics;
using Game.Core.Geometry;
using Game.Core.World;
using Microsoft.Xna.Framework;
using WorldVector2 = System.Numerics.Vector2;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Game.MonoGame.Rendering;

/// <summary>Paints the campaign landscape without exposing the route graph's hex topology.</summary>
internal sealed class AdventureMapRenderer(PixelCanvas canvas, AssetCatalog assets)
{
    private const int MapViewportHeight = 596;
    private static readonly Color Grass = new(91, 117, 51);
    private static readonly Color GrassLight = new(112, 133, 61);
    private static readonly Color Water = new(54, 91, 105);
    private static readonly Color Ink = new(39, 40, 23);
    private bool _compactMap;

    public void Draw(WorldMap map, AdventureMapProjection projection, AdventureMapCamera camera,
        HexCoord heroPosition, IReadOnlyList<HexCoord> route, HexCoord? hoveredTile,
        IReadOnlyDictionary<string, string> idleActors, float idleSeconds, bool drawHero = true)
    {
        _compactMap = map.Id == "flora-01";
        var viewport = new Rectangle(0, 0, 1280, 600);
        using var clip = canvas.PushClip(viewport);
        var hasScenicGround = DrawGround(map, projection, camera);
        if (!hasScenicGround) DrawWater(map, projection, camera);
        // Flora's authored landscape already contains its main trails.
        // The graph stays invisible so it cannot draw straight lines across the art.
        if (map.Id != "flora-01") DrawRoadNetwork(map, projection, camera);
        DrawRoute(map, projection, camera, route);

        var objects = new List<(float Depth, Action Draw)>();
        foreach (var tile in map.Tiles)
        {
            var screen = camera.ToScreen(projection.Project(tile.Coordinate, tile.Height));
            if (screen.X < -120 || screen.X > viewport.Width + 120 || screen.Y < -160 || screen.Y > viewport.Height + 100)
                continue;
            if (tile.TerrainId == "rock") objects.Add((screen.Y, () => DrawRock(screen)));
        }

        foreach (var point in map.PointsOfInterest)
        {
            if (map.GetTile(point.Coordinate) is not { } tile) continue;
            if (_compactMap && point.Type.Equals("encounter", StringComparison.OrdinalIgnoreCase) &&
                !idleActors.ContainsKey(point.Id)) continue;
            var screen = camera.ToScreen(projection.Project(tile.Coordinate, tile.Height));
            if (idleActors.TryGetValue(point.Id, out var idleKey))
                objects.Add((screen.Y, () => DrawIdleActor(point, idleKey, screen, idleSeconds)));
            else objects.Add((screen.Y, () => DrawPoint(point, tile, screen)));
        }
        if (drawHero && map.GetTile(heroPosition) is { } heroTile)
        {
            var screen = camera.ToScreen(projection.Project(heroPosition, heroTile.Height));
            objects.Add((screen.Y, () => DrawHero(screen)));
        }

        foreach (var item in objects.OrderBy(item => item.Depth)) item.Draw();
        if (hoveredTile is { } hovered && map.GetTile(hovered) is { } hoveredWorldTile)
        {
            var marker = camera.ToScreen(projection.Project(hovered, hoveredWorldTile.Height));
            canvas.Frame((int)marker.X - 23, (int)marker.Y - 17, 46, 34, new Color(236, 211, 139) * 0.85f, 2);
        }
    }

    private bool DrawGround(WorldMap map, AdventureMapProjection projection, AdventureMapCamera camera)
    {
        var worldRect = new Rectangle((int)-camera.Position.X, (int)-camera.Position.Y,
            (int)projection.WorldSize.X, (int)projection.WorldSize.Y);
        if (map.Id == "flora-01" && DrawFloraIslandTiles(projection, camera))
        {
            canvas.Rect(0, 0, 1280, MapViewportHeight, new Color(32, 36, 25) * 0.19f);
            return true;
        }

        var groundKey = map.Id == "flora-01" ? AssetCatalog.FloraIslandGround : AssetCatalog.AdventureGround;
        if (assets.TryDraw(canvas, groundKey, worldRect))
        {
            canvas.Rect(0, 0, 1280, MapViewportHeight, new Color(32, 36, 25) * 0.19f);
            return true;
        }

        canvas.Rect(0, 0, 1280, MapViewportHeight, Grass);
        for (var i = 0; i < 5200; i++)
        {
            var hash = unchecked((uint)(i + 173) * 747796405u + 2891336453u);
            hash = ((hash >> ((int)(hash >> 28) + 4)) ^ hash) * 277803737u;
            hash = (hash >> 22) ^ hash;
            var worldX = hash % (uint)Math.Max(1, projection.WorldSize.X);
            hash = unchecked(hash * 1664525u + 1013904223u);
            var worldY = hash % (uint)Math.Max(1, projection.WorldSize.Y);
            var x = worldX - camera.Position.X;
            var y = worldY - camera.Position.Y;
            if (x < 0 || x >= 1280 || y < 0 || y >= MapViewportHeight) continue;
            var color = (hash & 3) == 0 ? GrassLight : new Color(76, 104, 45);
            canvas.Rect((int)x, (int)y, 2 + (int)(hash % 3), 1, color * 0.68f);
        }
        return false;
    }

    private bool DrawFloraIslandTiles(AdventureMapProjection projection, AdventureMapCamera camera)
    {
        var keys = new[]
        {
            AssetCatalog.FloraIslandNorthWest, AssetCatalog.FloraIslandNorthEast,
            AssetCatalog.FloraIslandSouthWest, AssetCatalog.FloraIslandSouthEast
        };
        if (keys.Any(key => !assets.HasAsset(key))) return false;

        var tileWidth = projection.WorldSize.X / 2f;
        var tileHeight = projection.WorldSize.Y / 2f;
        var destinations = new Rectangle[keys.Length];
        for (var index = 0; index < keys.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var left = (int)MathF.Round(column * tileWidth - camera.Position.X);
            var right = (int)MathF.Round((column + 1) * tileWidth - camera.Position.X);
            var top = (int)MathF.Round(row * tileHeight - camera.Position.Y);
            var bottom = (int)MathF.Round((row + 1) * tileHeight - camera.Position.Y);
            destinations[index] = new Rectangle(left, top, right - left, bottom - top);
            assets.TryDraw(canvas, keys[index], destinations[index]);
        }

        return true;
    }

    private void DrawWater(WorldMap map, AdventureMapProjection projection, AdventureMapCamera camera)
    {
        foreach (var tile in map.Tiles.Where(tile => tile.TerrainId == "water"))
        {
            var p = camera.ToScreen(projection.Project(tile.Coordinate, tile.Height));
            var seed = tile.Coordinate.Q * 7919 + tile.Coordinate.R * 1543;
            var x = (int)p.X + seed % 25 - 12;
            var y = (int)p.Y + seed % 13 - 6;
            FantasyArt.Oval(canvas, x, y + 10, 75, 43, new Color(34, 67, 78));
            FantasyArt.Oval(canvas, x - 4, y + 5, 62, 33, Water);
            canvas.Line(new XnaVector2(x - 15, y + 3), new XnaVector2(x + 12, y + 3), new Color(120, 158, 161), 2);
        }
    }

    private void DrawRoute(WorldMap map, AdventureMapProjection projection, AdventureMapCamera camera,
        IReadOnlyList<HexCoord> route)
    {
        if (route.Count < 2) return;
        for (var i = 1; i < route.Count; i++)
        {
            if (map.GetTile(route[i - 1]) is not { } a || map.GetTile(route[i]) is not { } b) continue;
            var from = camera.ToScreen(projection.Project(a.Coordinate, a.Height));
            var to = camera.ToScreen(projection.Project(b.Coordinate, b.Height));
            var steps = Math.Max(1, (int)(WorldVector2.Distance(from, to) / 18));
            for (var n = 1; n < steps; n += 2)
            {
                var p = WorldVector2.Lerp(from, to, n / (float)steps);
                FantasyArt.Oval(canvas, (int)p.X, (int)p.Y + 17, 5, 3, Ink * 0.6f);
                FantasyArt.Oval(canvas, (int)p.X, (int)p.Y + 15, 3, 2, new Color(231, 211, 154));
            }
        }
    }

    private void DrawRoadNetwork(WorldMap map, AdventureMapProjection projection, AdventureMapCamera camera)
    {
        var roads = map.Tiles.Where(tile => tile.IsRoad).ToDictionary(tile => tile.Coordinate);
        foreach (var tile in roads.Values)
        {
            var from = camera.ToScreen(projection.Project(tile.Coordinate, tile.Height));
            foreach (var neighbor in tile.Coordinate.GetNeighbors())
            {
                if (!roads.TryGetValue(neighbor, out var next) ||
                    tile.Coordinate.Q > neighbor.Q ||
                    (tile.Coordinate.Q == neighbor.Q && tile.Coordinate.R >= neighbor.R))
                    continue;

                var to = camera.ToScreen(projection.Project(next.Coordinate, next.Height));
                // Keep the trail quieter than the surrounding terrain. A wide,
                // uniform stroke reads as a rail line on the isometric hex grid.
                canvas.Line(new XnaVector2(from.X, from.Y + 3), new XnaVector2(to.X, to.Y + 3),
                    new Color(140, 111, 73) * 0.48f, 6);

                var distance = WorldVector2.Distance(from, to);
                var stones = Math.Max(1, (int)(distance / 13));
                var hash = unchecked((uint)(tile.Coordinate.Q * 7919 + tile.Coordinate.R * 1543));
                for (var index = 1; index < stones; index += 3)
                {
                    var t = index / (float)stones;
                    var point = WorldVector2.Lerp(from, to, t);
                    hash = unchecked(hash * 1664525u + 1013904223u);
                    var jitterX = (int)(hash % 9) - 4;
                    var jitterY = (int)((hash >> 4) % 7) - 3;
                    FantasyArt.Oval(canvas, (int)point.X + jitterX, (int)point.Y + jitterY + 3,
                        2, 1, new Color(206, 174, 120) * 0.38f);
                }
            }
        }
    }

    private void DrawRock(WorldVector2 p)
    {
        var rect = new Rectangle((int)p.X - 29, (int)p.Y - 25, 58, 49);
        if (!assets.TryDraw(canvas, AssetCatalog.Rock, rect)) FantasyArt.DrawRock(canvas, (int)p.X, (int)p.Y);
    }

    private void DrawIdleActor(WorldPointOfInterest point, string idleKey, WorldVector2 p, float seconds)
    {
        if (point.Id == "guide-dialogue") p += new WorldVector2(-56, 5);
        var phase = point.Id.Sum(character => (int)character) & 3;
        var frame = ((int)(seconds * 1000f / 260f) + phase) % 4;
        var size = _compactMap ? new Rectangle((int)p.X - 32, (int)p.Y - 66, 64, 80)
            : new Rectangle((int)p.X - 40, (int)p.Y - 82, 80, 100);
        FantasyArt.Oval(canvas, (int)p.X, (int)p.Y + 8, _compactMap ? 34 : 42, 9, Ink * 0.65f);
        if (!assets.TryDrawIdle(canvas, idleKey, size, frame))
        {
            var staticKey = idleKey[..^".idle".Length];
            assets.TryDrawCropped(canvas, staticKey, size);
        }
    }

    private void DrawPoint(WorldPointOfInterest point, WorldTile tile, WorldVector2 p)
    {
        if (point.Type.Equals("encounter", StringComparison.OrdinalIgnoreCase))
        {
            FantasyArt.Oval(canvas, (int)p.X, (int)p.Y + 6, 22, 9, Ink * 0.72f);
            canvas.Rect((int)p.X - 7, (int)p.Y - 11, 14, 14, new Color(112, 43, 32));
            canvas.Frame((int)p.X - 7, (int)p.Y - 11, 14, 14, new Color(225, 177, 105), 2);
            canvas.Line(new XnaVector2(p.X - 3, p.Y - 5), new XnaVector2(p.X + 3, p.Y - 5),
                new Color(247, 221, 164), 2);
        }
        else if (point.Type.Equals("gate", StringComparison.OrdinalIgnoreCase))
        {
            // The quest reveals the northern trail; no constructed gate belongs here.
            return;
        }
        else if (point.Type.Equals("quest", StringComparison.OrdinalIgnoreCase))
        {
            FantasyArt.Oval(canvas, (int)p.X, (int)p.Y + 6, 20, 8, Ink * 0.72f);
            canvas.Rect((int)p.X - 7, (int)p.Y - 12, 14, 15, new Color(211, 168, 73));
            canvas.Frame((int)p.X - 7, (int)p.Y - 12, 14, 15, new Color(81, 56, 32), 2);
            canvas.Rect((int)p.X - 1, (int)p.Y - 8, 3, 7, new Color(79, 55, 31));
            canvas.Rect((int)p.X - 1, (int)p.Y + 0, 3, 2, new Color(79, 55, 31));
        }
        else if (point.Type.Equals("treasure", StringComparison.OrdinalIgnoreCase))
        {
            var rect = new Rectangle((int)p.X - 25, (int)p.Y - 37, 50, 43);
            if (!assets.TryDraw(canvas, AssetCatalog.Treasure, rect)) ExplorationArt.DrawTreasureChest(canvas, (int)p.X, (int)p.Y + 8);
            FantasyArt.Oval(canvas, (int)p.X, (int)p.Y + 10, 33, 10, Ink * 0.75f);
            canvas.Rect((int)p.X - 11, (int)p.Y + 1, 22, 10, new Color(174, 117, 50));
            canvas.Rect((int)p.X - 7, (int)p.Y - 3, 14, 7, new Color(220, 168, 74));
            canvas.Rect((int)p.X - 1, (int)p.Y + 1, 2, 10, new Color(100, 65, 32));
        }
        else if (tile.TerrainId == "old-tree")
        {
            var rect = new Rectangle((int)p.X - 80, (int)p.Y - 132, 160, 150);
            if (!assets.TryDraw(canvas, AssetCatalog.AncientOak, rect)) ExplorationArt.DrawAncientOak(canvas, (int)p.X, (int)p.Y + 10);
            DrawQuestMarker((int)p.X + 55, (int)p.Y - 151);
        }
        else if (point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase))
        {
            var assetKey = RecruitmentPointArt.GetAssetKey(point.RecruitmentSiteId);
            if (assetKey is not null)
            {
                var compact = _compactMap;
                var spriteWidth = compact ? 54 : 96;
                var spriteHeight = compact ? 58 : 96;
                var bounds = new Rectangle((int)p.X - spriteWidth / 2,
                    (int)p.Y - spriteHeight + 8, spriteWidth, spriteHeight);
                FantasyArt.Oval(canvas, (int)p.X, (int)p.Y + 10, compact ? 42 : 72, compact ? 8 : 14, Ink * 0.78f);
                if (!assets.TryDraw(canvas, assetKey, bounds))
                    DrawRecruitmentFallback(p);
            }
            else DrawRecruitmentFallback(p);
        }
        else DrawRecruitmentFallback(p);
    }

    private void DrawRecruitmentFallback(WorldVector2 p) =>
        canvas.Rect((int)p.X - 6, (int)p.Y - 40, 12, 16, new Color(226, 195, 117));

    private void DrawHero(WorldVector2 p)
    {
        FantasyArt.Oval(canvas, (int)p.X, (int)p.Y + 19, 37, 12, Ink * 0.82f);
        for (var index = 0; index < 32; index++)
        {
            var first = MathF.Tau * index / 32;
            var next = MathF.Tau * (index + 1) / 32;
            canvas.Line(new XnaVector2(p.X + MathF.Cos(first) * 21, p.Y + 19 + MathF.Sin(first) * 8),
                new XnaVector2(p.X + MathF.Cos(next) * 21, p.Y + 19 + MathF.Sin(next) * 8),
                new Color(230, 192, 107) * 0.9f, 2);
        }
        if (!assets.TryDraw(canvas, AssetCatalog.FloraHero, _compactMap
                ? new Rectangle((int)p.X - 22, (int)p.Y - 48, 44, 56)
                : new Rectangle((int)p.X - 37, (int)p.Y - 78, 74, 96)))
            FantasyArt.DrawHero(canvas, (int)p.X, (int)p.Y);
    }

    private void DrawQuestMarker(int x, int y)
    {
        canvas.Rect(x - 2, y + 3, 22, 27, Ink * 0.74f);
        canvas.Rect(x - 4, y, 22, 27, new Color(126, 84, 38));
        canvas.Rect(x - 2, y + 2, 18, 22, new Color(237, 196, 99));
        canvas.Frame(x - 4, y, 22, 27, new Color(73, 52, 29), 2);
        canvas.Rect(x + 5, y + 4, 3, 12, new Color(76, 53, 29));
        canvas.Rect(x + 5, y + 19, 3, 3, new Color(76, 53, 29));
    }
}
