using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Content;
using Game.MonoGame;
using Xunit;

namespace Game.Tests.Combat;

public sealed class BattleDemoTests
{
    [Fact]
    public void InitiativeCapture_KeepsNinthActiveStackInRealTenStackBattle()
    {
        var demo = BattleDemo.Create(Catalog(), 1729, fullRoster: true);
        BattleCaptureSetup.Advance(demo.Battle, BattleCaptureStage.Initiative);
        Assert.Equal(10, demo.Battle.Units.Count);
        Assert.Equal(2, demo.Battle.RemainingInitiative.Count);
        Assert.Same(demo.Battle.ActiveUnit, demo.Battle.RemainingInitiative[0]);
        Assert.Equal(1, demo.Battle.Round);
    }

    [Fact]
    public void InteractiveControllerCompletesDemoWithoutExternalRoundManagement()
    {
        var session = new BattleInteraction(BattleDemo.Create(Catalog(), 42).Battle);
        var ai = new BattleAi();
        for (var step = 0; step < 3000 && !session.Battle.IsFinished; step++)
        {
            if (!session.IsPlayerTurn)
            {
                Assert.NotNull(session.StepOpponent());
                continue;
            }
            var action = ai.ChooseAction(session.Battle, session.Battle.ActiveUnit!.Id);
            switch (action.Type)
            {
                case BattleActionType.Move:
                    Assert.False(session.SelectDestination(action.Destination!.Value));
                    Assert.True(session.SelectDestination(action.Destination.Value));
                    break;
                case BattleActionType.Attack:
                    Assert.True(session.TryAttack(action.TargetId!));
                    break;
                default:
                    Assert.True(session.EndPlayerTurn());
                    break;
            }
        }
        Assert.True(session.Battle.IsFinished);
    }

    [Theory]
    [InlineData(BattleCaptureStage.Healing)]
    [InlineData(BattleCaptureStage.Result)]
    public void ScreenshotFixturesReachRealStatesThroughBattleRules(BattleCaptureStage stage)
    {
        var demo = BattleDemo.Create(Catalog(), 1729);
        var cell = BattleCaptureSetup.Advance(demo.Battle, stage);
        if (stage == BattleCaptureStage.Healing)
        {
            Assert.Equal(UnitAbility.HealAlly, demo.Battle.ActiveUnit!.Ability);
            var target = demo.Battle.Units.Single(unit => unit.Position == cell && unit.IsAlive);
            Assert.True(target.RemainingHitPoints < target.MaximumHitPointsAtBattleStart);
            Assert.True(demo.Battle.TryHeal(demo.Battle.ActiveUnit.Id, target.Id));
        }
        else Assert.True(demo.Battle.IsFinished);
    }

    [Fact]
    public void DemoUsesCatalogStatsAndSafeGeneratedDeployment()
    {
        var catalog = Catalog();
        var demo = BattleDemo.Create(catalog, 1729);
        Assert.Equal(8, demo.Battle.Units.Count);
        Assert.Equal(5, demo.Battle.Units.Count(unit => unit.Side == BattleSide.Player));
        Assert.Equal(8, demo.Battle.Units.Select(unit => unit.Position).Distinct().Count());
        foreach (var unit in demo.Battle.Units)
        {
            var definition = catalog.Get(unit.UnitTypeId)!;
            Assert.Equal(definition.HitPoints, unit.HitPointsPerCreature);
            Assert.Equal(definition.Ability, unit.Ability);
            Assert.True(demo.Field.Map.GetTile(unit.Position)!.IsPassable);
        }
    }

    [Theory]
    [InlineData(1729)]
    [InlineData(42)]
    [InlineData(-1)]
    public void CompleteDemoSimulationTerminatesWithoutLosingTurn(int seed)
    {
        var demo = BattleDemo.Create(Catalog(), seed);
        var battle = demo.Battle;
        battle.StartRound();
        var ai = new BattleAi();
        for (var step = 0; step < 3000 && !battle.IsFinished; step++)
        {
            var active = Assert.IsType<BattleUnit>(battle.ActiveUnit);
            var action = ai.ChooseAction(battle, active.Id);
            switch (action.Type)
            {
                case BattleActionType.Move:
                    Assert.True(battle.Move(active.Id, action.Destination!.Value));
                    break;
                case BattleActionType.Attack:
                    Assert.True(battle.Attack(active.Id, action.TargetId!));
                    break;
                default:
                    battle.EndActivation();
                    if (battle.ActiveUnit is null) battle.StartRound();
                    break;
            }
        }
        Assert.True(battle.IsFinished, "AI battle failed to finish within 3000 actions.");
        Assert.NotNull(battle.Winner);
    }

    private static UnitCatalog Catalog() => UnitCatalog.Load(
        Path.Combine(AppContext.BaseDirectory, "content", "units", "units.json")).Catalog!;
}
