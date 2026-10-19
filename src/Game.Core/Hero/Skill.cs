namespace Game.Core.Characters;

public sealed class Skill
{
    public string Id { get; }
    public string Name { get; }
    public StatBonus Bonus { get; }

    public Skill(string id, string name, StatBonus bonus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
        Bonus = bonus;
    }
}
