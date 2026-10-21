# Battlefield generation and lighting implementation plan

> **For agentic workers:** Use superpowers:subagent-driven-development, with independent code and visual review before completion.

**Goal:** Generate repeatable, traversable tactical clearings with hills and props, with readable day/night presentation. Leave exploration maps and encounter placement unchanged until the user's map sketch.

**Architecture:** Plain C# generator in Game.Core creates immutable terrain and prop data. MonoGame draws this data and applies an ambient color palette to world art only. Existing two-click movement, high-ground range rules and default grid remain intact.

**Tech stack:** Existing .NET 10, C#, MonoGame and xUnit; no additional packages or engines.

**Spec:** Existing three-campaign RPG design and the user's request for varied battle terrain/objects and simple day/night lighting.

## Global constraints

- Keep code teachable for an OOP coursework project; no noise libraries, shaders, physics or dynamic-light engine.
- Fixed 12-column, 8-row odd-row-offset field stored as axial HexCoord cells.
- Heights 0..2, coherent hills with adjacent height differences at most one.
- Protect columns 0..2 and 9..11 for deployment; all passable cells stay connected.
- Rocks block movement and line of sight; fallen logs block movement only; shrubs are decoration.
- Day/night affects presentation, not damage, vision, AI or high-ground bonuses.
- Preserve sharp original PNG rendering and the current exploration screen.

## Review focus

- Extreme/negative seeds reproduce without failures or disconnected areas.
- Props cannot overlap deployment cells or each other, and agree with terrain passability.
- Regeneration clears stale route/occupancy selection and retains chosen lighting.
- Night remains readable, especially grid, path, units, height edges and HUD.
- Existing screenshot modes remain supported, including the fixed descent fixture.

## Task 1: Generator and invariants

Files: `src/Game.Core/Combat/Generation/{BattlefieldGenerator,GeneratedBattlefield,BattlefieldProp}.cs`; matching tests in `tests/Game.Tests/Combat/Generation/`.

Interface: `BattlefieldGenerator.Generate(int seed) -> GeneratedBattlefield` with `Seed`, `Map`, `Props`, `PlayerStart`, `EnemyStart`. Prop record: `BattlefieldProp(HexCoord Coordinate, BattlefieldPropKind Kind)`; kinds `Rock`, `FallenLog`, `Shrub`. Result properties expose immutable/read-only collections.

- [x] Write failing tests for determinism, seed variation, 96 cells, height range/adjacent differences, protected deployment, unique props and passability/LOS agreement.
- [x] Test whole-field connectivity over 200 seeds plus int.MinValue/int.MaxValue.
- [x] Generate 2–3 interior hills using hex distance; cap elevations next to protected deployment zones to avoid abrupt cliffs.
- [x] Shuffle candidate cells; accept 8–12 blocking props only if a flood fill still reaches every passable cell; add decorative shrubs on separate available cells.
- [x] Run focused tests and independent review.

## Task 2: Battlefield presentation

Files: `BattlePreviewScreen.cs`, `PixelCanvas.cs`, `FantasyArt.cs`, new small lighting/prop rendering helpers under `src/Game.MonoGame/`; lighting tests in `tests/Game.Tests/`.

Interface: append constructor options `int seed = 1729, bool night = false` after existing preview options. Use the generator unless `previewDescent` requests the legacy fixed fixture.

- [x] Keep `R` resetting movement on the same field; add `F5` for next generated field and `N` for a smooth day/night palette transition.
- [x] Draw rocks, fallen logs and shrubs in restrained, simple pixel shapes with terrain-consistent occupancy. Show height edges independently of grid toggle.
- [x] Add a seeded woodland/horizon backdrop and small sun/moon indication. No generated bitmap background is needed.
- [x] Scope palette tint to field/background/props/units; restore it before drawing overlays/HUD. Test palette endpoints, clamped transition and alpha-preserving tint.
- [x] Capture day/night for the same seed and a different generated field; obtain independent visual review.

## Task 3: Launch options and end-to-end verification

Files: `Program.cs`, `Game1.cs`, `GameLaunchOptions.cs`, CLI tests, README instructions.

- [x] Introduce testable parsing preserving all existing screenshot modes.
- [x] Support `--battle`, `--seed <int>` and `--night`; screenshot mode `--screenshot-grid <absolute path>` accepts seed/night options. Reject incompatible/unknown/incomplete arguments clearly.
- [x] Pass options into battle preview; default game still opens exploration.
- [x] Run full Release build/tests and diff-check, capture actual game screenshots and independently review final code.
- [x] Record known limits honestly: this is generated tactical terrain preview; campaign-to-combat integration remains separate work.

## Verification

- Release solution build: 0 warnings, 0 errors; all 244 tests pass.
- Generator invariants checked on seeds -100..99 plus int.MinValue and int.MaxValue.
- Actual screenshots: battle-generated-day.png, battle-generated-night.png and battle-generated-seed42.png under the task's artifacts/screenshots output.
- Fixed descent fixture and unchanged exploration launch both captured successfully through their original CLI modes.
- Comparing day/night screenshots for the same seed: all 140800 HUD pixels at y=610..719 are identical; 47520 of 48000 sampled world pixels change.
- Independent code review: no functional blockers. Independent visual review approves the checkpoint; additional unit contrast at night may be refined after user playtesting.
