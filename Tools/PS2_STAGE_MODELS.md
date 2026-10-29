# PS2 stage models

`extract_ps2_stage_models.py` reads the local `Sega Ages Vol. 9 Gain Ground.iso`
and decodes the stage-specific model binaries into `Analysis/PS2StageModels`.
The Unity command **Gain Ground > Import all PS2 stage model prefabs** regenerates
the editable prefabs in `Assets/GainGround/Editable/OriginalCampaign/Objects/Models3D`.
They share the existing `StageTextures/<stage>/Constructions.png` and
`Elevations.png` atlases. **Gain Ground > Link PS2 stage model palettes** exposes
the imported prefabs through each stage's texture palette.

The ISO contains 449 candidate `.bin` models across the 50 stage packs. The
decoder produced 448 renderable prefabs. `r2s02_06.bin` has no mesh vertices and
is recorded in `Analysis/PS2StageModels/import.json` as non-renderable.

The `.map` actor records contain both scenery and other entities, and their
type codes are not yet mapped reliably to model filenames. Native meshes were
placed and visually checked in stages 1-1 through 1-5 and 1-8. The 1-4 hill
meshes use saved variants with their flat floor caps removed. The 1-5 and 1-8
long models sit at the two side edges, leaving the central lane and exit clear.
Their stage-specific prefab variants use a 75-degree view and fit the native
320-unit length to the 14-unit playable map. Both stages have collision
along their side banks, with openings at the side-enemy spawn lane.
The 1-6 palisade already has separate editable pieces. The 4-9 trial produced
a duplicate exit structure and was removed. Do not automatically place every
decoded mesh.

`Validation/PS2StageActorInventory.json` is the starting point for decoding
instance transforms. `Validation/PS2SceneryPlacementReview.md` lists model and
placement counts for all 50 stages. Stage painters for 1-1 through 1-5 and
1-8 reapply the confirmed native placements after rebuilding the editable tilemaps.
