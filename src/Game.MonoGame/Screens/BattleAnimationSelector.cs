using Game.Core.Combat;

namespace Game.MonoGame.Screens;

/// <summary>Chooses the secondary action clip when a unit uses a special ability.</summary>
public static class BattleAnimationSelector
{
    public static bool UsesSecondaryClip(BattleUnit actor, bool healing = false, bool conversion = false)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return healing && actor.Ability == UnitAbility.HealAlly ||
               conversion && actor.Ability == UnitAbility.ConvertOnHit ||
               actor.Ability == UnitAbility.RadiantModes && actor.AttackMode == AttackMode.HolyFire;
    }
}
