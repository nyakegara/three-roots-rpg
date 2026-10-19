using Game.Core.Characters;
using HeroModel = Game.Core.Characters.Hero;
using Xunit;

namespace Game.Tests.Hero;

public sealed class HeroTests
{
    [Fact]
    public void GainExperience_LevelUpAwardsSkillPointAndCarriesRemainder()
    {
        var hero = new HeroModel("hero", "Hero");

        hero.GainExperience(150);

        Assert.Equal(2, hero.Level);
        Assert.Equal(50, hero.Experience);
        Assert.Equal(1, hero.SkillPoints);
    }

    [Fact]
    public void GainExperience_CanGainSeveralLevelsFromOneReward()
    {
        var hero = new HeroModel("hero", "Hero");

        hero.GainExperience(300);

        Assert.Equal(3, hero.Level);
        Assert.Equal(0, hero.Experience);
        Assert.Equal(2, hero.SkillPoints);
    }

    [Fact]
    public void LearnSkill_ConsumesPointAndAddsPermanentBonusOnlyOnce()
    {
        var hero = new HeroModel("hero", "Hero", baseAttack: 2);
        var skill = new Skill("marksmanship", "Marksmanship", new StatBonus(Attack: 1));
        hero.GainExperience(100);

        Assert.True(hero.LearnSkill(skill));
        Assert.Equal(3, hero.Attack);
        Assert.Equal(0, hero.SkillPoints);
        Assert.False(hero.LearnSkill(skill));
        Assert.Equal(3, hero.Attack);
    }

    [Fact]
    public void EquipArtifact_AddsPermanentStats()
    {
        var hero = new HeroModel("hero", "Hero", baseDefense: 1, baseMaxHitPoints: 20);
        var artifact = new Artifact("root-charm", "Root Charm", new StatBonus(Defense: 1, MaxHitPoints: 5));

        Assert.True(hero.EquipArtifact(artifact));

        Assert.Equal(2, hero.Defense);
        Assert.Equal(25, hero.MaxHitPoints);
        Assert.False(hero.EquipArtifact(artifact));
    }

    [Fact]
    public void Reputation_IsBoundedToMinusThreeThroughThree()
    {
        var hero = new HeroModel("hero", "Hero", initialReputation: 2);

        hero.ChangeReputation(5);
        Assert.Equal(3, hero.Reputation);
        hero.ChangeReputation(-20);
        Assert.Equal(-3, hero.Reputation);
    }

    [Fact]
    public void LearnSkill_WithoutSkillPoint_IsRejected()
    {
        var hero = new HeroModel("hero", "Hero");

        Assert.False(hero.LearnSkill(new Skill("guard", "Guard", new StatBonus(Defense: 1))));
        Assert.Equal(1, hero.Defense);
    }
}
