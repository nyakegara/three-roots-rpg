using Game.Core.Geometry;

namespace Game.Core.Combat.AI;

public enum BattleActionType
{
    Move,
    Attack,
    EndActivation
}

public sealed record BattleAction(BattleActionType Type, HexCoord? Destination = null, string? TargetId = null)
{
    public static BattleAction MoveTo(HexCoord destination) => new(BattleActionType.Move, destination);
    public static BattleAction Attack(string targetId) => new(BattleActionType.Attack, TargetId: targetId);
    public static BattleAction End() => new(BattleActionType.EndActivation);
}
