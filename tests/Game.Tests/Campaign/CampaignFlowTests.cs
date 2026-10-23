using Game.Core.Campaigns;
using Game.Core.Combat;
using Game.Core.Content;
using Game.Core.Persistence;
using Game.MonoGame;
using Xunit;

namespace Game.Tests.Campaigns;

public sealed class CampaignFlowTests
{
    [Fact]
    public void CampaignStartsWithStoryArmyAndCapturePartyUsesNormalGoldAndStock()
    {
        var (definition, _) = Load();
        var campaign = definition.CreateGameState();
        Assert.Equal(new[] { ("flora.thorn-guard", 12), ("flora.sprout", 3) },
            campaign.Hero.Army.Stacks.Select(stack => (stack.UnitTypeId, stack.Count)));
        CampaignCaptureSetup.RecruitParty(campaign);
        Assert.Equal(72, campaign.Hero.Gold);
        Assert.Equal(2, campaign.Hero.Army.Stacks.Count);
        Assert.Equal(10, campaign.RecruitmentSites.Single(site => site.Id == "starting-clearing")
            .GetOffer("flora.thorn-guard")!.RemainingStock);
        Assert.Equal(2, definition.CreateGameState().Hero.Army.Stacks.Count);
    }

    [Fact]
    public void HireFightReturnAppliesRealSurvivorsAndRewardOnce()
    {
        var (definition, units) = Load();
        var campaign = definition.CreateGameState();
        CampaignCaptureSetup.RecruitParty(campaign);
        var encounter = campaign.Encounters.Single(candidate => candidate.Id == "optional-patrol");
        var point = definition.Map.PointsOfInterest.Single(poi => poi.Id == encounter.PointOfInterestId);
        var session = campaign.TryStartEncounter(point, point.Coordinate, units).Session!;
        var starting = campaign.Hero.Army.Stacks.ToDictionary(stack => stack.UnitTypeId, stack => stack.Count);
        BattleCaptureSetup.Advance(session.Battle, BattleCaptureStage.Result);
        Assert.Equal(BattleSide.Player, session.Battle.Winner);
        var expected = session.Battle.GetOutcome().SurvivingPlayerStacks.ToDictionary(stack => stack.UnitTypeId, stack => stack.Count);
        Assert.True(campaign.ResolveEncounter(session).Success);
        Assert.Equal(117, campaign.Hero.Gold);
        Assert.Equal(65, campaign.Hero.Experience);
        Assert.Contains(encounter.Id, campaign.CompletedEncounterIds);
        foreach (var stack in campaign.Hero.Army.Stacks)
        {
            Assert.Equal(expected[stack.UnitTypeId], stack.Count);
            Assert.True(stack.Count <= starting[stack.UnitTypeId]);
        }
        Assert.Equal(expected.Count, campaign.Hero.Army.Stacks.Count);
        Assert.False(campaign.ResolveEncounter(session).Success);
        Assert.False(campaign.TryStartEncounter(point, point.Coordinate, units).Success);
        Assert.Equal(117, campaign.Hero.Gold);
    }

    [Theory]
    [InlineData("artifact-ash-quest", "artifact-ash", 75)]
    [InlineData("artifact-spore-quest", "artifact-spore", 90)]
    public void ArtifactRewardIsGrantedByWinningEncounterNotByChoosingIt(string questId, string encounterId,
        int expectedGold)
    {
        var (definition, _) = Load();
        var campaign = definition.CreateGameState();
        var startingGold = campaign.Hero.Gold;

        var choice = campaign.ApplyChoice(questId, "claim");

        Assert.True(choice.Success, choice.Message);
        Assert.Equal(startingGold, campaign.Hero.Gold);
        Assert.Equal(expectedGold, campaign.Encounters.Single(encounter => encounter.Id == encounterId).GoldReward);
        if (encounterId == "artifact-spore")
            Assert.True(campaign.TryRestoreProgress(new CampaignProgressSnapshot(
                ["plants-pasture-battle", "burned-forest", "mountain-outpost", "mushroom-outpost"], false)));
        Assert.True(campaign.CanAccessEncounter(encounterId));
    }

    [Fact]
    public void CampaignSaveRestoresChoicePositionArmyAndRecruitmentStock()
    {
        var (definition, units) = Load();
        var campaign = definition.CreateGameState();
        var start = definition.Map.StartPosition!.Value;
        Assert.True(campaign.ApplyChoice("guide-introduction", "begin").Success);
        var site = campaign.RecruitmentSites.Single(candidate => candidate.Id == "starting-clearing");
        var economy = new Game.Core.Economy.EconomyRules();
        Assert.True(economy.TryRecruit(campaign.Hero, site, "flora.thorn-guard", 1).Success);
        campaign.Hero.ChangeReputation(1);
        campaign.Hero.GainExperience(115);
        var save = campaign.CreateSave(definition.Id, definition.Map.Id, start);

        var restored = definition.CreateGameState();

        Assert.True(restored.TryRestoreSave(save, definition.Id, definition.PlayerFactionId,
            definition.Map, units, out var error), error);
        Assert.Equal(campaign.Hero.Gold, restored.Hero.Gold);
        Assert.Equal(campaign.Hero.Reputation, restored.Hero.Reputation);
        Assert.Equal(campaign.Hero.Level, restored.Hero.Level);
        Assert.Equal(campaign.Hero.Experience, restored.Hero.Experience);
        Assert.Equal(campaign.Hero.Army.Stacks.Select(stack => (stack.UnitTypeId, stack.Count)),
            restored.Hero.Army.Stacks.Select(stack => (stack.UnitTypeId, stack.Count)));
        Assert.Equal(campaign.SelectedChoiceIds, restored.SelectedChoiceIds);
        Assert.Equal(17, restored.RecruitmentSites.Single(candidate => candidate.Id == "starting-clearing")
            .GetOffer("flora.thorn-guard")!.RemainingStock);
    }

    [Fact]
    public void CampaignSaveRejectsCompletedEncountersWithUnmetPrerequisites()
    {
        var (definition, units) = Load();
        var save = definition.CreateGameState()
            .CreateSave(definition.Id, definition.Map.Id, definition.Map.StartPosition!.Value) with
        {
            CompletedEncounterIds = ["final-landing"]
        };

        var restored = definition.CreateGameState();

        Assert.False(restored.TryRestoreSave(save, definition.Id, definition.PlayerFactionId,
            definition.Map, units, out var error));
        Assert.Contains("progression", error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(restored.CompletedEncounterIds);
    }

    [Fact]
    public void CampaignSaveRejectsSimultaneousVictoryAndDefeat()
    {
        var (definition, units) = Load();
        var save = definition.CreateGameState()
            .CreateSave(definition.Id, definition.Map.Id, definition.Map.StartPosition!.Value) with
        {
            Army = [],
            CompletedQuestIds = ["pasture-choice"],
            SelectedChoices = [new SavedQuestChoice("pasture-choice", "refuse")],
            CompletedEncounterIds = ["farmers-pasture-battle", "burned-forest", "mountain-outpost",
                "mushroom-outpost", "final-landing"],
            IsDefeated = true
        };
        var restored = definition.CreateGameState();

        Assert.False(restored.TryRestoreSave(save, definition.Id, definition.PlayerFactionId,
            definition.Map, units, out var error));
        Assert.Contains("victory", error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(restored.CompletedEncounterIds);
        Assert.False(restored.IsDefeated);
    }

    [Fact]
    public void SporeArtifactUsesTheMushroomRouteUnlock()
    {
        var (definition, _) = Load();
        var artifact = definition.Map.PointsOfInterest.Single(point => point.Id == "artifact-spore");

        Assert.Equal("fungus-route", definition.Map.GetTile(artifact.Coordinate)!.RequiredUnlock);
        var campaign = definition.CreateGameState();
        Assert.True(campaign.ApplyChoice("artifact-spore-quest", "claim").Success);
        Assert.False(campaign.CanAccessEncounter("artifact-spore"));
    }

    private static (CampaignDefinition, UnitCatalog) Load()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        return (new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!, units);
    }
}
