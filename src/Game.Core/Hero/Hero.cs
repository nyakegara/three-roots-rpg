using System.Collections.ObjectModel;

namespace Game.Core.Characters;

/// <summary>Campaign hero state: resources, army, level, skills, and collected artifacts.</summary>
public sealed class Hero
{
    private readonly List<Skill> _skills = [];
    private readonly List<Artifact> _artifacts = [];
    private readonly ReadOnlyCollection<Skill> _skillView;
    private readonly ReadOnlyCollection<Artifact> _artifactView;
    private int _experience;
    private readonly int _baseAttack;
    private readonly int _baseDefense;
    private readonly int _baseMaxHitPoints;

    public string Id { get; }
    public string Name { get; }
    public int Gold { get; private set; }
    public int Reputation { get; private set; }
    public int Level { get; private set; } = 1;
    public int Experience => _experience;
    public int ExperienceToNextLevel => checked(Level * 100);
    public int SkillPoints { get; private set; }
    public int Attack => CalculateStat(_baseAttack, bonus => bonus.Attack);
    public int Defense => CalculateStat(_baseDefense, bonus => bonus.Defense);
    public int MaxHitPoints => CalculateStat(_baseMaxHitPoints, bonus => bonus.MaxHitPoints);
    public Army Army { get; } = new();
    public IReadOnlyList<Skill> Skills => _skillView;
    public IReadOnlyList<Artifact> Artifacts => _artifactView;

    public Hero(string id, string name, int startingGold = 0, int initialReputation = 0,
        int baseAttack = 1, int baseDefense = 1, int baseMaxHitPoints = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(startingGold);
        ArgumentOutOfRangeException.ThrowIfNegative(baseAttack);
        ArgumentOutOfRangeException.ThrowIfNegative(baseDefense);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseMaxHitPoints);
        Id = id;
        Name = name;
        Gold = startingGold;
        Reputation = Math.Clamp(initialReputation, -3, 3);
        _baseAttack = baseAttack;
        _baseDefense = baseDefense;
        _baseMaxHitPoints = baseMaxHitPoints;
        _skillView = _skills.AsReadOnly();
        _artifactView = _artifacts.AsReadOnly();
    }

    public void GainExperience(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        _experience = checked(_experience + amount);
        while (_experience >= ExperienceToNextLevel)
        {
            _experience -= ExperienceToNextLevel;
            Level = checked(Level + 1);
            SkillPoints = checked(SkillPoints + 1);
        }
    }

    internal bool CanGainExperience(int amount)
    {
        if (amount < 0 || (long)_experience + amount > int.MaxValue)
            return false;
        var experience = (long)_experience + amount;
        var level = (long)Level;
        var skillPoints = (long)SkillPoints;
        while (experience >= level * 100)
        {
            if (level * 100 > int.MaxValue || level >= int.MaxValue || skillPoints >= int.MaxValue)
                return false;
            experience -= level * 100;
            level++;
            skillPoints++;
        }
        return level * 100 <= int.MaxValue;
    }

    public bool LearnSkill(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (SkillPoints == 0 || _skills.Any(known => known.Id == skill.Id))
        {
            return false;
        }

        _skills.Add(skill);
        SkillPoints--;
        return true;
    }

    public bool EquipArtifact(Artifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (_artifacts.Any(known => known.Id == artifact.Id))
        {
            return false;
        }

        _artifacts.Add(artifact);
        return true;
    }

    public void ChangeReputation(int amount)
    {
        Reputation = (int)Math.Clamp((long)Reputation + amount, -3L, 3L);
    }

    internal bool TryChangeGold(int amount)
    {
        var newGold = (long)Gold + amount;
        if (newGold < 0 || newGold > int.MaxValue)
        {
            return false;
        }

        Gold = (int)newGold;
        return true;
    }

    private int CalculateStat(int baseValue, Func<StatBonus, int> selectBonus)
    {
        var total = (long)baseValue + _skills.Sum(skill => (long)selectBonus(skill.Bonus)) +
                    _artifacts.Sum(artifact => (long)selectBonus(artifact.Bonus));
        return checked((int)total);
    }
}
