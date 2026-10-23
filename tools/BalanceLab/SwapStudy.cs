using System.Text.Json;
using Game.Core.Combat;
using Game.Core.Combat.AI;
using Game.Core.Combat.Generation;
using Game.Core.Content;
using Game.Core.Geometry;

internal static class SwapStudy
{
    private const int RoundLimit = 35;

    private sealed record Slot(string Human, string Flora, string Undead, int Tier);
    private sealed record Scenario(string Name, Slot[] Slots);
    private sealed record Result(string Scenario, int StackCount, string Opponent,
        int ReplacedSlot, string ReplacedHuman, string Replacement, string ReplacementFaction,
        int Battles, double BaselineWinRate, double ReplacementWinRate, double DeltaPercentagePoints);

    private static readonly Slot Militia = new("human.militia", "flora.thorn-guard", "undead.utburd", 1);
    private static readonly Slot Longbow = new("human.longbow", "flora.sprout", "undead.silat", 1);
    private static readonly Slot Knight = new("human.banner-knight", "flora.pod-slinger", "undead.vetala", 2);
    private static readonly Slot Hunter = new("human.order-hunter", "flora.ember-bloom", "undead.nachzehrer", 2);
    private static readonly Slot Inquisitor = new("human.inquisitor", "flora.root-warden", "undead.asura-preta", 3);
    private static readonly IReadOnlyDictionary<int, string[]> TierCandidates = new Dictionary<int, string[]>
    {
        [1] = ["flora.thorn-guard", "flora.sprout", "undead.utburd", "undead.silat"],
        [2] = ["flora.ember-bloom", "flora.pod-slinger", "undead.nachzehrer", "undead.vetala"],
        [3] = ["flora.root-warden", "undead.asura-preta"]
    };
    private static readonly Scenario[] Scenarios = [
        new("t1-melee-1v1", [Militia]),
        new("t1-ranged-1v1", [Longbow]),
        new("t1-pair-2v2", [Militia, Longbow]),
        new("t1-three-3v3", [Militia, Militia, Longbow]),
        new("t1-t2-three-3v3", [Militia, Longbow, Knight]),
        new("t1-t2-four-4v4", [Militia, Longbow, Knight, Hunter]),
        new("full-five-5v5", [Militia, Longbow, Knight, Hunter, Inquisitor])
    ];

    public static void Run(string root, int samples, int guardCost = 0, int goldPerStack = 90)
    {
        if (samples is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(samples));
        if (guardCost is < 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(guardCost));
        if (goldPerStack is < 90 or > 1000) throw new ArgumentOutOfRangeException(nameof(goldPerStack));
        if (guardCost > goldPerStack) throw new ArgumentOutOfRangeException(nameof(guardCost));
        var loaded = UnitCatalog.Load(Path.Combine(root, "content/units/units.json"));
        if (!loaded.Success) throw new Exception(string.Join("; ", loaded.Errors.Select(error => error.Message)));
        var catalog = loaded.Catalog!;
        var results = new List<Result>();
        var maps = Enumerable.Range(0, samples)
            .Select(seed => new BattlefieldGenerator().Generate(10000 + seed * 37).Map).ToArray();

        foreach (var scenario in Scenarios)
        foreach (var opponent in new[] { "flora", "undead" })
        {
            var human = scenario.Slots.Select(slot => slot.Human).ToArray();
            var enemy = scenario.Slots.Select(slot => opponent == "flora" ? slot.Flora : slot.Undead).ToArray();
            var baseline = Measure(human, enemy, catalog, maps, guardCost, goldPerStack);
            for (var slotIndex = 0; slotIndex < human.Length; slotIndex++)
            foreach (var candidate in TierCandidates[scenario.Slots[slotIndex].Tier])
            {
                var replaced = (string[])human.Clone();
                replaced[slotIndex] = candidate;
                var score = Measure(replaced, enemy, catalog, maps, guardCost, goldPerStack);
                results.Add(new Result(scenario.Name, human.Length, opponent, slotIndex,
                    human[slotIndex], candidate, catalog.Get(candidate)!.FactionId, samples * 2,
                    baseline, score, Math.Round((score - baseline) * 100, 2)));
            }
            Console.WriteLine($"{scenario.Name} vs {opponent}: base {baseline:P1}; " +
                string.Join(", ", results.Where(result => result.Scenario == scenario.Name && result.Opponent == opponent)
                    .OrderByDescending(result => result.DeltaPercentagePoints).Take(3)
                    .Select(result => $"{result.Replacement} @ {result.ReplacedHuman} {result.DeltaPercentagePoints:+0.##;-0.##;0}pp")));
        }

        var suffix = (guardCost == 0 ? "" : $"-guard-cost-{guardCost}") +
            (goldPerStack == 90 ? "" : $"-budget-{goldPerStack}");
        var output = Path.Combine(root, "docs", $"balance-lab-swap-study{suffix}.json");
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            generatedAt = DateTimeOffset.UtcNow,
            samplesPerSide = samples,
            battlesPerVariant = samples * 2,
            goldPerStack,
            guardCostOverride = guardCost == 0 ? null : (int?)guardCost,
            terrain = "generated, fixed seed per paired comparison",
            scenario = "Human army vs matched flora/undead roster; replace exactly one human stack with a same-tier type; reverse sides",
            results
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Saved {results.Count} replacements to {output}");
    }

    private static double Measure(string[] humanIds, string[] enemyIds, UnitCatalog catalog, BattleMap[] maps,
        int guardCost, int goldPerStack)
    {
        double humanWins = 0;
        for (var seed = 0; seed < maps.Length; seed++)
        for (var swap = 0; swap < 2; swap++)
            humanWins += Simulate(humanIds, enemyIds, catalog, maps[seed], seed, swap, guardCost, goldPerStack);
        return humanWins / (maps.Length * 2);
    }

    private static double Simulate(string[] humanIds, string[] enemyIds, UnitCatalog catalog,
        BattleMap map, int seed, int swap, int guardCost, int goldPerStack)
    {
        var left = swap == 0 ? humanIds : enemyIds;
        var right = swap == 0 ? enemyIds : humanIds;
        var units = new List<BattleUnit>();
        for (var i = 0; i < left.Length; i++)
        {
            var row = i + (left.Length == 1 ? 3 : 1);
            units.Add(Stack(left[i], $"L{i}", BattleSide.Player, new HexCoord(2 - row / 2, row)));
            units.Add(Stack(right[i], $"R{i}", BattleSide.Enemy, new HexCoord(9 - row / 2, row)));
        }
        var battle = new Battle(map, units, new Random(seed * 23 + 1729));
        var ai = new BattleAi();
        var actions = 0;
        while (!battle.IsFinished && battle.Round < RoundLimit && actions++ < 2000)
        {
            if (battle.ActiveUnit is null) { battle.StartRound(); continue; }
            var actor = battle.ActiveUnit;
            var action = ai.ChooseAction(battle, actor.Id);
            var applied = action.Type switch
            {
                BattleActionType.Attack => battle.Attack(actor.Id, action.TargetId!),
                BattleActionType.Heal => battle.TryHeal(actor.Id, action.TargetId!),
                BattleActionType.Move => battle.Move(actor.Id, action.Destination!.Value),
                BattleActionType.SetAttackMode => battle.TrySetAttackMode(actor.Id, action.Mode!.Value),
                _ => false
            };
            if (!applied) battle.EndActivation();
        }
        if (battle.Winner is null) return 0.5;
        var humansArePlayer = swap == 0;
        return (battle.Winner == BattleSide.Player) == humansArePlayer ? 1 : 0;

        BattleUnit Stack(string id, string instanceId, BattleSide side, HexCoord position)
        {
            var type = catalog.Get(id) ?? throw new InvalidOperationException($"Unknown unit {id}");
            var cost = guardCost > 0 && id == "flora.thorn-guard" ? guardCost : type.RecruitmentCost;
            var count = Math.Max(1, goldPerStack / cost);
            return new BattleUnit(instanceId, side, position, count, type.HitPoints, type.Damage,
                type.Speed, type.Movement, type.Range, type.IsRanged,
                ability: type.Ability, unitTypeId: type.Id);
        }
    }
}
