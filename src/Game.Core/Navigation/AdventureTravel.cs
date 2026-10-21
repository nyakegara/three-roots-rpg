using System.Numerics;

namespace Game.Core.Navigation;

/// <summary>Two-click route selection and time-based movement to an exact map position.</summary>
public sealed class AdventureTravel
{
    public const float ApproachDistance = 20;
    public const float InteractionRadius = 21;
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

    public bool Select(MapPosition destination) => SelectPath(destination, _pathfinder.FindPath(Position, destination));

    /// <summary>Choose a reachable standing spot beside a landmark, rather than inside its model.</summary>
    public bool SelectApproach(MapPosition landmark, float distance = ApproachDistance)
    {
        if (!landmark.IsFinite) throw new ArgumentException("Landmark must be finite.", nameof(landmark));
        if (!float.IsFinite(distance) || distance <= 0) throw new ArgumentOutOfRangeException(nameof(distance));
        // A standing spot on this side of a closed story barrier must not grant interaction across it.
        if (_pathfinder.FindPath(Position, landmark).IsReachable)
        {
            var towardHero = Position.Vector - landmark.Vector;
            var angle = towardHero.LengthSquared() > 0.001f ? MathF.Atan2(towardHero.Y, towardHero.X) : MathF.PI / 2;
            int[] turns = [0, 1, -1, 2, -2, 3, -3, 4, -4, 5, -5, 6];
            foreach (var turn in turns)
            {
                var direction = angle + turn * MathF.PI / 6;
                var candidate = new MapPosition(landmark.X + MathF.Cos(direction) * distance,
                    landmark.Y + MathF.Sin(direction) * distance);
                if (!Surface.IsSegmentWalkable(candidate, landmark)) continue;
                var path = _pathfinder.FindPath(Position, candidate);
                if (path.IsReachable) return SelectPath(candidate, path);
            }
        }
        Stop();
        Destination = landmark;
        return false;
    }

    public void Stop()
    {
        _activePath = [];
        _next = 0;
        Destination = null;
        Preview = AdventurePath.Unreachable;
    }

    public bool CanInteractWith(MapPosition landmark, float radius = InteractionRadius) =>
        MapPosition.Distance(Position, landmark) <= radius && Surface.IsSegmentWalkable(Position, landmark);

    private bool SelectPath(MapPosition destination, AdventurePath path)
    {
        _activePath = [];
        _next = 0;
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
