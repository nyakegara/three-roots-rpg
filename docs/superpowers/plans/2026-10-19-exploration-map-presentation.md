# Exploration Map Presentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the hex-rendered exploration prototype with a large 2D isometric adventure map that pans by mouse-edge scrolling, while keeping battles on their own visible hex battlefield.

**Architecture:** Keep axial coordinates and pathfinding in `Game.Core`; add framework-independent isometric projection and camera math there. The MonoGame exploration screen will compose a scrollable terrain/object layer and a fixed campaign HUD, while `BattlePreviewScreen` remains the tactical presentation.

**Tech Stack:** C#, .NET 10, MonoGame DesktopGL, xUnit, JSON map content.

**Spec:** `docs/superpowers/specs/2026-10-19-exploration-map-presentation-design.md`

## Global Constraints

- Only C# is used for game code and logic; no game engine is added.
- `Game.Core` has no MonoGame dependency.
- Exploration uses a 2D overhead view with a slight isometric tilt and no visible hex outlines.
- The exploration map is larger than its viewport; moving the cursor to the map viewport edge pans the camera.
- The bottom exploration HUD stays fixed while the map moves.
- Battle retains its separate hex grid, battle HUD, and tactical elevation display.
- Temporary pixel art uses stable asset keys and remains replaceable by PNG files.

## Review Focus

- Cursor outside the map viewport, including over the HUD, must not pan the camera (`AdventureMapCameraTests.Update_CursorOutsideViewport_DoesNotPan`).
- Zero or negative elapsed time must not move the camera (`AdventureMapCameraTests.Update_NonPositiveElapsedTime_DoesNotPan`).
- Maps smaller than the viewport must stay clamped at the origin (`AdventureMapCameraTests.Pan_MapSmallerThanViewport_StaysAtOrigin`).
- Clicking a hole in a sparse map must not select a nonexistent tile (`AdventureMapProjectionTests.TryPick_EmptyMapSpace_ReturnsFalse`).
- Picking with a camera offset and terrain elevation must resolve the intended tile (`AdventureMapProjectionTests.TryPick_ProjectAndCameraOffset_SelectsOriginalTile`).

---

### Task 1: Framework-independent adventure projection and camera

**Files:**
- Create: `src/Game.Core/Geometry/AdventureMapProjection.cs`
- Create: `src/Game.Core/Geometry/AdventureMapCamera.cs`
- Test: `tests/Game.Tests/Geometry/AdventureMapProjectionTests.cs`
- Test: `tests/Game.Tests/Geometry/AdventureMapCameraTests.cs`

**Interfaces:**
- `AdventureMapProjection(WorldMap map, float tileWidth, float tileHeight, float elevationStep)` exposes `Vector2 Project(HexCoord coordinate, int height = 0)`, `Vector2 WorldSize`, and `bool TryPick(Vector2 worldPosition, out HexCoord coordinate)`.
- Projection uses a 2:1 isometric basis: `(q-r) * tileWidth / 2` for X and `(q+r) * tileHeight / 2 - height * elevationStep` for Y; picking returns the closest existing tile only within the configured tile footprint.
- `AdventureMapCamera(Vector2 viewportSize, Vector2 worldSize, Vector2 initialPosition)` exposes `Vector2 Position`, `Update(Vector2 pointer, float elapsedSeconds)`, `ToScreen(Vector2 worldPosition)`, and `ToWorld(Vector2 screenPosition)`.
- Edge scrolling uses a 20-pixel edge band and 360 world pixels per second; camera position is clamped to `[0, worldSize - viewportSize]` on each axis. Pointers outside the map viewport and nonpositive elapsed time do not move it.

- [x] **Step 1: Write failing projection and camera tests**

  Cover the five cases in Review Focus, plus known isometric neighbor positions and elevation offset. Verify picking after `camera.ToWorld(camera.ToScreen(projection.Project(...)))`.

- [x] **Step 2: Run the focused tests and verify they fail for missing types**

  Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --configuration Debug --filter "FullyQualifiedName~AdventureMapProjectionTests|FullyQualifiedName~AdventureMapCameraTests"`

  Expected: compile failure because the projection and camera types do not exist yet.

- [x] **Step 3: Implement the pure geometry types**

  Keep all calculations on `System.Numerics.Vector2`; do not reference MonoGame types. Clamp after every edge-scroll update and make `ToWorld` the inverse of `ToScreen`.

- [x] **Step 4: Run the focused tests and verify they pass**

  Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --configuration Debug --filter "FullyQualifiedName~AdventureMapProjectionTests|FullyQualifiedName~AdventureMapCameraTests"`

  Expected: all projection and camera tests pass.

- [ ] **Step 5: Commit**

  Commit message: `feat(map): add isometric projection and edge camera`

### Task 2: Expand tutorial content beyond the viewport

**Files:**
- Modify: `content/maps/tutorial.json`
- Test: `tests/Game.Tests/Content/MapLoaderTests.cs`

**Interfaces:**
- Continue using the existing JSON map format and `MapLoader.Load(string path)`.
- The tutorial map becomes 20 columns by 14 rows, with a complete passable base, distinct forest/hill/water/rock areas, start position, old-oak quest, and hill-stash point of interest.

- [ ] **Step 1: Add a failing tutorial-map extent test**

  Extend `Load_TutorialContentFile_ReturnsPlayableMap` to assert width is 20, height is 14, the start is passable, and both points of interest refer to existing tiles.

- [ ] **Step 2: Run the map-loader test and verify the current 6-by-5 map fails the extent assertions**

  Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --configuration Debug --filter FullyQualifiedName~Load_TutorialContentFile_ReturnsPlayableMap`

  Expected: FAIL because the tutorial map is smaller than the new scrollable dimensions.

- [ ] **Step 3: Expand `tutorial.json` to a connected 20-by-14 adventure map**

  Preserve the existing start and both point-of-interest IDs. Add continuous travel space and terrain areas; keep blocked rocks away from any required route.

- [ ] **Step 4: Run the map-loader test and verify it passes**

  Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --configuration Debug --filter FullyQualifiedName~Load_TutorialContentFile_ReturnsPlayableMap`

  Expected: PASS with the expanded map and valid objective coordinates.

- [ ] **Step 5: Commit**

  Commit message: `feat(content): expand tutorial adventure map`

### Task 3: Draw the adventure map as a continuous scenic layer

**Files:**
- Create: `src/Game.MonoGame/Rendering/AdventureMapRenderer.cs`
- Modify: `src/Game.MonoGame/ExplorationArt.cs`
- Modify: `src/Game.MonoGame/AssetCatalog.cs`
- Modify: `src/Game.MonoGame/PixelCanvas.cs`
- Modify: `src/Game.MonoGame/Game1.cs`

**Interfaces:**
- `AdventureMapRenderer(PixelCanvas canvas, AssetCatalog assets).Draw(WorldMap map, AdventureMapProjection projection, AdventureMapCamera camera, HexCoord heroPosition, IReadOnlyList<HexCoord> route, HexCoord? hoveredTile)` draws the camera-visible map region.
- `PixelCanvas.PushClip(Rectangle viewport)` returns an `IDisposable` scope that restores the previous scissor rectangle; `Game1` begins its sprite batch with scissor testing enabled.
- Draw order is ground, terrain zones, route, then scenery/points-of-interest/hero sorted together by projected foot Y, followed by the hover marker. This lets foreground objects overlap the hero correctly.
- Terrain and object textures are optional overrides with stable keys in `AssetCatalog`; missing assets use pixel-art fallbacks.

- [x] **Step 1: Capture the existing exploration view as a visual baseline**

  Run `dotnet run --project src/Game.MonoGame/Game.MonoGame.csproj --configuration Debug -- --screenshot C:\Temp\exploration-before.png` and verify the frame is saved.

- [x] **Step 2: Implement the continuous ground and depth-sorted object renderer**

  Remove visible tile polygons, hex outlines, elevation-edge markers, and terrain-height hover text from exploration rendering. Retain terrain and elevation data for route/blocking and scenic hill placement. Clip all map layers to the map viewport through `PixelCanvas.PushClip`; keep the battle renderer unchanged.

- [x] **Step 3: Capture the renderer result and inspect it at 1280×720**

  Expected: no visible hex field; terrain spans beyond the viewport and has distinct grass, forest, hill, water, rock, hero, quest, and treasure silhouettes.

- [ ] **Step 4: Commit**

  Commit message: `feat(ui): render exploration as an isometric adventure map`

### Task 4: Build a fixed exploration HUD

**Files:**
- Create: `src/Game.MonoGame/Rendering/ExplorationHud.cs`
- Modify: `src/Game.MonoGame/AssetCatalog.cs`
- Test: manual frame capture reviewed in Task 6

**Interfaces:**
- `ExplorationHud(PixelCanvas canvas, AssetCatalog assets).Draw(Hero hero, WorldPointOfInterest? hoveredPoint, string actionText)` reads `Hero.Name`, `Hero.Gold`, and `Hero.Army.Stacks`; it draws only in screen coordinates and never applies the map camera transform.
- The lower wood/bronze bar shows the portrait, five army slots, gold, and map/quest commands; hover name and action fit within the same bar. It does not add a route sidebar.

- [x] **Step 1: Implement the fixed five-slot layout and HUD draw call**

  Keep empty slots visible and draw occupied slots from the supplied hero's stack list.

- [x] **Step 2: Capture the tutorial HUD at 1280×720 and inspect its bounds and text fit**

  Expected: every label, slot, portrait, and command stays inside the fixed lower panel without overlapping the map viewport.

- [ ] **Step 3: Commit**

  Commit message: `feat(ui): add campaign exploration HUD`

### Task 5: Integrate camera scrolling, picking, and route confirmation

**Files:**
- Modify: `src/Game.MonoGame/Screens/ExplorationScreen.cs`
- Modify: `src/Game.MonoGame/WorldMapLayout.cs`
- Modify: `src/Game.MonoGame/Game1.cs`
- Modify: `src/Game.MonoGame/Program.cs`

**Interfaces:**
- `ExplorationScreen` delegates map painting to `AdventureMapRenderer`, fixed UI to `ExplorationHud`, and mouse edge motion to `AdventureMapCamera`.
- Pointer coordinates convert screen-to-world through `AdventureMapCamera.ToWorld` before `AdventureMapProjection.TryPick`; clicks over HUD controls cannot select map tiles.
- `MovementSelection` remains the only authority for first-click preview and second-click confirmation. Camera movement never changes `MovementSelection.Position`.
- Until a campaign loader supplies the selected protagonist, `Game1.LoadTutorialHero()` returns `new Hero("tutorial-hero", "Герой")` and the HUD displays this existing `Game.Core` model.

- [x] **Step 1: Run existing movement tests as the route confirmation baseline**

  Run `dotnet test tests/Game.Tests/Game.Tests.csproj --configuration Debug --filter FullyQualifiedName~MovementSelectionTests` and confirm first-click preview/second-click confirmation still passes.

- [x] **Step 2: Integrate edge scrolling, picking, route preview, and the fixed HUD**

  Remove the exploration `G` grid toggle. Keep battle grid controls and battle screenshot switches intact. Initialize the camera around the tutorial start and clamp it to map bounds.

- [x] **Step 3: Run the full test suite and launch the interactive game**

  Run `dotnet test ThreeRoots.sln --configuration Debug`, then `dotnet run --project src/Game.MonoGame/Game.MonoGame.csproj --configuration Debug`.

  Expected: cursor-edge panning moves only the map; the HUD remains stationary; two-click movement still previews then follows the displayed route.

- [ ] **Step 4: Commit**

  Commit message: `feat(ui): integrate adventure camera and movement`

### Task 6: Verify visuals, update documentation, and prepare review

**Files:**
- Modify: `README.md`
- Modify: `content/art/README.md`
- Modify: `docs/superpowers/plans/2026-10-19-three-roots-rpg.md`
- Create: `docs/screenshots/exploration.png`
- Create: `docs/screenshots/exploration-quest-hover.png`

**Interfaces:**
- Screenshot mode captures the ordinary map state and a hovered quest state from the actual MonoGame renderer.
- The visual reviewer checks the two screenshots against the design spec; the user reviews the screenshots before any next UI element starts.

- [x] **Step 1: Run complete Release build, tests, and whitespace checks**

  Run: `dotnet build ThreeRoots.sln --configuration Release`

  Run: `dotnet test ThreeRoots.sln --configuration Release`

  Run: `git diff --check`

  Expected: all pass; investigate and repair any failure before continuing.

- [x] **Step 2: Capture and review actual application screenshots**

  Capture a clean map and a hovered POI. Send both to the visual quality subagent and fix any blocking readability, camera, layering, or HUD defects.

- [x] **Step 3: Update the README and screenshot artifacts**

  Document edge scrolling, map selection, battle separation, and asset replacement. Include only the final captures.

- [ ] **Step 4: Commit**

  Commit message: `docs: document the separate exploration map presentation`

- [ ] **Step 5: Push the completed branch as a draft PR and wait for user visual review**

  Link the PR, show both screenshots in chat, report local/CI test results, and leave the PR unmerged until the user approves the screen.
