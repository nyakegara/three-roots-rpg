using Game.Core.Combat.Pathfinding;
using Game.Core.Geometry;

namespace Game.Core.Combat;

/// <summary>Owns the state and validates all commands in a tactical battle.</summary>
public sealed class Battle
{
    private readonly Dictionary<string, BattleUnit> _units;
    private readonly AStarPathfinder _pathfinder = new();
    private readonly Dictionary<string, bool> _hasMoved = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _hasAttacked = new(StringComparer.Ordinal);
    private readonly HashSet<string> _usedHeroAbilities = new(StringComparer.Ordinal);
    private List<BattleUnit> _initiative = [];
    private int _activationIndex = -1;

    public BattleMap Map { get; }
    public IReadOnlyCollection<BattleUnit> Units => _units.Values;
    public int Round { get; private set; }
    public BattleUnit? ActiveUnit => _activationIndex >= 0 && _activationIndex < _initiative.Count
        ? _initiative[_activationIndex]
        : null;
    public bool IsFinished => _units.Values.Where(unit => unit.IsAlive).Select(unit => unit.Side).Distinct().Count() <= 1;
    public BattleSide? Winner => IsFinished ? _units.Values.FirstOrDefault(unit => unit.IsAlive)?.Side : null;

    public Battle(BattleMap map, IEnumerable<BattleUnit> units)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(units);
        Map = map;
        var list = units.ToList();
        if (list.Any(unit => unit is null))
        {
            throw new ArgumentException("A battle cannot contain null units.", nameof(units));
        }

        _units = new Dictionary<string, BattleUnit>(StringComparer.Ordinal);
        foreach (var unit in list)
        {
            if (!_units.TryAdd(unit.Id, unit))
            {
                throw new ArgumentException($"Duplicate unit id '{unit.Id}'.", nameof(units));
            }

            var tile = map.GetTile(unit.Position);
            if (tile is null || !tile.IsPassable)
            {
                throw new ArgumentException($"Unit '{unit.Id}' must start on a passable battle cell.", nameof(units));
            }

            unit.MoveTo(tile);
        }

        if (_units.Values.Select(unit => unit.Side).Distinct().Count() != 2)
        {
            throw new ArgumentException("A battle requires units from both sides.", nameof(units));
        }

        if (_units.Values.Where(unit => unit.IsAlive).Select(unit => unit.Position).Distinct().Count() !=
            _units.Values.Count(unit => unit.IsAlive))
        {
            throw new ArgumentException("Living units cannot occupy the same cell.", nameof(units));
        }
    }

    public void StartRound()
    {
        if (ActiveUnit is not null)
        {
            return;
        }

        if (IsFinished)
        {
            _initiative = [];
            _activationIndex = -1;
            return;
        }

        Round++;
        _initiative = _units.Values.Where(unit => unit.IsAlive)
            .OrderByDescending(unit => unit.Speed)
            .ThenBy(unit => unit.Id, StringComparer.Ordinal)
            .ToList();
        _activationIndex = _initiative.Count > 0 ? 0 : -1;
        _hasMoved.Clear();
        _hasAttacked.Clear();
        _usedHeroAbilities.Clear();
    }

    public bool Move(string unitId, HexCoord destination)
    {
        if (IsFinished)
        {
            return false;
        }

        var unit = GetActiveUnit(unitId);
        if (unit is null || _hasMoved.GetValueOrDefault(unitId) || destination == unit.Position)
        {
            return false;
        }

        var path = _pathfinder.FindPath(Map, unit.Position, destination, GetOccupiedCells(unit));
        if (!path.IsReachable || path.Cost > unit.Movement)
        {
            return false;
        }

        unit.MoveTo(Map.GetTile(destination)!);
        _hasMoved[unitId] = true;
        return true;
    }

    public bool Attack(string unitId, string targetId)
    {
        if (IsFinished)
        {
            return false;
        }

        var attacker = GetActiveUnit(unitId);
        if (attacker is null || _hasAttacked.GetValueOrDefault(unitId) ||
            !_units.TryGetValue(targetId, out var target) || !BattleRules.CanAttack(attacker, target, Map))
        {
            return false;
        }

        target.ApplyDamage(attacker.TotalDamage);
        _hasAttacked[unitId] = true;
        return true;
    }

    public bool TryUseHeroAbility(string unitId)
    {
        if (Round == 0 || IsFinished || !_units.TryGetValue(unitId, out var hero) || !hero.IsHero || !hero.IsAlive ||
            _usedHeroAbilities.Contains(unitId))
        {
            return false;
        }

        _usedHeroAbilities.Add(unitId);
        return true;
    }

    public void EndActivation()
    {
        if (ActiveUnit is not null)
        {
            _activationIndex++;
        }
    }

    public IReadOnlyList<BattleUnit> GetEnemies(BattleSide side) => _units.Values
        .Where(unit => unit.IsAlive && unit.Side != side)
        .OrderBy(unit => unit.Id, StringComparer.Ordinal)
        .ToArray();

    public bool HasMoved(string unitId) => _hasMoved.GetValueOrDefault(unitId);
    public bool HasAttacked(string unitId) => _hasAttacked.GetValueOrDefault(unitId);

    private BattleUnit? GetActiveUnit(string unitId) =>
        ActiveUnit is { IsAlive: true } active && string.Equals(active.Id, unitId, StringComparison.Ordinal)
            ? active
            : null;

    private IReadOnlySet<HexCoord> GetOccupiedCells(BattleUnit except) => _units.Values
        .Where(unit => unit.IsAlive && !ReferenceEquals(unit, except))
        .Select(unit => unit.Position)
        .ToHashSet();
}
