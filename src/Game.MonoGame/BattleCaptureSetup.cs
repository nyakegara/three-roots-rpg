using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Geometry;

namespace Game.MonoGame;

/// <summary>Reaches real combat states through legal commands for reproducible UI captures.</summary>
public static class BattleCaptureSetup
{
    public static HexCoord? Advance(Battle battle, BattleCaptureStage stage)
    {
        ArgumentNullException.ThrowIfNull(battle);
        if (stage == BattleCaptureStage.Opening) return null;
        battle.StartRound();
        var ai = new BattleAi();
        for (var step = 0; step < 3000 && !battle.IsFinished; step++)
        {
            var active = battle.ActiveUnit ?? throw new InvalidOperationException("Missing active unit in capture.");
            if (stage == BattleCaptureStage.Target && active.Side == BattleSide.Player &&
                !battle.HasAttacked(active.Id))
            {
                var target = battle.Units.FirstOrDefault(unit => unit.IsAlive && unit.Side != active.Side &&
                    BattleRules.CanAttack(active, unit, battle.Map));
                if (target is not null) return target.Position;
            }
            if (stage == BattleCaptureStage.RangedTarget && active.Side == BattleSide.Player && active.IsRanged &&
                !battle.HasAttacked(active.Id))
            {
                var target = battle.Units.FirstOrDefault(unit => unit.IsAlive && unit.Side != active.Side &&
                    BattleRules.CanAttack(active, unit, battle.Map));
                if (target is not null) return target.Position;
            }
            if (stage == BattleCaptureStage.Healing && active is { Side: BattleSide.Player, Ability: UnitAbility.HealAlly })
            {
                var wounded = battle.Units.FirstOrDefault(unit => unit.IsAlive && unit.Id != active.Id &&
                    unit.Side == active.Side && unit.RemainingHitPoints < unit.MaximumHitPointsAtBattleStart);
                if (wounded is not null) return wounded.Position;
            }
            var action = ai.ChooseAction(battle, active.Id);
            var applied = action.Type switch
            {
                BattleActionType.Move => battle.Move(active.Id, action.Destination!.Value),
                BattleActionType.Attack => battle.Attack(active.Id, action.TargetId!),
                _ => false
            };
            if (!applied)
            {
                battle.EndActivation();
                if (battle.ActiveUnit is null) battle.StartRound();
            }
        }
        if (!battle.IsFinished || stage == BattleCaptureStage.Healing)
            throw new InvalidOperationException("Cannot reach requested combat capture with this seed.");
        return null;
    }
}
