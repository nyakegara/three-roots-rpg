using System.Collections.ObjectModel;
using Game.Core.Characters;
using Game.Core.Geometry;
using Game.Core.Economy;
using Game.Core.Persistence;
using Game.Core.World;
using Game.Core.Content;
using Game.Core.Combat;
using Game.Core.Combat.Generation;

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
    private readonly ReadOnlyCollection<CampaignEncounter> _encounters;
    private readonly HashSet<string> _completedEncounterIds = new(StringComparer.Ordinal);
    private CampaignBattle? _activeEncounter;

    public Hero Hero { get; }
    public IReadOnlyList<Quest> Quests => _questView;
    public IReadOnlySet<string> CompletedQuestIds => _completedQuestIds.ToHashSet(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> SelectedChoiceIds =>
        new Dictionary<string, string>(_selectedChoiceIds, StringComparer.Ordinal);
    public IReadOnlyList<RecruitmentSite> RecruitmentSites => _recruitmentSiteView;
    public IReadOnlyList<CampaignEncounter> Encounters => _encounters;
    public IReadOnlySet<string> CompletedEncounterIds => _completedEncounterIds.ToHashSet(StringComparer.Ordinal);

    public bool TryGetRecruitmentSiteAt(WorldPointOfInterest point, HexCoord heroPosition,
        out RecruitmentSite? site)
    {
        ArgumentNullException.ThrowIfNull(point);
        site = null;
        if (!point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase) ||
            point.Coordinate != heroPosition || string.IsNullOrWhiteSpace(point.RecruitmentSiteId))
            return false;

        site = _recruitmentSites.GetValueOrDefault(point.RecruitmentSiteId);
        return site is not null;
    }

    public Campaign(Hero hero, IEnumerable<Quest> quests, IEnumerable<RecruitmentSite>? recruitmentSites = null,
        IEnumerable<CampaignEncounter>? encounters = null)
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
        var encounterArray = (encounters ?? []).ToArray();
        if (encounterArray.Any(encounter => encounter is null) ||
            encounterArray.Select(encounter => encounter.Id).Distinct(StringComparer.Ordinal).Count() != encounterArray.Length ||
            encounterArray.Select(encounter => encounter.PointOfInterestId).Distinct(StringComparer.Ordinal).Count() != encounterArray.Length)
            throw new ArgumentException("Campaign encounters require unique ids and world points.", nameof(encounters));
        _encounters = Array.AsReadOnly(encounterArray);
    }

    public EncounterStartResult TryStartEncounter(WorldPointOfInterest point, HexCoord heroPosition, UnitCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(catalog);
        if (_activeEncounter is not null)
            return new(false, "Another encounter is already active.");
        var encounter = _encounters.FirstOrDefault(candidate => candidate.PointOfInterestId == point.Id);
        if (point.Coordinate != heroPosition || encounter is null)
            return new(false, "The hero must reach the encounter's world point.");
        if (_completedEncounterIds.Contains(encounter.Id))
            return new(false, "This encounter is already completed.");
        if (Hero.Army.Stacks.Count == 0)
            return new(false, "Recruit an army before starting this encounter.");

        var startingArmy = Array.AsReadOnly(Hero.Army.Stacks.Select(stack => new EncounterTroop(stack.UnitTypeId, stack.Count)).ToArray());
        var allTroops = startingArmy.Concat(encounter.EnemyArmy);
        foreach (var troop in allTroops)
        {
            var definition = catalog.Get(troop.UnitTypeId);
            if (definition is null || definition.HitPoints <= 0 || definition.Damage <= 0 || definition.Speed <= 0 ||
                definition.Movement < 0 || definition.Range <= 0 || !Enum.IsDefined(definition.Ability) ||
                (long)troop.Count * definition.HitPoints > int.MaxValue)
                return new(false, $"Unit type '{troop.UnitTypeId}' has missing or invalid battle values.");
        }

        var field = new BattlefieldGenerator().Generate(encounter.Seed);
        var units = Deploy(startingArmy, BattleSide.Player).Concat(Deploy(encounter.EnemyArmy, BattleSide.Enemy));
        var battle = new Battle(field.Map, units, new Random(encounter.Seed));
        _activeEncounter = new CampaignBattle(this, encounter, field, battle, startingArmy);
        return new(true, "Encounter started.", _activeEncounter);

        IEnumerable<BattleUnit> Deploy(IReadOnlyList<EncounterTroop> troops, BattleSide side)
        {
            (int Column, int Row)[] positions = side == BattleSide.Player
                ? [(1, 1), (2, 2), (1, 3), (2, 5), (1, 6)]
                : [(10, 1), (9, 2), (10, 3), (9, 5), (10, 6)];
            for (var i = 0; i < troops.Count; i++)
            {
                var troop = troops[i];
                var definition = catalog.Get(troop.UnitTypeId)!;
                var (deploymentColumn, row) = positions[i];
                yield return new BattleUnit($"{side}:{i}", side, new HexCoord(deploymentColumn - row / 2, row), troop.Count,
                    definition.HitPoints, definition.Damage, definition.Speed, definition.Movement, definition.Range,
                    definition.IsRanged, ability: definition.Ability, unitTypeId: definition.Id);
            }
        }
    }

    public EncounterResolution ResolveEncounter(CampaignBattle session)
    {
        if (session is null || !ReferenceEquals(session.Owner, this) || !ReferenceEquals(_activeEncounter, session))
            return new(false, "This encounter session is stale or belongs to another campaign.");
        if (!session.Battle.IsFinished)
            return new(false, "Finish the battle before resolving this encounter.");
        if (!session.StartingArmy.SequenceEqual(Hero.Army.Stacks.Select(stack => new EncounterTroop(stack.UnitTypeId, stack.Count))))
            return new(false, "The campaign army changed during this encounter.");

        var victory = session.Battle.Winner == BattleSide.Player;
        var gold = victory ? session.Encounter.GoldReward : 0;
        var experience = victory ? session.Encounter.ExperienceReward : 0;
        if ((long)Hero.Gold + gold > int.MaxValue || !Hero.CanGainExperience(experience))
            return new(false, "The encounter rewards exceed the hero's resource limits.");
        var survivors = session.Battle.GetOutcome().SurvivingPlayerStacks;
        if (!Hero.Army.TryReplace(survivors.Select(stack => new TroopStack(stack.UnitTypeId, stack.Count))))
            return new(false, "The battle survivors cannot form a valid campaign army.");
        Hero.TryChangeGold(gold);
        Hero.GainExperience(experience);
        if (victory)
            _completedEncounterIds.Add(session.Encounter.Id);
        _activeEncounter = null;
        return new(true, victory ? "Encounter won." : "Encounter lost.", victory, gold, experience);
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
