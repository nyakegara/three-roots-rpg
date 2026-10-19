using System.Collections.ObjectModel;

namespace Game.Core.Campaigns;

public sealed class Quest
{
    private readonly Dictionary<string, QuestChoice> _choices;
    private readonly ReadOnlyCollection<QuestChoice> _choiceView;

    public string Id { get; }
    public string Title { get; }
    public IReadOnlyList<QuestChoice> Choices => _choiceView;

    public Quest(string id, string title, IEnumerable<QuestChoice> choices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(choices);
        Id = id;
        Title = title;
        _choices = new Dictionary<string, QuestChoice>(StringComparer.Ordinal);
        foreach (var choice in choices)
        {
            ArgumentNullException.ThrowIfNull(choice);
            if (!_choices.TryAdd(choice.Id, choice))
            {
                throw new ArgumentException($"Quest contains duplicate choice id '{choice.Id}'.", nameof(choices));
            }
        }

        if (_choices.Count == 0)
        {
            throw new ArgumentException("A quest must offer at least one choice.", nameof(choices));
        }

        _choiceView = Array.AsReadOnly(_choices.Values.ToArray());
    }

    public QuestChoice? GetChoice(string id) => string.IsNullOrWhiteSpace(id) ? null : _choices.GetValueOrDefault(id);
}
