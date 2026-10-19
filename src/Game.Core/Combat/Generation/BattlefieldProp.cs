using Game.Core.Geometry;

namespace Game.Core.Combat.Generation;

public enum BattlefieldPropKind { Rock, FallenLog, Shrub }

public sealed record BattlefieldProp(HexCoord Coordinate, BattlefieldPropKind Kind);
