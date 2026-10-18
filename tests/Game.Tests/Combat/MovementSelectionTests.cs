using Game.Core.Combat;
using Game.Core.Geometry;
using Xunit;

namespace Game.Tests.Combat;

public sealed class MovementSelectionTests
{
    private static readonly HexCoord Start = new(0, 0);
    private static readonly HexCoord Target = new(3, 0);

    [Fact]
    public void NewSelection_HasNoPendingDestination()
    {
        var movement = new MovementSelection(CreateMap(), Start);

        Assert.Equal(Start, movement.Position);
        Assert.Null(movement.Destination);
        Assert.Empty(movement.Path.Coordinates);
    }

    [Fact]
    public void Select_FirstClick_PreviewsPathWithoutMoving()
    {
        var movement = new MovementSelection(CreateMap(), Start);

        var moved = movement.Select(Target);

        Assert.False(moved);
        Assert.Equal(Start, movement.Position);
        Assert.Equal(Target, movement.Destination);
        Assert.True(movement.Path.IsReachable);
        Assert.Equal(3, movement.Path.Cost);
        Assert.Equal(Start, movement.Path.Coordinates[0]);
        Assert.Equal(Target, movement.Path.Coordinates[^1]);
    }

    [Fact]
    public void Select_SameDestinationTwice_MovesAndClearsPreview()
    {
        var movement = new MovementSelection(CreateMap(), Start);
        movement.Select(Target);

        var moved = movement.Select(Target);

        Assert.True(moved);
        Assert.Equal(Target, movement.Position);
        Assert.Null(movement.Destination);
        Assert.Empty(movement.Path.Coordinates);
    }

    [Fact]
    public void Select_DifferentSecondDestination_OnlyUpdatesPreview()
    {
        var movement = new MovementSelection(CreateMap(), Start);
        movement.Select(Target);
        var other = new HexCoord(2, 1);

        var moved = movement.Select(other);

        Assert.False(moved);
        Assert.Equal(Start, movement.Position);
        Assert.Equal(other, movement.Destination);
        Assert.Equal(other, movement.Path.Coordinates[^1]);
        Assert.True(movement.Select(other));
        Assert.Equal(other, movement.Position);
    }

    [Fact]
    public void Select_PreviouslyReplacedDestination_RequiresNewConfirmation()
    {
        var movement = new MovementSelection(CreateMap(), Start);
        movement.Select(Target);
        movement.Select(new HexCoord(2, 1));

        Assert.False(movement.Select(Target));
        Assert.Equal(Start, movement.Position);
        Assert.True(movement.Select(Target));
        Assert.Equal(Target, movement.Position);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Select_UnreachableDestinationTwice_DoesNotMove(bool occupied)
    {
        var map = CreateMap(impassable: occupied ? null : Target);
        var blocked = occupied ? new HashSet<HexCoord> { Target } : new HashSet<HexCoord>();
        var movement = new MovementSelection(map, Start, blocked);

        Assert.False(movement.Select(Target));
        Assert.False(movement.Select(Target));
        Assert.Equal(Start, movement.Position);
        Assert.False(movement.Path.IsReachable);
    }

    [Fact]
    public void Select_CurrentPosition_DoesNotCountAsMovement()
    {
        var movement = new MovementSelection(CreateMap(), Start);

        Assert.False(movement.Select(Start));
        Assert.False(movement.Select(Start));
        Assert.Equal(Start, movement.Position);
    }

    [Fact]
    public void Select_AfterMoving_NewDestinationNeedsTwoClicks()
    {
        var movement = new MovementSelection(CreateMap(), Start);
        movement.Select(Target);
        movement.Select(Target);
        var next = new HexCoord(4, 0);

        Assert.False(movement.Select(next));
        Assert.Equal(Target, movement.Position);
        Assert.True(movement.Select(next));
        Assert.Equal(next, movement.Position);
    }

    [Fact]
    public void Select_PathChangesBeforeConfirmation_RequiresPreviewAgain()
    {
        var occupied = new HashSet<HexCoord>();
        var movement = new MovementSelection(CreateMap(), Start, occupied);
        movement.Select(Target);
        occupied.Add(new HexCoord(1, 0));

        Assert.False(movement.Select(Target));
        Assert.Equal(Start, movement.Position);
        Assert.True(movement.Path.IsReachable);
        Assert.DoesNotContain(new HexCoord(1, 0), movement.Path.Coordinates);
        Assert.True(movement.Select(Target));
        Assert.Equal(Target, movement.Position);
    }

    [Fact]
    public void Select_MissingDestination_DoesNotMove()
    {
        var movement = new MovementSelection(CreateMap(), Start);
        var missing = new HexCoord(100, 0);

        Assert.False(movement.Select(missing));
        Assert.False(movement.Select(missing));
        Assert.Equal(Start, movement.Position);
        Assert.False(movement.Path.IsReachable);
    }

    [Fact]
    public void Constructor_NullMap_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new MovementSelection(null!, Start));
    }

    [Fact]
    public void Constructor_InvalidStart_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new MovementSelection(CreateMap(), new HexCoord(-10, 0)));
    }

    private static BattleMap CreateMap(HexCoord? impassable = null)
    {
        var tiles = new List<BattleTile>();
        for (var r = 0; r < 2; r++)
        for (var q = 0; q < 5; q++)
        {
            var coordinate = new HexCoord(q, r);
            tiles.Add(new BattleTile(coordinate, isPassable: coordinate != impassable));
        }
        return new BattleMap(tiles);
    }
}
