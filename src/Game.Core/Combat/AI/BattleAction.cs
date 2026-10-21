using Game.Core.Geometry;

namespace Game.Core.Combat.AI;

public enum BattleActionType
{
    Move,
    Attack,
    SetAttackMode,
    EndActivation
}

public sealed record BattleAction(BattleActionType Type, HexCoord? Destination = null, string? TargetId = null,
    AttackMode? Mode = null)
{
    public static BattleAction MoveTo(HexCoord destination) => new(BattleActionType.Move, destination);
    public static BattleAction Attack(string targetId) => new(BattleActionType.Attack, TargetId: targetId);
    public static BattleAction SetMode(AttackMode mode) => new(BattleActionType.SetAttackMode, Mode: mode);
    public static BattleAction End() => new(BattleActionType.EndActivation);
}
