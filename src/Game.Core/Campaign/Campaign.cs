using System.Collections.ObjectModel;
using Game.Core.Characters;

namespace Game.Core.Campaigns;

/// <summary>Tracks completed quests and applies their selected rewards to the campaign hero.</summary>
public sealed class Campaign
{
    private readonly Dictionary<string, Quest> _quests;
    private readonly HashSet<string> _completedQuestIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _selectedChoiceIds = new(StringComparer.Ordinal);
    private readonly ReadOnlyCollection<Quest> _questView;

    public Hero Hero { get; }
    public IReadOnlyList<Quest> Quests => _questView;
    public IReadOnlySet<string> CompletedQuestIds => _completedQuestIds.ToHashSet(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> SelectedChoiceIds =>
        new Dictionary<string, string>(_selectedChoiceIds, StringComparer.Ordinal);

    public Campaign(Hero hero, IEnumerable<Quest> quests)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(quests);
        Hero = hero;
        _quests = new Dictionary<string, Quest>(StringComparer.Ordinal);
        foreach (var quest in quests)
        {
            ArgumentNullException.ThrowIfNull(quest);
            if (!_quests.TryAdd(quest.Id, quest))
            {
                throw new ArgumentException($"Campaign contains duplicate quest id '{quest.Id}'.", nameof(quests));
            }
        }

        _questView = Array.AsReadOnly(_quests.Values.ToArray());
    }

    public ChoiceResult ApplyChoice(string questId, string choiceId)
    {
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(choiceId))
        {
            return new ChoiceResult(false, "A quest id and a choice id are required.");
        }

        if (!_quests.TryGetValue(questId, out var quest))
        {
            return new ChoiceResult(false, $"Quest '{questId}' was not found.");
        }

        if (_completedQuestIds.Contains(questId))
        {
            return new ChoiceResult(false, $"Quest '{questId}' already has a selected choice.");
        }

        var choice = quest.GetChoice(choiceId);
        if (choice is null)
        {
            return new ChoiceResult(false, $"Choice '{choiceId}' was not found in quest '{questId}'.");
        }

        if (!Hero.TryChangeGold(choice.GoldChange))
        {
            return new ChoiceResult(false, "The hero does not have enough gold for this choice.");
        }

        Hero.ChangeReputation(choice.ReputationChange);
        _completedQuestIds.Add(questId);
        _selectedChoiceIds.Add(questId, choiceId);
        return new ChoiceResult(true, "Choice applied.", choice);
    }
}
