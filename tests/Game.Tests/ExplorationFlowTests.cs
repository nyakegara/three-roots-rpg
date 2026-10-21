using System.Reflection;
using System.Runtime.CompilerServices;
using System.Numerics;
using Game.Core.Campaigns;
using Game.Core.Content;
using Game.Core.Geometry;
using Game.Core.Navigation;
using Game.MonoGame;
using Game.MonoGame.Screens;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace Game.Tests;

public sealed class ExplorationFlowTests
{
    [Fact]
    public void ClickingPastureQuestMarker_SelectsNpcApproachAndOpensDialogueOnArrival()
    {
        var screen = Create();
        CloseIntroduction(screen);
        var travel = Field<AdventureTravel>(screen, "_travel");
        var map = Field<Game.Core.World.WorldMap>(screen, "_map");
        var point = map.PointsOfInterest.Single(point => point.Id == "pasture-request");
        var projection = Field<AdventureMapProjection>(screen, "_projection");
        var camera = Field<AdventureMapCamera>(screen, "_camera");
        var landmark = map.Navigation!.Points[point.Id];
        travel.SelectApproach(landmark, 80);
        Assert.True(travel.SelectApproach(landmark, 80));
        for (var step = 0; step < 1000 && travel.IsMoving; step++) screen.Update(Pointer(), default, 0.1f);
        var marker = camera.ToScreen(projection.FromArt(landmark) + new Vector2(0, -78));
        var clicked = new MouseState((int)marker.X, (int)marker.Y, 0, ButtonState.Pressed,
            ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        screen.Update(clicked, default, 0.016f);
        Assert.Equal(point.Id, Field<Game.Core.World.WorldPointOfInterest>(screen, "_travelPoint")?.Id);
        screen.Update(Pointer(clicked.X, clicked.Y), default, 0.016f);
        screen.Update(clicked, default, 0.016f);
        for (var step = 0; step < 1000 && travel.IsMoving; step++) screen.Update(Pointer(), default, 0.1f);
        Assert.NotNull(Field<QuestChoiceScreen>(screen, "_questScreen"));
    }

    [Fact]
    public void NewCampaign_OpensGuideDialogueAndAcceptsItsChoice()
    {
        var screen = Create();
        Assert.True(screen.HasOpenModal);
        Assert.NotNull(Field<QuestChoiceScreen>(screen, "_questScreen"));
        screen.Update(Pointer(), new KeyboardState(Keys.Enter), 0.016f);
        Assert.Contains("guide-introduction", Field<Campaign>(screen, "_campaign").CompletedQuestIds);
        Assert.False(screen.HasOpenModal);
    }

    [Fact]
    public void PastureDialogue_OpensWithEAfterRealApproach()
    {
        var screen = Create();
        CloseIntroduction(screen);
        var map = Field<Game.Core.World.WorldMap>(screen, "_map");
        var travel = Field<AdventureTravel>(screen, "_travel");
        var landmark = map.Navigation!.Points["pasture-request"];
        travel.SelectApproach(landmark);
        Assert.True(travel.SelectApproach(landmark));
        for (var step = 0; step < 1000 && travel.IsMoving; step++) screen.Update(Pointer(), default, 0.1f);
        Assert.False(travel.IsMoving);
        Assert.True(travel.CanInteractWith(landmark));
        screen.Update(Pointer(), new KeyboardState(Keys.E), 0.016f);
        Assert.NotNull(Field<QuestChoiceScreen>(screen, "_questScreen"));
        screen.Update(Pointer(), new KeyboardState(Keys.D1), 0.016f);
        Assert.Equal("help", Field<Campaign>(screen, "_campaign").SelectedChoiceIds["pasture-choice"]);
        Assert.False(screen.HasOpenModal); // The plants are a separate destination, not an immediate battle.
        Assert.DoesNotContain("plants-pasture-battle", Field<Campaign>(screen, "_campaign").CompletedEncounterIds);
    }

    [Fact]
    public void Camera_FollowsTheHeroDuringTravelAndIgnoresMouseEdgePanning()
    {
        var screen = Create();
        CloseIntroduction(screen);
        var travel = Field<AdventureTravel>(screen, "_travel");
        var projection = Field<AdventureMapProjection>(screen, "_projection");
        var camera = Field<AdventureMapCamera>(screen, "_camera");
        var nav = Field<Game.Core.World.WorldMap>(screen, "_map").Navigation!;
        travel.Select(nav.Points["pasture-request"]);
        Assert.True(travel.Select(nav.Points["pasture-request"]));
        for (var step = 0; step < 10; step++) screen.Update(Pointer(), default, 0.1f);
        var expected = new AdventureMapCamera(new(1280, 596), projection.WorldSize, Vector2.Zero);
        expected.CenterOn(projection.FromArt(travel.Position));
        Assert.Equal(expected.Position, camera.Position);
        travel.Stop();
        screen.Update(Pointer(0, 290), default, 0.1f);
        Assert.Equal(expected.Position, camera.Position);
    }

    private static void CloseIntroduction(ExplorationScreen screen)
    {
        screen.Update(Pointer(), new KeyboardState(Keys.Enter), 0.016f);
        screen.Update(Pointer(), default, 0.016f);
    }

    private static MouseState Pointer(int x = 640, int y = 298) => new(x, y, 0,
        ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private static T Field<T>(ExplorationScreen screen, string name) =>
        (T)typeof(ExplorationScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(screen)!;

    private static ExplorationScreen Create()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var definition = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!;
        // Input and state transitions do not draw: avoid creating a GPU/window in these tests.
        return new((PixelCanvas)RuntimeHelpers.GetUninitializedObject(typeof(PixelCanvas)),
            (AssetCatalog)RuntimeHelpers.GetUninitializedObject(typeof(AssetCatalog)), definition, units);
    }
}
