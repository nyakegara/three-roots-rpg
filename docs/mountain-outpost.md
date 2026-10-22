# Mountain outpost battlefield

The campaign outpost uses a fixed eastern plateau at height +2. The closed cliff occupies column 6, rows 0–4; the three southern rows form a broad ascent at +1. Enemy deployment is three knights at the foot, two hunters guarding the ascent, and two stacks of four archers on the plateau. Other encounters retain their existing deployment.

The cliff blocks walking but does not itself block line of sight. Existing range and height rules still apply to both armies. A tested route from column 3, row 2 to column 9, row 2 costs nine steps. Slow units can take longer than three activations to reach their chosen target, especially around occupied cells.

Reproduce the visual fixture (does not load or write campaign saves):

```powershell
dotnet run --project src/Game.MonoGame/Game.MonoGame.csproj -- --screenshot-combat-outpost output/mountain-outpost.png
```

The fixture uses the outpost terrain and exact enemy stack counts, with a sample player army. It is not a recorded campaign playthrough. Independent visual review approved the readable elevation after adding local height projection and rock faces; the terrain texture remains simple.

Validation: 533 functional tests passed, including forced lower detour, return fire over the cliff and ascent with occupied defender positions. Five previously known campaign balance cases were excluded from this run; campaign balance is not certified by this change.
