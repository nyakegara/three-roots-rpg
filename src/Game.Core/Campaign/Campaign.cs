using System.Collections.ObjectModel;
using Game.Core.Characters;
using Game.Core.Economy;
using Game.Core.Persistence;

namespace Game.Core.Campaigns;

/// <summary>Tracks completed quests and applies their selected rewards to the campaign hero.</summary>
public sealed class Campaign
{
    private readonly Dictionary<string, Quest> _quests;
    private readonly HashSet<string> _completedQuestIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _selectedChoiceIds = new(StringComparer.Ordinal);
    private readonly ReadOnlyCollection<Quest> _questView;
    private readonly Dictionary<string, RecruitmentSite> _recruitmentSites;
    private readonly ReadOnlyCollection<RecruitmentSite> _recruitmentSiteView;

    public Hero Hero { get; }
    public IReadOnlyList<Quest> Quests => _questView;
    public IReadOnlySet<string> CompletedQuestIds => _completedQuestIds.ToHashSet(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> SelectedChoiceIds =>
        new Dictionary<string, string>(_selectedChoiceIds, StringComparer.Ordinal);
    public IReadOnlyList<RecruitmentSite> RecruitmentSites => _recruitmentSiteView;

    public Campaign(Hero hero, IEnumerable<Quest> quests, IEnumerable<RecruitmentSite>? recruitmentSites = null)
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
        _recruitmentSites = new Dictionary<string, RecruitmentSite>(StringComparer.Ordinal);
        foreach (var site in recruitmentSites ?? [])
        {
            ArgumentNullException.ThrowIfNull(site);
            if (!_recruitmentSites.TryAdd(site.Id, site))
                throw new ArgumentException($"Campaign contains duplicate recruitment site id '{site.Id}'.", nameof(recruitmentSites));
        }
        _recruitmentSiteView = Array.AsReadOnly(_recruitmentSites.Values.ToArray());
    }

    public IReadOnlyList<SavedRecruitmentStock> GetRecruitmentStockSnapshot() => Array.AsReadOnly(
        _recruitmentSites.Values.SelectMany(site => site.Offers.Select(offer =>
            new SavedRecruitmentStock(site.Id, offer.UnitTypeId, offer.RemainingStock))).ToArray());

    public RecruitmentStockRestoreResult TryRestoreRecruitmentStock(IReadOnlyList<SavedRecruitmentStock> savedStocks)
    {
        if (savedStocks is null)
            return new RecruitmentStockRestoreResult(false, "Recruitment stock is required.");

        var expected = _recruitmentSites.Values.SelectMany(site => site.Offers.Select(offer =>
            (SiteId: site.Id, UnitId: offer.UnitTypeId, Offer: offer))).ToArray();
        if (savedStocks.Count != expected.Length)
            return new RecruitmentStockRestoreResult(false, "Saved recruitment stock is incomplete or contains unknown offers.");

        var values = new Dictionary<(string SiteId, string UnitId), int>();
        foreach (var saved in savedStocks)
        {
            if (saved is null || string.IsNullOrWhiteSpace(saved.SiteId) || string.IsNullOrWhiteSpace(saved.UnitId) ||
                !values.TryAdd((saved.SiteId, saved.UnitId), saved.RemainingStock))
                return new RecruitmentStockRestoreResult(false, "Saved recruitment stock contains duplicate or invalid keys.");
        }

        foreach (var (siteId, unitId, offer) in expected)
        {
            if (!values.TryGetValue((siteId, unitId), out var stock) || stock < 0 || stock > offer.StartingStock)
                return new RecruitmentStockRestoreResult(false, $"Saved stock for '{siteId}/{unitId}' is missing or outside its valid range.");
        }

        foreach (var (siteId, unitId, offer) in expected)
            offer.RestoreStock(values[(siteId, unitId)]);
        return new RecruitmentStockRestoreResult(true, "Recruitment stock restored.");
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

public sealed record RecruitmentStockRestoreResult(bool Success, string Message);
