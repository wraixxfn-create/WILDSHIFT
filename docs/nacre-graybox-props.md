# Nacre graybox props: reusable environmental placement

This is the environmental prop pass for the Nacre survey-site blockout in `Assets/_Project/Scenes/Prototype.unity`. Its
goal is to make the region feel less empty **without** cluttering it or costing frame time. It changes no region ID, no
save data, and no player movement code. The layout it decorates — boundaries, routes, landmark, crevices, and traversal
rules — is documented in [`nacre-graybox-region.md`](nacre-graybox-region.md) and is unchanged apart from the group
renaming described below.

Coordinates are **world** positions. The Nacre root sits at world (60, 0, 0), so a prop's local coordinate is its world
coordinate minus 60 on x.

## What was reviewed before anything was created

Nothing new was added until the existing assets were inventoried:

| Existing asset | Reused here? | Notes |
| --- | --- | --- |
| `NacreRockBlock.prefab` | Left alone | A unit cube whose instances set their own scale. Reusing it for props would have kept scale-and-position on every instance, which is what this pass is trying to stop doing. |
| `NacreSurveyStake.prefab` | **Yes, reused as-is** | Route markers are stakes. No new prefab was made for them. |
| 10 `Nacre*` palette materials | 9 reused | `NacreRock`, `NacreSurveyStructure`, and `NacreSurveyAccent` carry the new props. One material was added (below). |
| 19 existing prefab instances | Unchanged | All of them still resolve, and none was re-parented except the two cargo crates (below). |
| URP Lit shader | Reused | The new material points at the same built-in URP Lit shader the palette already uses. |

No asset-store package, external download, or third-party asset was used. Every mesh is the Unity built-in cube
(`fileID: 10202`), which the existing graybox already uses throughout.

## Prop groups

The prototype props were already loose in the region hierarchy. They are now in three labelled groups, and two new
groups were added for the props this pass placed:

| Group (hierarchy) | Contents | Change |
| --- | --- | --- |
| `NACRE_08_Props_Alien_Rock_Formations` | The existing central rock block and 3 boulders, plus 11 new rock slabs and shards (15 objects). | **Renamed** from `NACRE_08_Obstacles_and_Boulders`. Contents and transforms unchanged. |
| `NACRE_10_Props_Ground_Details` | 8 shell plates and 9 pebbles (17 objects). | **New group.** |
| `NACRE_11_Props_Survey_Equipment` | The 2 existing cargo crates, plus 3 survey cases and 5 route markers (10 objects). | **New group.** The crates were moved here from group 08; only their parent changed, not their transform. |

Groups 00–07 and 09 are untouched, so `NACRE_09_Region_Lookup` still holds the locator and its volume, and the region
footprint is unchanged.

## New prefabs

Five prefabs were added to `Assets/_Project/Prefabs/Nacre/`. Each one bakes in the values that should be maintained
centrally — the material, the shadow setting, the collider, and a default size that already sits correctly on a surface —
so an instance only overrides its position, Y rotation, and (where the composition needs it) its scale.

| Prefab | Default size (m) | Material | Collider | Casts shadows | Instances |
| --- | --- | --- | --- | --- | --- |
| `NacreRockSlab` | 2.2 × 0.9 × 1.5 | `NacreRock` | Box | Yes | 6 |
| `NacreRockShard` | 0.8 × 2.6 × 0.8, tilted 6° on Z | `NacreRock` | Box | Yes | 5 |
| `NacreShellPlate` | 1.2 × 0.08 × 0.9 | `NacreShellShard` (new) | **None** | No | 8 |
| `NacrePebble` | 0.45 × 0.22 × 0.4 | `NacreRock` | **None** | No | 9 |
| `NacreSurveyCase` | 0.9 × 0.45 × 0.6 | `NacreSurveyStructure` | Box | Yes | 3 |
| `NacreSurveyStake` (existing) | 0.25 × 1.0 × 0.25 | `NacreSurveyAccent` | Box | Yes | 5 new (14 total) |

`NacreShellShard` is the one new material: a pale nacreous grey (0.76, 0.78, 0.80) with smoothness 0.4, so the shell
plates read as alien biogenic debris rather than as rock or as human equipment. It is a URP Lit material with a single
base colour, matching the existing palette convention. The region now uses **11** materials in total.

### Default transforms and the seating convention

Each prefab's default local position is chosen so that **an instance placed at the surface height sits correctly**:

- `NacreRockSlab` and `NacreSurveyCase` rest with their base exactly on the surface (`y` = half height).
- `NacreRockShard` is tilted 6° about Z. Its default `y` puts its lowest corner 0.05 m **below** the surface, so the
  lean looks planted instead of balanced on an edge. Because of the tilt, a 2.6 m shard is 2.67 m tall and its footprint
  is about 0.27 m wider than its scale suggests. Both are accounted for in the placement rules below.
- `NacreShellPlate` and `NacrePebble` default to 0.01 m **above** the surface. They have no collider, so the lift only
  prevents z-fighting with the ground.

An instance overrides `m_LocalPosition.y` with `surface height + (default y − 0)`, so a slab on the plateau is at
y = 1.5 + 0.45 = 1.95, and a shard on the plateau is at y = 1.5 + 1.2847 = 2.7847.

## Collision policy

Collision is added only where it serves traversal:

- **Colliders (19 props):** rock slabs, rock shards, survey cases, and route markers. Each is a single `BoxCollider`,
  sized to the mesh. No convex hull, no mesh collider, no compound collider, and no rigidbody anywhere.
- **No collider (17 props):** shell plates and pebbles. They are ground detail. A 0.08 m plate or a 0.2 m pebble with a
  collider would be a lip under the controller's 0.3 m step offset — exactly the kind of thing that snags a
  CharacterController — so they are deliberately collider-free.
- **Everything above the step offset.** The survey case was authored at 0.28 m and then raised to **0.45 m**, because at
  0.28 m its collider was under the 0.3 m step offset and the player would have stepped onto it. At 0.45 m it is a low
  obstacle you walk around, which is what the prop is meant to read as.
- **Shadow casting follows collision.** The 19 collider props cast shadows; the 17 decorative props do not, matching the
  existing convention in [`lighting-baseline.md`](lighting-baseline.md) where flat ground meshes receive but do not cast.

## Placement rules used

Every prop was placed against these rules, and the optional audit tool below checks the same ones.

1. **Reinforce the routes, never obscure them.** A prop with a collider must stay outside a route *corridor*: the route
   widened by the 0.5 m capsule radius plus a working margin. A decorative prop may come closer, but must not cover a
   painted route *strip*, because the strips are the navigation cue.
2. **Thin markers sit at the route edge, not across it.** A stake is 0.25 m wide, so it is allowed inside a corridor,
   but its centre must stay at least 1.2 m (capsule radius + 0.7 m) from a route centre line.
3. **Keep the spawn clear.** Nothing within 3 m of the spawn at world (47.5, 1.05, −14).
6. **Leave the landmark visible.** The sightline from the spawn eye (1.7 m) to the spire top must stay clear. Four props
   come close to that line horizontally, but the ray climbs steeply, so all four stay well below it:
   `Rock_Shard_Plateau_Arrival` (0.36 m from the ray, top 4.12 m against a ray height of 10.75 m),
   `Shell_Plate_Plateau_Survey` (0.96 m, top 1.59 m against 11.27 m), `Route_Marker_RouteStart` (1.49 m, top 1.00 m
   against 3.18 m), and `Survey_Case_Route_Start` (1.77 m, top 0.45 m against 3.88 m).
5. **No new crevices.** Two solid props keep at least 1.0 m between them, so the capsule cannot wedge between them.
7. **Sit on the surface.** A prop's base must be within 0.15 m above, or 0.35 m below, the surface under its centre.
   Shards use the deliberate 0.05 m sink.
8. **Stay inside the region.** Every prop is inside the 40 × 40 m footprint and inside the region volume, so the region
   lookup is unaffected.

### Where the props went, and why

| Prop | World position | Why it is there |
| --- | --- | --- |
| `Rock_Slab_SouthLeg_North` | (64.4, 0.0, −10.0) | North verge of the main south leg; frames the route without touching it. |
| `Rock_Slab_SouthLeg_South` | (58.0, 0.0, −16.0) | South verge of the same leg, so the route reads as a corridor between rock. |
| `Rock_Slab_GullyMouth` | (45.3, 0.0, −9.4) | Marks the entrance to the optional gully, opposite the gully's east wall. |
| `Rock_Slab_WestCentral` | (54.5, 0.0, 8.0) | Fills the empty west-central lowland between the gully and the terrace. |
| `Rock_Slab_Lowland_NorthWest` | (53.5, 0.0, 15.5) | Leads the eye from the lowland up to the overlook. |
| `Rock_Slab_Plateau_East` | (73.5, 1.5, 8.0) | East side of the plateau route, on the way to the landmark. |
| `Rock_Shard_Plateau_Arrival` | (65.4, 1.5, 6.9) | Vertical accent at the top of the main ramp: the arrival moment. |
| `Rock_Shard_Plateau_East` | (75.8, 1.5, 11.0) | Sits beside the landmark approach without blocking the sightline. |
| `Rock_Shard_Plateau_NorthWest` | (63.0, 1.5, 15.5) | Balances the survey hut on the plateau's north-west. |
| `Rock_Shard_Overlook_West` | (44.5, 0.25, 15.5) | The overlook's reward: a tall shard beside the marker stake. |
| `Rock_Shard_Overlook_South` | (45.6, 0.25, 12.6) | Second shard, so the overlook is not a single object on a slab. |
| 8 shell plates | see the group in the scene | Nacreous ground debris spread across the lowland, the plateau, and the disturbed plot. |
| 9 pebbles | see the group in the scene | Clustered at the bases of the central rock and of three slabs, so the rocks look bedded in rubble. |
| `Survey_Case_Route_Start` | (53.2, 0.0, −10.2) | Equipment dropped where the main route begins. |
| `Survey_Case_Plateau_Staged` | (63.2, 1.5, 9.0) | Staged gear near the survey installation, off the route. |
| `Survey_Case_Plot_Sample` | (74.4, 0.06, −10.8) | Sampling case inside the disturbed ecology plot. |
| 5 route markers | see the group in the scene | Stakes at the route start, the south-to-north turn, the ramp foot, the ramp top, and the gully mouth. |

The route markers are the props that most directly reinforce the main and optional routes: they punctuate the four
decision points (start, turn, ramp foot, ramp top) plus the gully entrance, reusing the stake prefab the disturbed plot
already established.

## Optional editor tool

Placement can be done entirely by hand in the Hierarchy; the tool is not required and nothing in a build depends on it.

- **Assembly:** `Assets/_Project/Scripts/Editor/Wildshift.Editor.asmdef`, editor-only, `autoReferenced: false`.
- **Files:** `Environment/NacrePropPlacementAuditor.cs` (the rules, pure logic) and
  `Environment/NacrePropPlacementWindow.cs` (the window).
- **Open it:** `Window → WILDSHIFT → Nacre Prop Placement`.
- **Use it:** select `NACRE_GRAYBOX_REGION_ROOT` (or any `NACRE_*_Props_*` group, or a single prop — the window walks up
  to the region root) and press **Audit Placement**. **Frame First Error** moves the Scene view to the first failing prop.

It is **read-only**: it never moves, creates, renames, or destroys anything, and it does not write to the scene.

A prop, for this tool, is any renderer that is a direct child of a group whose name contains `_Props_`. Everything else
under the region root is treated as layout the props must respect.

**Errors** (the layout blocks or hides a route):

- a collider prop inside a route corridor, or inside the 3 m spawn clearance;
- a thin marker within 1.2 m of a route centre line;
- a decorative prop covering a painted route strip;
- a collider under the 0.3 m step offset (it would read as a lip);
- two collider props that intersect, or a collider prop that intersects the existing layout;
- a prop floating more than 0.15 m above, or sunk more than 0.35 m below, the surface under it.

**Warnings** (style and budget):

- a decorative prefab carrying a collider;
- a prop whose name does not describe its prefab;
- more than 20 props in one group;
- two solid props closer than 1.0 m, which can leave a crevice the capsule snags on.

### Known tool limitations

- **Spacing is measured between props only.** The crevices in the pre-existing layout are already listed in the crevice
  table in [`nacre-graybox-region.md`](nacre-graybox-region.md), so the tool does not re-report a prop against a boundary
  ridge or a gully wall.
- **Two warnings are expected on this layout.** `Cargo_Crate_A` and `Cargo_Crate_B` are 0.30 m apart. That crevice is
  pre-existing and documented in the region doc; it is reported because both crates now live in a prop group. It is not a
  defect introduced by this pass.
- **Bounds are axis-aligned.** A rotated prop is judged by the footprint it actually occupies, which is conservative:
  the tool can report a conflict that a tighter oriented-box test would allow.
- **It is not a traversal test.** It cannot tell you whether a route is still walkable end to end. Walk the routes in
  Play Mode; see the verification checklist below.
- **The corridor and strip rectangles are hardcoded** from the routes in the region doc. If a route moves, update
  `RouteCorridors` and `RouteStrips` in the auditor.

## Performance

The pass is deliberately small:

| Measure | Before | After |
| --- | --- | --- |
| Renderers in the Nacre region | 45 | **81** (+36) |
| Box colliders in the Nacre region | 45 | **64** (+19) |
| Materials used in the region | 10 | **11** (+1) |
| Distinct prop meshes | 1 (built-in cube) | 1 (built-in cube) |
| Shadow casters added | — | 19 (the 17 decorative props do not cast) |
| New scripts in a build | — | **0** (the tool is editor-only) |
| New runtime components | — | **0** |

The "before" collider count here is 45, while [`nacre-graybox-region.md`](nacre-graybox-region.md) reports 46 for its own
pass. The two harnesses filter the footprint differently. Nothing was removed to produce the difference: diffing the two
scene revisions collider by collider shows **19 added, 0 removed**, so the prop pass is purely additive.

Every prop is a single built-in cube with one material, so all 36 share one mesh and 11 materials across the region. No
LOD group, no occlusion volume, no light probe, no reflection probe, no particle system, no animation, and no script was
added. There is no per-frame cost at all: the props are static scene data.

The 17 decorative props do not cast shadows, which keeps them out of the shadow pass — the main GPU cost in this scene
per [`lighting-baseline.md`](lighting-baseline.md).

## Restrictions honoured

- No vegetation simulation, no procedural foliage system, no destruction, no physics-driven debris. Nothing has a
  rigidbody, and nothing moves at runtime.
- No unlicensed or asset-store asset, and no new package. `Packages/manifest.json` is unchanged.
- No region ID, save data, or player movement change. `RegionDef_NacreSurveySite.asset`, the catalogs, the locator, the
  volume, and every file under `Scripts/Runtime/` are byte-identical.

## Verification

### Done in the agent environment (no Unity Editor available)

Unity is not installed here, so these checks were run against the generated files and a geometric proxy built from the
scene, not in Play Mode. The proxy extracts every box collider in the Nacre footprint with its world transform, resolves
prefab-instance overrides, and models the capsule (radius 0.5 m, height 2 m, step offset 0.3 m, no jump) on a 0.25 m grid.

1. **Scene integrity.** All 438 YAML documents parse. Every in-file `fileID` resolves, with no dangling local reference.
   Every project GUID resolves, including the five new prefab GUIDs and the new material GUID. The only external GUID is
   the built-in URP Lit shader, which the existing palette already references.
2. **Prefabs.** All 55 prefab instances point at real prefab assets, and all of their overrides target objects that
   exist. The five new prefabs are structurally identical to `NacreRockBlock.prefab`, which Unity has already accepted.
3. **Serialized hierarchy.** Every `m_Children` entry in the scene resolves to a transform that points back at the same
   parent, and every child's parent reference appears in that parent's `m_Children` — 0 mismatches across 64 plain
   transforms and 55 prefab instances. Group 08 lists 15 children, group 10 lists 17, group 11 lists 10, and the region
   root lists 12.
4. **Props present.** All 36 authored props are in the scene, each parented to the intended group.
5. **Seating.** Every prop's base is within tolerance of the surface under its centre. No prop floats and none is buried.
   The five shards sit 0.05 m into their surface, as designed.
6. **Placement rules.** All 36 props pass every rule above. The smallest gap between two solid props is 1.07 m
   (`Rock_Slab_GullyMouth` to `Route_Marker_GullyMouth`), above the 1.0 m minimum. No decorative prop covers a route
   strip.
7. **No intersections.** No prop intersects any existing object, and no two props intersect.
8. **Traversal.** On the capsule proxy, all 28 route and landmark checkpoints remain reachable from the spawn: the south
   leg, the north leg, the ramp foot, the ramp, the ramp top, both plateau legs, the spire plaza, the plinth's north
   band, the survey plateau, the terrace, the gully, the overlook ramp, the overlook ledge, the marker, the disturbed
   plot, and the open lowland. Reachable-cell count moved from 19,293 to 18,915 — a 2.0 % reduction, which is the area
   the new obstacles occupy, not a lost route.
9. **Narrowest passage.** The gully still passes at the capsule radius with 3.25 m of centre travel (x 47.12 to 50.38 at
   z = 0, unchanged at z = 4 and z = −6). It was 3.5 m of clear width before; the props sit outside it.
10. **Landmark sightline.** The line from the spawn eye to the spire top is clear. Of the four props near that line, the
   smallest vertical margin is 2.18 m (`Route_Marker_RouteStart`, top 1.00 m against a ray height of 3.18 m).
11. **Region lookup.** The volume's half-open box test still returns inside for the spawn, the start pad, the spire top,
    and every new prop, and outside for the origin movement course. The volume and its definition were not touched.
12. **Audit tool.** A Python mirror of the auditor's rules, run over the patched scene, reports **0 errors and 2
    warnings** — the two expected, pre-existing cargo-crate crevice warnings documented above.

This is a proxy. It does not model the CharacterController's skin width, ground probing, sliding, or camera collision.

### To run in the Unity Editor (open `Prototype`)

1. Open `Prototype.unity`. The Console should show no missing-script, missing-prefab, or missing-material warnings, and
   no warning about the new `Wildshift.Editor` assembly.
2. Check the Project window shows five new prefabs in `Assets/_Project/Prefabs/Nacre/` and `NacreShellShard` in
   `Assets/_Project/Materials/Nacre/`.
3. In the Hierarchy, expand `NACRE_GRAYBOX_REGION_ROOT`. Confirm groups 08, 10, and 11 are named as above and hold 15,
   17, and 10 children respectively, and that the boulders and cargo crates are still where they were. Every prop should
   appear nested under its group — none should be at the scene root or show as missing.
4. Enter Play Mode from the spawn. **Walk the main route**: south leg east, turn north, up the ramp, across the plateau
   to the spire plaza. Confirm no prop blocks you, no prop snags the capsule, and the route strips stay visible.
5. **Walk the optional route**: north through the gully to the overlook, then back east. Confirm the gully does not feel
   pinched and the camera does not clip the new shards.
6. Check the landmark is still visible from the spawn, and that the two shards on the plateau do not hide it.
7. Walk into each new rock slab, shard, survey case, and route marker, and confirm the capsule stops. Walk **through**
   each shell plate and pebble, and confirm nothing resists.
8. Step onto a survey case and confirm you cannot — it is 0.45 m, above the 0.3 m step offset.
9. Open `Window → WILDSHIFT → Nacre Prop Placement`, select the region root, and press **Audit Placement**. Expect
   0 errors and the 2 documented cargo-crate warnings.
10. Watch the frame rate and the Frame Debugger on the same camera path as the lighting baseline. Draw calls should rise
    by roughly the 36 new renderers, with no change to the shadow pass beyond 19 casters.
11. **Confirm the prefabs are reusable:** select `NacreRockSlab.prefab` in the Project window and change its material or
    its default scale. Every slab instance in the scene should update, and none should break. Revert the change.

## Known limitations

- **Not yet run in Unity.** The scene, prefabs, material, and editor scripts were written as text and checked as
  described above. Open the scene once in the Editor and run the checklist. Unity may re-serialise the files on first
  save.
- **The editor scripts are not compiled here.** No C# compiler is available in this sandbox. They were reviewed by hand
  and checked for balanced structure; expect to fix at most a small compile error on first open.
- **Graybox only.** The props are coloured cubes. They read as mass and silhouette, not as rock, shell, or equipment.
- **Hardcoded corridors.** The audit tool's route rectangles are copied from the region doc. If a route moves, update
  both.
- **Static composition.** No prop is instanced on the GPU, batched, or combined. At 36 extra draw calls this is fine;
  revisit it if the prop count grows by an order of magnitude.
- **Two crates are still 0.30 m apart.** That crevice predates this pass and is documented in the region doc. Moving the
  crates would change the existing layout, which this pass avoids.
- **No props on the movement course.** The origin `GBX_*` test course is unchanged.
