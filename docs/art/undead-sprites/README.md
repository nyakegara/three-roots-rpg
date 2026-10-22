# Undead static sprite prototypes

Five generated transparent idle-pose candidates following the selected character concepts. These files are not wired into gameplay or the old undead unit definitions. They contain no animation or new combat behavior.

Open `index.html` to compare the sprites against the existing peasant, cucumber and knight. `comparison.png` shows native 1x and nearest-neighbor 2x. This is an art comparison, not a screenshot of a battle.

Exports are 128x128 RGBA PNGs, one output pixel per logical game pixel, common baseline y=120. Maximum visible boxes: Utburd 48x66, Silat 56x66, Nachzehrer 84x70, Vetala 56x72, Asura-Preta 112x108. Proportions are retained within each box; Asura is deliberately larger. Transparent padding is not body size.

Run `pwsh -File docs/art/undead-sprites/Export.ps1` from any directory to reproduce exports and comparison. The packaging script only crops alpha bounds and samples nearest-neighbor; character drawing and design changes use image_gen. Sources are archived unchanged in `sources/`; `utburd-v1.png` preserves the first horror treatment. The soft simplified Utburd was rejected by the user and is not selected.

Selected concepts: Silat D (crouched veiled sand spirit), Utburd B (burial bundle on thin supporting limbs), Vetala B from the refinement sheet (aristocratic vampire), Nachzehrer hybrid B/D, Asura original D with large sword and separate giant upper hands.

Generation uses the built-in image_gen tool. Initial prompts and refinement notes are in `prompts.json`. Scale comparison and visual review are required before gameplay integration; per-unit scale will need to use these native visible dimensions rather than shrinking every 128x128 canvas into a standard unit box.
