using System.Numerics;

namespace Game.Core.Navigation;

/// <summary>Two-click route selection and time-based movement to an exact map position.</summary>
public sealed class AdventureTravel
{
    private readonly AdventurePathfinder _pathfinder;
    private readonly float _speed;
    private IReadOnlyList<MapPosition> _activePath = [];
    private int _next;
    public AdventureSurface Surface { get; }
    public MapPosition Position { get; private set; }
    public MapPosition? Destination { get; private set; }
    public AdventurePath Preview { get; private set; } = AdventurePath.Unreachable;
    public bool IsMoving => _next < _activePath.Count;
    public Vector2 Direction { get; private set; } = new(1, 1);
    public float AnimationSeconds { get; private set; }
    public IReadOnlyList<MapPosition> DisplayPath => IsMoving
        ? new[] { Position }.Concat(_activePath.Skip(_next)).ToArray() : Preview.Points;

    public AdventureTravel(AdventureSurface surface, MapPosition start, float speed = 75)
    {
        if (!surface.IsWalkable(start)) throw new ArgumentException("Start must be walkable.", nameof(start));
        if (!float.IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
        Position = start;
        Surface = surface;
        _speed = speed;
        _pathfinder = new(surface);
    }

    public bool Select(MapPosition destination)
    {
        _activePath = [];
        _next = 0;
        var path = _pathfinder.FindPath(Position, destination);
        if (Destination is { } selected && MapPosition.Distance(selected, destination) <= 3 &&
            Preview.IsReachable && path.IsReachable && path.Length > 0.01f)
        {
            _activePath = path.Points;
            _next = 1;
            Destination = null;
            Preview = AdventurePath.Unreachable;
            return true;
        }
        Destination = destination;
        Preview = path;
        return false;
    }

    public bool Advance(float elapsedSeconds)
    {
        if (!IsMoving || !float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0) return false;
        AnimationSeconds += elapsedSeconds;
        var remaining = _speed * elapsedSeconds;
        while (IsMoving && remaining > 0)
        {
            var next = _activePath[_next];
            var distance = MapPosition.Distance(Position, next);
            if (distance > 0.001f) Direction = Vector2.Normalize(next.Vector - Position.Vector);
            if (remaining < distance)
            {
                Position = MapPosition.From(Position.Vector + Direction * remaining);
                return false;
            }
            Position = next;
            remaining -= distance;
            _next++;
        }
        return !IsMoving;
    }
}
