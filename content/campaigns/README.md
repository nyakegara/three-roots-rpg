# Campaign encounters

Campaign files may include an optional `encounters` array. Omitting it or using an empty array leaves the campaign without battles. Existing quests may share an encounter's map point; recruitment points cannot.

```json
"encounters": [
  {
    "id": "road-patrol",
    "pointOfInterestId": "iron-road",
    "name": "Дозор у стройки",
    "seed": 20261019,
    "enemyArmy": [{ "unitId": "human.militia", "count": 4 }],
    "goldReward": 60,
    "experienceReward": 60
  }
]
```

Each encounter requires a unique nonempty ID, an existing map point linked to no other encounter, a nonempty name, an integer seed, and nonnegative integer rewards. The enemy army requires one to five stacks with unique known unit IDs belonging to the campaign's `enemyFactionId`. Counts must be positive integers, and each stack's count multiplied by its unit's hit points must fit in a signed 32-bit integer. Invalid content returns file diagnostics.

Flora's `road-patrol` uses the existing `iron-road` quest point without changing the map. Human and undead campaign files currently omit encounters.
