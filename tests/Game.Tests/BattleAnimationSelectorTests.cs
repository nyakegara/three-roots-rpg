using Game.Core.Combat;
using Game.Core.Geometry;
using Game.MonoGame.Screens;
using Xunit;

namespace Game.Tests;

public sealed class BattleAnimationSelectorTests
{
    [Theory]
    [InlineData("unit.human.inquisitor", true, "unit.human.inquisitor.cast")]
    [InlineData("unit.human.inquisitor", false, "unit.human.inquisitor.attack")]
    [InlineData("unit.human.longbow", false, "unit.human.longbow.attack")]
    public void HumanPose_MatchesSelectedAttackMode(string artKey, bool secondary, string expected)
    {
        Assert.Equal(expected, BattleAnimationSelector.HumanPoseKey(artKey, secondary));
    }

    [Fact]
    public void HealAbility_UsesSecondaryClipOnlyForHealing()
    {
        var healer = CreateUnit(UnitAbility.HealAlly);

        Assert.True(BattleAnimationSelector.UsesSecondaryClip(healer, healing: true));
        Assert.False(BattleAnimationSelector.UsesSecondaryClip(healer));
    }

    [Fact]
    public void ConversionAbility_UsesSecondaryClipOnlyWhenConversionTriggers()
    {
        var warden = CreateUnit(UnitAbility.ConvertOnHit);

        Assert.True(BattleAnimationSelector.UsesSecondaryClip(warden, conversion: true));
        Assert.False(BattleAnimationSelector.UsesSecondaryClip(warden));
    }

    [Fact]
    public void RadiantModes_UsesSecondaryClipForHolyFire()
    {
        var inquisitor = CreateUnit(UnitAbility.RadiantModes);
        var battle = new Battle(new BattleMap(
                [new BattleTile(new HexCoord(0, 0)), new BattleTile(new HexCoord(2, 1))]),
            [inquisitor, CreateUnit(UnitAbility.None, "enemy", BattleSide.Enemy,
                new HexCoord(2, 1), speed: 1)],
            new Random(1));
        battle.StartRound();
        Assert.True(battle.TrySetAttackMode(inquisitor.Id, AttackMode.HolyFire));

        Assert.True(BattleAnimationSelector.UsesSecondaryClip(inquisitor));
    }

    private static BattleUnit CreateUnit(UnitAbility ability, string id = "actor",
        BattleSide side = BattleSide.Player, HexCoord? position = null, int speed = 2) =>
        new(id, side, position ?? new HexCoord(0, 0), count: 1, hitPointsPerCreature: 10,
            damagePerCreature: 2, speed: speed, movement: 2, range: 7,
            isRanged: false, ability: ability);
}
