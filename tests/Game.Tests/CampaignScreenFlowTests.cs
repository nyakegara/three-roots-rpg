using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Core.Campaigns;
using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Content;
using Game.Core.Economy;
using Game.Core.Navigation;
using Game.Core.World;
using Game.MonoGame;
using Game.MonoGame.Screens;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace Game.Tests;

public sealed class CampaignScreenFlowTests
{
    [Theory]
    [InlineData("help", "plants-pasture-battle")]
    [InlineData("refuse", "farmers-pasture-battle")]
    public void PastureChoice_ReachesBattleAndReturnsThroughScreen(string choice, string encounterId)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var units = UnitCatalog.Load(Path.Combine(root, "units", "units.json")).Catalog!;
        var definition = new CampaignLoader(units).Load(Path.Combine(root, "campaigns", "flora-01.json")).Definition!;
        var screen = new ExplorationScreen(
            (PixelCanvas)RuntimeHelpers.GetUninitializedObject(typeof(PixelCanvas)),
            (AssetCatalog)RuntimeHelpers.GetUninitializedObject(typeof(AssetCatalog)), definition, units);
        var campaign = Field<Campaign>(screen, "_campaign");
        var map = Field<WorldMap>(screen, "_map");
        var travel = Field<AdventureTravel>(screen, "_travel");
        var economy = new EconomyRules();
        var site = campaign.RecruitmentSites.Single(site => site.Id == "starting-clearing");
        Assert.True(economy.TryRecruit(campaign.Hero, site, "flora.thorn-guard", 18).Success);
        Assert.True(economy.TryRecruit(campaign.Hero, site, "flora.sprout", 8).Success);

        Press(screen, Keys.Enter); // The guide's introduction.
        Assert.Contains("guide-introduction", campaign.CompletedQuestIds);
        Approach(screen, travel, map.Navigation!.Points["pasture-request"]);
        Press(screen, Keys.E);
        Assert.NotNull(Field<QuestChoiceScreen?>(screen, "_questScreen"));
        Press(screen, choice == "help" ? Keys.D1 : Keys.D2);
        Assert.Equal(choice, campaign.SelectedChoiceIds["pasture-choice"]);
        Assert.Null(Field<BattleScreen?>(screen, "_battleScreen"));
        Assert.Equal(choice == "help", Field<EncounterScreen?>(screen, "_encounterScreen") is null);

        if (choice == "help")
        {
            Assert.DoesNotContain(encounterId, campaign.CompletedEncounterIds);
            Approach(screen, travel, map.Navigation.Points[encounterId]);
            Press(screen, Keys.E);
        }
        Assert.NotNull(Field<EncounterScreen?>(screen, "_encounterScreen"));
        Press(screen, Keys.Escape); // Leaving the confirmation must not count as a loss.
        Assert.Null(Field<EncounterScreen?>(screen, "_encounterScreen"));
        Assert.DoesNotContain(encounterId, campaign.CompletedEncounterIds);
        Press(screen, Keys.E);
        Assert.NotNull(Field<EncounterScreen?>(screen, "_encounterScreen"));
        Press(screen, Keys.Enter);
        Assert.Null(Field<EncounterScreen?>(screen, "_encounterScreen"));
        Assert.NotNull(Field<BattleScreen?>(screen, "_battleScreen"));

        var session = Field<CampaignBattle?>(campaign, "_activeEncounter");
        Assert.NotNull(session);
        SimulateBattle(session!.Battle);
        Assert.Equal(BattleSide.Player, session.Battle.Winner);
        Press(screen, Keys.Enter);
        Assert.Null(Field<BattleScreen?>(screen, "_battleScreen"));
        Assert.Contains(encounterId, campaign.CompletedEncounterIds);
        Assert.True(campaign.IsUnlocked("north-gate"));
        Assert.False(screen.HasOpenModal);
    }

    private static void Approach(ExplorationScreen screen, AdventureTravel travel, MapPosition landmark)
    {
        travel.SelectApproach(landmark);
        Assert.True(travel.SelectApproach(landmark));
        for (var step = 0; step < 1000 && travel.IsMoving; step++) screen.Update(Pointer(), default, 0.1f);
        Assert.False(travel.IsMoving);
        Assert.True(travel.CanInteractWith(landmark));
    }

    private static void Press(ExplorationScreen screen, Keys key)
    {
        screen.Update(Pointer(), default, 0.016f);
        screen.Update(Pointer(), new KeyboardState(key), 0.016f);
    }

    // Keep the pointer off the battlefield: these tests exercise keyboard/modal flow,
    // not texture alpha picking, and intentionally have no GPU-backed asset catalog.
    private static MouseState Pointer() => new(640, 650, 0, ButtonState.Released,
        ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void SimulateBattle(Battle battle)
    {
        var ai = new BattleAi();
        battle.StartRound();
        for (var step = 0; step < 10000 && !battle.IsFinished; step++)
        {
            var active = battle.ActiveUnit;
            if (active is null)
            {
                battle.StartRound();
                continue;
            }
            var action = ai.ChooseAction(battle, active.Id);
            var applied = action.Type switch
            {
                BattleActionType.Move => battle.Move(active.Id, action.Destination!.Value),
                BattleActionType.Attack => battle.Attack(active.Id, action.TargetId!),
                BattleActionType.SetAttackMode => battle.TrySetAttackMode(active.Id, action.Mode!.Value),
                _ => EndActivation()
            };
            Assert.True(applied, $"AI action {action.Type} was rejected for {active.Id}.");

            bool EndActivation()
            {
                battle.EndActivation();
                return true;
            }
        }
        Assert.True(battle.IsFinished);
    }
}
