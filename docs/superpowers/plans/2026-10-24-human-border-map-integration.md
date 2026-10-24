# Flooded Border Map Integration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render the user's upscaled second-campaign map in the game and allow free walking on the pictured land and crossings while keeping deep water and solid terrain impassable.

**Architecture:** Keep the 6688×3764 supplied PNG byte-identical as a master. Load it as the `human-01` scenic ground; use map-authored navigation polygons in 1672×941 reference coordinates (one quarter of the master dimensions) to separate drawing from movement. Existing quest points gain art anchors. The current campaign story and encounter balance stay unchanged.

**Tech Stack:** .NET 10, MonoGame, JSON map content, xUnit.

**Spec:** `docs/design/campaign-02-flooded-border.md`; master art `content/art/source/exploration.human-border-master-v1.png`.

## Global Constraints

- The user supplied the exact master; do not re-encode it.
- Preserve separate, scrollable art and invisible navigation; no visible hex grid.
- Two fords remain usable; the southern dam is a distinct crossing.
- This step aligns the existing placeholder quests; the branch campaign is not implemented here.

## Review Focus

- A destination in the middle of deep river water must be rejected.
- A path across the river must use an authored crossing.
- Hero start and each existing landmark must be walkable and approachable.
- The map and overview panel must use the same art-to-world coordinates.
- A missing image must retain the previous procedural fallback rather than crashing.

### Task 1: Navigation

**Files:** `content/maps/human-01.json`, `tests/Game.Tests/Content/AdventureMapContentTests.cs`.

- [x] Add failing tests for map load, landmark approach, both ford routes, dam route, and sample water obstacles.
- [x] Run the focused tests and confirm failure for missing navigation.
- [x] Author walkable and blocked polygons, art anchors, and start in quarter-resolution image coordinates.
- [x] Run the focused tests; refine polygons until crossings and exclusions pass.

### Task 2: Scenic rendering

**Files:** `src/Game.MonoGame/AssetCatalog.cs`, `src/Game.MonoGame/Rendering/AdventureMapRenderer.cs`, `src/Game.MonoGame/Screens/ExplorationPanels.cs`, `src/Game.MonoGame/Game.MonoGame.csproj`, derived runtime image asset.

- [x] Add screenshot fixtures for the human ground, overview and diagnostic navigation outline.
- [x] Register the supplied map, draw it as `human-01` ground and in its overview panel, and keep procedural fallback.
- [x] Build and run focused tests. Capture exploration and overview screenshots.

### Task 3: Final checks

- [x] Verify master SHA-256 still matches `66498BA1D48A2EB10B0BDB06FA41A915551F391F6428122C62320131B6CFD6FB`.
- [x] Run relevant content/navigation tests and build. Full suite: 554 passed, five existing `CampaignBalanceTests` autopath failures at the mountain outpost remain; enemy balance unchanged by this work.
- [ ] Commit and push to PR #60; update issue #61 according to actual completion.
