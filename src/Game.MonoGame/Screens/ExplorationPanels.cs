using Game.Core.Campaigns;
using Game.Core.Geometry;
using Game.Core.World;
using Game.Core.Navigation;
using Microsoft.Xna.Framework;

namespace Game.MonoGame.Screens;

internal enum ExplorationPanelKind { None, Map, Journal }

/// <summary>Read-only campaign reference panels; movement and quest choices stay in ExplorationScreen.</summary>
internal sealed class ExplorationPanels(PixelCanvas canvas, AssetCatalog assets)
{
    public static readonly Rectangle CloseButton = new(1086, 67, 66, 38);
    private static readonly Rectangle MapArea = new(110, 123, 1060, 462);
    private static readonly Color Wood = new(42, 31, 23);
    private static readonly Color Bronze = new(148, 111, 57);
    private static readonly Color Parchment = new(216, 201, 159);
    private static readonly Color Ink = new(55, 42, 29);
    private static readonly Color Gold = new(230, 199, 122);
    private readonly PixelFont _font = new(canvas);

    public void DrawMap(WorldMap map, AdventureMapProjection projection, Campaign? campaign,
        HexCoord heroPosition, MapPosition? adventurePosition = null, bool showNavigation = false)
    {
        Frame("КАРТА ОСТРОВА");
        canvas.Rect(MapArea.X - 5, MapArea.Y - 5, MapArea.Width + 10, MapArea.Height + 10, Wood);
        canvas.Frame(MapArea.X - 5, MapArea.Y - 5, MapArea.Width + 10, MapArea.Height + 10, Bronze, 3);
        if (map.Id == "flora-01")
        {
            var keys = new[] { AssetCatalog.FloraIslandNorthWest, AssetCatalog.FloraIslandNorthEast,
                AssetCatalog.FloraIslandSouthWest, AssetCatalog.FloraIslandSouthEast };
            for (var index = 0; index < keys.Length; index++)
            {
                var column = index % 2;
                var row = index / 2;
                assets.TryDraw(canvas, keys[index], new Rectangle(MapArea.X + column * MapArea.Width / 2,
                    MapArea.Y + row * MapArea.Height / 2, MapArea.Width / 2, MapArea.Height / 2));
            }
        }
        else canvas.Rect(MapArea.X, MapArea.Y, MapArea.Width, MapArea.Height, new Color(92, 110, 63));

        if (showNavigation && map.Navigation is { } navigation)
        {
            foreach (var region in navigation.WalkableAreas) Outline(region.Points, new Color(255, 217, 112));
            foreach (var hole in navigation.BlockedAreas) Outline(hole.Points, new Color(241, 89, 69));
            foreach (var gate in navigation.Gates.Where(gate => campaign?.IsUnlocked(gate.UnlockId) != true))
                Outline(gate.Points, new Color(164, 160, 231));
            void Outline(IReadOnlyList<MapPosition> points, Color color) => canvas.Outline(points.Select(p =>
            {
                var pixel = MapPixel(projection, projection.FromArt(p));
                return new Vector2(pixel.X, pixel.Y);
            }).ToArray(), color, 2);
        }
        var completedQuests = campaign?.CompletedQuestIds;
        var activeQuestPoints = campaign?.Quests.Where(quest => !completedQuests!.Contains(quest.Id))
            .Select(quest => quest.PointOfInterestId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var point in map.PointsOfInterest)
        {
            if (point.Type.Equals("quest", StringComparison.OrdinalIgnoreCase) &&
                activeQuestPoints is not null && !activeQuestPoints.Contains(point.Id)) continue;
            var encounter = campaign?.Encounters.FirstOrDefault(item => item.PointOfInterestId == point.Id);
            var recruitment = point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase);
            if (encounter is not null && !campaign!.CanAccessEncounter(encounter.Id) ||
                recruitment && campaign is not null &&
                (point.RecruitmentSiteId is null || !campaign.CanAccessRecruitmentSite(point.RecruitmentSiteId)))
                continue;
            if (encounter is null && !recruitment && point.Type != "quest") continue;
            var p = MapPixel(projection, projection.ProjectPoint(point));
            var color = encounter is not null ? new Color(189, 67, 53) :
                recruitment ? new Color(103, 157, 87) : Gold;
            canvas.Rect(p.X - 4, p.Y - 4, 9, 9, Wood);
            canvas.Frame(p.X - 3, p.Y - 3, 7, 7, color, 2);
        }

        var hero = adventurePosition is { } position ? MapPixel(projection, projection.FromArt(position))
            : MapPoint(projection, heroPosition, map.GetTile(heroPosition)!.Height);
        canvas.Rect(hero.X - 7, hero.Y - 7, 15, 15, Wood);
        canvas.Frame(hero.X - 5, hero.Y - 5, 11, 11, Gold, 3);
        _font.Draw("ЛИАРА", hero.X + 11, hero.Y - 8, Gold, 1);
        _font.Draw("ЗОЛОТОЙ: ГЕРОЙ И ЗАДАНИЯ   ЗЕЛЁНЫЙ: НАЙМ   КРАСНЫЙ: БОЙ",
            137, 600, Ink, 2);
    }

    public void DrawJournal(Campaign? campaign)
    {
        Frame("ЗАДАНИЯ И ПОХОД");
        if (campaign is null)
        {
            _font.Draw("КАМПАНИЯ НЕ ЗАГРУЖЕНА", 154, 156, Ink, 2);
            return;
        }

        canvas.Rect(634, 129, 2, 450, Bronze);
        _font.Draw("РЕШЕНИЯ", 144, 137, Ink, 2);
        _font.Draw("СРАЖЕНИЯ", 666, 137, Ink, 2);
        for (var index = 0; index < Math.Min(5, campaign.Quests.Count); index++)
        {
            var quest = campaign.Quests[index];
            var y = 181 + index * 77;
            _font.Draw(Short(quest.Title, 36), 144, y, Ink, 2);
            if (campaign.SelectedChoiceIds.TryGetValue(quest.Id, out var choiceId))
            {
                var choice = quest.GetChoice(choiceId);
                _font.Draw(Short(choice?.Description ?? "ВЫБОР СДЕЛАН", 38), 145, y + 24,
                    new Color(77, 92, 49), 2);
            }
            else _font.Draw("ОЖИДАЕТ РЕШЕНИЯ", 145, y + 24, new Color(124, 91, 53), 2);
        }

        for (var index = 0; index < Math.Min(10, campaign.Encounters.Count); index++)
        {
            var encounter = campaign.Encounters[index];
            var y = 182 + index * 40;
            var finished = campaign.CompletedEncounterIds.Contains(encounter.Id);
            var available = campaign.CanAccessEncounter(encounter.Id);
            _font.Draw(Short(encounter.Name, 29), 666, y, Ink, 2);
            _font.Draw(finished ? "ПОБЕДА" : available ? "ОТКРЫТО" : "ЗАКРЫТО", 1060, y + 3,
                finished ? new Color(76, 113, 55) : available ? new Color(158, 74, 49) :
                new Color(132, 116, 88), 2);
            canvas.Rect(664, y + 26, 472, 1, Bronze * 0.44f);
        }

        _font.Draw($"ПОБЕД: {campaign.CompletedEncounterIds.Count}/{campaign.Encounters.Count}   " +
            $"ЗОЛОТО: {campaign.Hero.Gold}   РЕПУТАЦИЯ: {campaign.Hero.Reputation}", 145, 599, Ink, 2);
    }

    private void Frame(string title)
    {
        canvas.Rect(0, 0, 1280, 720, Color.Black * 0.58f);
        canvas.Rect(75, 49, 1130, 622, Wood);
        canvas.Frame(75, 49, 1130, 622, Bronze, 4);
        canvas.Rect(86, 60, 1108, 600, Parchment);
        canvas.Frame(86, 60, 1108, 600, new Color(88, 64, 36), 2);
        _font.Draw(title, 123, 79, Ink, 3);
        canvas.Rect(122, 112, 1037, 3, Bronze);
        canvas.Rect(CloseButton.X, CloseButton.Y, CloseButton.Width, CloseButton.Height, Wood);
        canvas.Frame(CloseButton.X, CloseButton.Y, CloseButton.Width, CloseButton.Height, Bronze, 2);
        _font.Draw("X", CloseButton.X + 26, CloseButton.Y + 11, Gold, 2);
        _font.Draw("ESC: ЗАКРЫТЬ   M: КАРТА   J: ЗАДАНИЯ", 690, 631, Ink, 2);
    }

    private Point MapPoint(AdventureMapProjection projection, HexCoord coordinate, int height)
    {
        var projected = projection.Project(coordinate, height);
        return MapPixel(projection, projected);
    }

    private Point MapPixel(AdventureMapProjection projection, System.Numerics.Vector2 projected)
    {
        return new Point(MapArea.X + Math.Clamp((int)(projected.X / projection.WorldSize.X * MapArea.Width), 0, MapArea.Width),
            MapArea.Y + Math.Clamp((int)(projected.Y / projection.WorldSize.Y * MapArea.Height), 0, MapArea.Height));
    }

    private static string Short(string text, int max) => text.Length <= max ? text : text[..(max - 2)] + "..";
}
