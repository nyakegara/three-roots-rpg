namespace Game.Core.Characters;

public sealed class Artifact
{
    public string Id { get; }
    public string Name { get; }
    public StatBonus Bonus { get; }

    public Artifact(string id, string name, StatBonus bonus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
        Bonus = bonus;
    }
}
