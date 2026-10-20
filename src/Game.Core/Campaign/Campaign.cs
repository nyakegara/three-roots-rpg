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
    private readonly CampaignProgressionRules _progression;
    private CampaignBattle? _activeEncounter;

    public Hero Hero { get; }
    public IReadOnlyList<Quest> Quests => _questView;
    public IReadOnlySet<string> CompletedQuestIds => _completedQuestIds.ToHashSet(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> SelectedChoiceIds =>
        new Dictionary<string, string>(_selectedChoiceIds, StringComparer.Ordinal);
    public IReadOnlyList<RecruitmentSite> RecruitmentSites => _recruitmentSiteView;
    public IReadOnlyList<CampaignEncounter> Encounters => _encounters;
    public IReadOnlySet<string> CompletedEncounterIds => _completedEncounterIds.ToHashSet(StringComparer.Ordinal);
    public bool IsVictory => _progression.VictoryEncounterId is not null &&
        _completedEncounterIds.Contains(_progression.VictoryEncounterId);
    public bool IsDefeated { get; private set; }
    public bool IsUnlocked(string unlockId) => !string.IsNullOrWhiteSpace(unlockId) &&
        ((_progression.UnlockEncounterIds.TryGetValue(unlockId, out var encounterId) &&
          _completedEncounterIds.Contains(encounterId)) ||
         (_progression.UnlockEncounterOptions.TryGetValue(unlockId, out var encounterOptions) &&
          encounterOptions.Any(_completedEncounterIds.Contains)));
    public bool CanAccessEncounter(string encounterId) => !IsDefeated && !IsVictory &&
        !_completedEncounterIds.Contains(encounterId) &&
        _encounters.Any(encounter => encounter.Id == encounterId) &&
        Meets(_progression.EncounterRequirements.GetValueOrDefault(encounterId));
    public bool CanAccessRecruitmentSite(string siteId) => !IsDefeated && !IsVictory &&
        _recruitmentSites.ContainsKey(siteId) &&
        Meets(_progression.RecruitmentRequirements.GetValueOrDefault(siteId));

    public BattleMap CreateTravelMap(WorldMap map, HexCoord currentPosition)
    {
        ArgumentNullException.ThrowIfNull(map);
        var markedLocations = map.PointsOfInterest.Select(point => point.Coordinate).ToHashSet();
        var tiles = map.Tiles.Select(tile => new BattleTile(tile.Coordinate, tile.Height,
            tile.IsPassable &&
            (tile.IsRoad || markedLocations.Contains(tile.Coordinate) || tile.Coordinate == currentPosition) &&
            (tile.Coordinate == currentPosition || tile.RequiredUnlock is null || IsUnlocked(tile.RequiredUnlock))));
        return new BattleMap(tiles);
    }

    public WorldPointOfInterest? FindEncounterPointAt(WorldMap map, HexCoord coordinate)
    {
        ArgumentNullException.ThrowIfNull(map);
        var candidates = map.PointsOfInterest
            .Where(point => point.Coordinate == coordinate)
            .Select(point => (Point: point, Encounter: _encounters.FirstOrDefault(encounter =>
                encounter.PointOfInterestId == point.Id)))
            .Where(candidate => candidate.Encounter is not null)
            .ToArray();

        return candidates.FirstOrDefault(candidate => CanAccessEncounter(candidate.Encounter!.Id)).Point ??
               candidates.FirstOrDefault().Point;
    }

    public bool TryGetRecruitmentSiteAt(WorldPointOfInterest point, HexCoord heroPosition,
        out RecruitmentSite? site)
    {
        ArgumentNullException.ThrowIfNull(point);
        site = null;
        if (!point.Type.Equals("recruitment", StringComparison.OrdinalIgnoreCase) ||
            point.Coordinate != heroPosition || string.IsNullOrWhiteSpace(point.RecruitmentSiteId))
            return false;

        site = CanAccessRecruitmentSite(point.RecruitmentSiteId)
            ? _recruitmentSites.GetValueOrDefault(point.RecruitmentSiteId) : null;
        return site is not null;
    }

    public Campaign(Hero hero, IEnumerable<Quest> quests, IEnumerable<RecruitmentSite>? recruitmentSites = null,
        IEnumerable<CampaignEncounter>? encounters = null, CampaignProgressionRules? progression = null)
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
        _progression = progression ?? new CampaignProgressionRules();
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
        if (!CanAccessEncounter(encounter.Id))
            return new(false, "This encounter is locked by campaign progress.");
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

        var field = encounter.BattlefieldId switch
        {
            null or "generated" => new BattlefieldGenerator().Generate(encounter.Seed),
            "mountain-outpost" => MountainOutpostBattlefield.Create(encounter.Seed),
            _ => null
        };
        if (field is null)
            return new(false, $"Unknown battlefield '{encounter.BattlefieldId}'.");
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
        {
            _completedEncounterIds.Add(session.Encounter.Id);
            if (_progression.EncounterReputationRewards.TryGetValue(session.Encounter.Id, out var reputation))
                Hero.ChangeReputation(reputation);
        }
        else if (_progression.DefeatOnArmyWipe && Hero.Army.Stacks.Count == 0)
            IsDefeated = true;
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
        if (IsDefeated || IsVictory)
            return new ChoiceResult(false, "The campaign has ended.");
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

    public CampaignProgressSnapshot GetProgressSnapshot() => new(
        _completedEncounterIds.Order(StringComparer.Ordinal).ToArray(), IsDefeated);

    public CampaignSave CreateSave(string campaignId, string mapId, HexCoord heroPosition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        return new CampaignSave(campaignId, Hero.Id, Hero.Name, mapId, heroPosition, Hero.Gold,
            Hero.Reputation, Hero.Experience, Hero.Level, Hero.SkillPoints,
            Hero.Army.Stacks.Select(stack => new SavedTroopStack(stack.UnitTypeId, stack.Count)).ToArray(),
            _completedQuestIds.Order(StringComparer.Ordinal).ToArray(),
            _selectedChoiceIds.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new SavedQuestChoice(pair.Key, pair.Value)).ToArray(),
            Hero.Skills.Select(skill => skill.Id).ToArray(), Hero.Artifacts.Select(artifact => artifact.Id).ToArray())
        {
            RecruitmentStocks = GetRecruitmentStockSnapshot(),
            CompletedEncounterIds = _completedEncounterIds.Order(StringComparer.Ordinal).ToArray(),
            IsDefeated = IsDefeated
        };
    }

    public bool TryRestoreSave(CampaignSave save, string campaignId, string playerFactionId,
        WorldMap map, UnitCatalog units, out string error)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(units);
        error = "Campaign save restored.";
        if (save is null || save.CompletedEncounterIds is null || save.CompletedQuestIds is null ||
            save.SelectedChoices is null || save.RecruitmentStocks is null || save.Army is null ||
            save.SkillIds is null || save.ArtifactIds is null ||
            save.CampaignId != campaignId || save.HeroId != Hero.Id ||
            save.HeroName != Hero.Name || save.MapId != map.Id || save.SkillIds.Count != 0 || save.ArtifactIds.Count != 0)
            return Fail(out error, "Save belongs to another campaign or contains unsupported hero items.");

        var positionTile = map.GetTile(save.HeroPosition);
        if (positionTile is null || !positionTile.IsPassable)
            return Fail(out error, "Saved hero position is missing or impassable.");

        var completedEncounters = save.CompletedEncounterIds.ToHashSet(StringComparer.Ordinal);
        if (completedEncounters.Count != save.CompletedEncounterIds.Count ||
            completedEncounters.Any(id => !_encounters.Any(encounter => encounter.Id == id)))
            return Fail(out error, "Saved encounter progress contains unknown or duplicate ids.");
        if (save.SelectedChoices.Any(choice => choice is null || string.IsNullOrWhiteSpace(choice.QuestId) ||
                string.IsNullOrWhiteSpace(choice.ChoiceId)))
            return Fail(out error, "Saved quest choices contain empty ids.");
        var selectedChoices = save.SelectedChoices.GroupBy(choice => choice.QuestId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().ChoiceId, StringComparer.Ordinal);
        if (selectedChoices.Count != save.SelectedChoices.Count ||
            selectedChoices.Any(choice => !_quests.TryGetValue(choice.Key, out var quest) ||
                                          quest.GetChoice(choice.Value) is null) ||
            !save.CompletedQuestIds.Order(StringComparer.Ordinal)
                .SequenceEqual(selectedChoices.Keys.Order(StringComparer.Ordinal)))
            return Fail(out error, "Saved choices and completed quests do not match this campaign.");
        if (!HasReachableEncounterHistory(completedEncounters, selectedChoices))
            return Fail(out error, "Saved encounter progress violates campaign progression requirements.");
        if (save.IsDefeated && (!_progression.DefeatOnArmyWipe || save.Army.Count != 0))
            return Fail(out error, "Defeated save must belong to a campaign that ends on an army wipe and have no army.");
        if (save.Army.Any(stack => stack is null || units.Get(stack.UnitId) is not { } unit ||
                                   unit.FactionId != playerFactionId))
            return Fail(out error, "Saved army contains unknown units or units from another faction.");
        if (save.SkillIds.Count != 0 || save.ArtifactIds.Count != 0)
            return Fail(out error, "This campaign build cannot restore skills or artifacts yet.");

        if (save.RecruitmentStocks.Any(stock => stock is null || string.IsNullOrWhiteSpace(stock.SiteId) ||
                string.IsNullOrWhiteSpace(stock.UnitId)))
            return Fail(out error, "Saved recruitment stock contains invalid keys.");
        var savedStocks = save.RecruitmentStocks.GroupBy(stock => (stock.SiteId, stock.UnitId))
            .ToDictionary(group => group.Key, group => group.First().RemainingStock);
        var expectedStocks = _recruitmentSites.Values.SelectMany(site => site.Offers.Select(offer =>
            (Key: (site.Id, offer.UnitTypeId), offer.StartingStock))).ToArray();
        if (savedStocks.Count != save.RecruitmentStocks.Count || savedStocks.Count != expectedStocks.Length ||
            expectedStocks.Any(stock => !savedStocks.TryGetValue(stock.Key, out var count) ||
                                        count < 0 || count > stock.StartingStock))
            return Fail(out error, "Saved recruitment stock is incomplete or outside its valid range.");

        if (save.IsDefeated && save.Army.Count != 0)
            return Fail(out error, "Defeated save cannot contain a surviving army.");
        if (positionTile.RequiredUnlock is { } requiredUnlock && !IsUnlockedInSave(requiredUnlock))
            return Fail(out error, "Saved hero position is behind a locked route.");
        if (!Hero.TryRestoreCampaignState(save.Gold, save.Reputation, save.Experience, save.Level,
                save.SkillPoints, save.Army))
            return Fail(out error, "Saved hero resources or army are invalid.");
        if (!TryRestoreChoices(save.SelectedChoices) ||
            !TryRestoreProgress(new CampaignProgressSnapshot(save.CompletedEncounterIds, save.IsDefeated)) ||
            !TryRestoreRecruitmentStock(save.RecruitmentStocks).Success)
            return Fail(out error, "Campaign progress could not be restored.");

        return true;

        bool IsUnlockedInSave(string unlockId) =>
            _progression.UnlockEncounterIds.TryGetValue(unlockId, out var encounterId) && completedEncounters.Contains(encounterId) ||
            _progression.UnlockEncounterOptions.TryGetValue(unlockId, out var encounterOptions) &&
            encounterOptions.Any(completedEncounters.Contains);
    }

    private bool HasReachableEncounterHistory(IReadOnlySet<string> completed,
        IReadOnlyDictionary<string, string> choices)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = completed.ToHashSet(StringComparer.Ordinal);
        var madeProgress = true;
        while (madeProgress && pending.Count > 0)
        {
            madeProgress = false;
            foreach (var encounterId in pending.ToArray())
            {
                if (!MeetsInSave(_progression.EncounterRequirements.GetValueOrDefault(encounterId)))
                    continue;
                reachable.Add(encounterId);
                pending.Remove(encounterId);
                madeProgress = true;
            }
        }

        return pending.Count == 0;

        bool MeetsInSave(CampaignAccessRequirement? requirement) => requirement is null ||
            (requirement.RequiredEncounterIds.All(reachable.Contains) &&
             (requirement.RequiredUnlockIds?.All(IsUnlockedInReachableSet) ?? true) &&
             (requirement.QuestId is null ||
              (choices.TryGetValue(requirement.QuestId, out var choiceId) &&
               (requirement.ChoiceId is null || requirement.ChoiceId == choiceId))));

        bool IsUnlockedInReachableSet(string unlockId) =>
            _progression.UnlockEncounterIds.TryGetValue(unlockId, out var encounterId) && reachable.Contains(encounterId) ||
            _progression.UnlockEncounterOptions.TryGetValue(unlockId, out var encounterOptions) &&
            encounterOptions.Any(reachable.Contains);
    }

    private static bool Fail(out string error, string message)
    {
        error = message;
        return false;
    }

    public bool TryRestoreChoices(IReadOnlyList<SavedQuestChoice> choices)
    {
        if (choices is null || choices.Any(choice => choice is null ||
            !_quests.TryGetValue(choice.QuestId, out var quest) || quest.GetChoice(choice.ChoiceId) is null) ||
            choices.Select(choice => choice.QuestId).Distinct(StringComparer.Ordinal).Count() != choices.Count)
            return false;
        _selectedChoiceIds.Clear();
        _completedQuestIds.Clear();
        foreach (var choice in choices)
        {
            _selectedChoiceIds.Add(choice.QuestId, choice.ChoiceId);
            _completedQuestIds.Add(choice.QuestId);
        }
        return true;
    }

    public bool TryRestoreProgress(CampaignProgressSnapshot snapshot)
    {
        if (snapshot is null || snapshot.CompletedEncounterIds is null ||
            snapshot.CompletedEncounterIds.Any(string.IsNullOrWhiteSpace) ||
            snapshot.CompletedEncounterIds.Distinct(StringComparer.Ordinal).Count() != snapshot.CompletedEncounterIds.Count ||
            snapshot.CompletedEncounterIds.Any(id => !_encounters.Any(encounter => encounter.Id == id)) ||
            (snapshot.IsDefeated && (!_progression.DefeatOnArmyWipe || Hero.Army.Stacks.Count != 0)))
            return false;
        _completedEncounterIds.Clear();
        foreach (var id in snapshot.CompletedEncounterIds)
            _completedEncounterIds.Add(id);
        IsDefeated = snapshot.IsDefeated;
        return true;
    }

    private bool Meets(CampaignAccessRequirement? requirement) => requirement is null ||
        (requirement.RequiredEncounterIds.All(_completedEncounterIds.Contains) &&
         (requirement.RequiredUnlockIds?.All(IsUnlocked) ?? true) &&
         (requirement.QuestId is null ||
          (_selectedChoiceIds.TryGetValue(requirement.QuestId, out var choiceId) &&
           (requirement.ChoiceId is null || choiceId == requirement.ChoiceId))));
}

public sealed record RecruitmentStockRestoreResult(bool Success, string Message);
public sealed record CampaignProgressSnapshot(IReadOnlyList<string> CompletedEncounterIds, bool IsDefeated);
