# Task 1 report — catalog ability metadata

## Changes

- Added `Game.Core.Combat.UnitAbility` with `None`, `HealAlly`, and `ConvertOnHit`.
- Added `Ability` to `UnitDefinition`, defaulting to `None` for source compatibility.
- Added explicit enum-name string parsing in `UnitCatalog`; unknown values return a diagnostic naming the unit and value.
- Declared `HealAlly` for `flora.sprout`, `ConvertOnHit` for `flora.root-warden`, and `None` for every other catalog entry.
- Added catalog behavior tests for the assignments and for unknown ability diagnostics.

## TDD evidence

- RED: `dotnet test tests/Game.Tests/Game.Tests.csproj --filter FullyQualifiedName~UnitCatalogTests` failed to compile with missing `UnitAbility` and `UnitDefinition.Ability` errors, as expected before production implementation.
- GREEN: the same focused command passed after implementation: 4 passed, 0 failed.

## Verification

- `dotnet test tests/Game.Tests/Game.Tests.csproj --filter FullyQualifiedName~UnitCatalogTests` — passed, 4/4.
- `dotnet test ThreeRoots.sln` — passed, 127/127.
- `git diff --check` — passed.

## Self-review

- The catalog explicitly names every ability value; only the two requested flora units have non-`None` abilities.
- Loader parsing accepts exact enum names and reports unknown, numeric, or differently cased values instead of silently defaulting them.
- Changes are limited to Task 1 implementation, its tests, unit JSON, and this report.

## Commit

`feat(content): declare flora unit abilities`
