using Game.Core.Combat;
using Game.Core.Content;
using Game.MonoGame;
using Xunit;

namespace Game.Tests.Campaigns;

public sealed class CampaignFlowTests
{
    [Fact]
    public void CapturePartyUsesNormalGoldAndStockAndRegularCampaignStartsEmpty()
    {
        var (definition, _) = Load();
        var campaign = definition.CreateGameState();
        Assert.Empty(campaign.Hero.Army.Stacks);
        CampaignCaptureSetup.RecruitParty(campaign);
        Assert.Equal(34, campaign.Hero.Gold);
        Assert.Equal(4, campaign.Hero.Army.Stacks.Count);
        Assert.Equal(7, campaign.RecruitmentSites.Single(site => site.Id == "root-greenhouse")
            .GetOffer("flora.thorn-guard")!.RemainingStock);
        Assert.Empty(definition.CreateGameState().Hero.Army.Stacks);
    }

    [Fact]
    public void HireFightReturnAppliesRealSurvivorsAndRewardOnce()
    {
        var (definition, units) = Load();
        var campaign = definition.CreateGameState();
        CampaignCaptureSetup.RecruitParty(campaign);
        var encounter = Assert.Single(campaign.Encounters);
        var point = definition.Map.PointsOfInterest.Single(poi => poi.Id == encounter.PointOfInterestId);
        var session = campaign.TryStartEncounter(point, point.Coordinate, units).Session!;
        var starting = campaign.Hero.Army.Stacks.ToDictionary(stack => stack.UnitTypeId, stack => stack.Count);
        BattleCaptureSetup.Advance(session.Battle, BattleCaptureStage.Result);
        Assert.Equal(BattleSide.Player, session.Battle.Winner);
        var expected = session.Battle.GetOutcome().SurvivingPlayerStacks.ToDictionary(stack => stack.UnitTypeId, stack => stack.Count);
        Assert.True(campaign.ResolveEncounter(session).Success);
        Assert.Equal(94, campaign.Hero.Gold);
        Assert.Equal(60, campaign.Hero.Experience);
        Assert.Contains(encounter.Id, campaign.CompletedEncounterIds);
        foreach (var stack in campaign.Hero.Army.Stacks)
        {
            Assert.Equal(expected[stack.UnitTypeId], stack.Count);
            Assert.True(stack.Count <= starting[stack.UnitTypeId]);
        }
        Assert.Equal(expected.Count, campaign.Hero.Army.Stacks.Count);
        Assert.False(campaign.ResolveEncounter(session).Success);
        Assert.False(campaign.TryStartEncounter(point, point.Coordinate, units).Success);
        Assert.Equal(94, campaign.Hero.Gold);
    }

    private static (CampaignDefinition, UnitCatalog) Load()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        return (new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!, units);
    }
}
