# Nacre graybox region: first survey site

This is the first playable blockout of Nacre, built in `Assets/_Project/Scenes/Prototype.unity`. It is a remote frontier
survey site: a safe landing pad, one main route with a ramp onto a plateau, an optional gully route, a violet landmark
that is visible from the start, a human-made survey installation, and a disturbed ecology plot. It is **graybox**: primitives
and flat palette colours only, with no final art, enemies, missions, dialogue, or hazards. Its baseline lighting (sun,
ambient, background, shadow casting) is set in the scene and documented in [`lighting-baseline.md`](lighting-baseline.md).

Region identity: `nacre/frontier/survey-site`. The region system it uses is documented in [`world-regions.md`](world-regions.md).

## Where it is in the scene

The area is an offset block centred at world **(60, 0, 0)**, so it does not overlap the older movement course at the origin
([`graybox-movement-test.md`](graybox-movement-test.md)). Every Nacre object is a child of `NACRE_GRAYBOX_REGION_ROOT`,
split into labelled groups:

| Group (hierarchy) | Contents |
| --- | --- |
| `NACRE_00_Ground_and_Boundary` | Ground slab (40 × 40 m) and four 4 m perimeter ridges. |
| `NACRE_01_Start_Safe_Zone` | Start landing pad and orange beacon. |
| `NACRE_02_Main_Route_Path` | Pale route strips on the ground and plateau. |
| `NACRE_03_Elevation_Ramp_and_Plateau` | Main ramp, 1.5 m plateau, 0.25 m low terrace step. |
| `NACRE_04_Landmark_Spire` | Violet monolith on a plinth (the orientation landmark). |
| `NACRE_05_Survey_Installation` | Hut, mast, overhead solar panel, supply crate. |
| `NACRE_06_Optional_Gully_Route` | Gully path, two gully walls, overlook ramp, overlook ledge, marker stake. |
| `NACRE_07_Disturbed_Ecology_Site` | Dark soil plot, spoil heap, sample tray, perimeter stakes. |
| `NACRE_08_Props_Alien_Rock_Formations` | Central rock block, three boulders, and the alien rock props (slabs and shards). Renamed from `NACRE_08_Obstacles_and_Boulders`; the cargo crates moved to `NACRE_11`. |
| `NACRE_09_Region_Lookup` | `WorldRegionLocator_Nacre` with its `WorldRegionVolume_SurveySite` child. |
| `NACRE_10_Props_Ground_Details` | Shell plates and pebbles. Decorative: no colliders, no shadow casting. |
| `NACRE_11_Props_Survey_Equipment` | Two cargo crates, three survey cases, and five route marker stakes. |

Groups 08, 10, and 11 hold the environmental props and are documented in
[`nacre-graybox-props.md`](nacre-graybox-props.md). Groups 00 to 07 and 09 are unchanged.

Coordinates below are **local** to the Nacre root: x runs west to east, z runs south to north, and y is height. The
world position of a local point is local + (60, 0, 0).

## What the player sees and does

- **Start (safe point).** A mint landing pad in the south-west, marked by an orange beacon. The player spawns at world
  `(47.5, 1.05, -14)` (local `(-12.5, -14)`), facing north. The interaction validation cube, `INTERACTION_TEST_Target`, sits
  2 m in front of the spawn at world `(47.5, 1, -12)`.
- **Main route.** Pale sand strips lead east along the south leg (local z −14 to −12) to the north leg (x 7 to 9). The
  route reaches the 4 m-wide main ramp (x 6 to 10), climbs 1.5 m at about 14°, and continues across the plateau
  to the spire plaza. Walking distance from the start to the spire plinth is about 57 m.
- **Optional route.** A darker lilac gully runs north from the start area between the west rock mass and a 1 m
  gully wall. It is about 3.5 m wide. It leads to a raised overlook in the north-west, reached by a short ramp, with a
  marker stake. It then loops back east to the central ground. Walking distance from the start to the overlook is about 31 m.
- **Landmark.** A 12 m violet monolith with a cap on a plinth in the north-east of the plateau. Its top is at local
  height 14.4 m. A line of sight from the start is clear (checked; see Verification).
- **Survey installation.** On the plateau's west side: a 2.2 m hut, a 4 m mast with a crossbar, an overhead tilted
  solar panel above head height, and a 1 m supply crate. It is set back from the route, so it does not block it.
- **Disturbed ecology site.** An east-lowland plot of dark soil (6 × 14 m, flush with the ground), with a 0.9 m spoil heap,
  a low sample tray, and eight orange perimeter stakes. The plot is walkable around the heap and stakes. It has no
  gameplay effect yet; it exists so later systems can reference it.
- **Other obstacles.** A central 2.5 m rock block, three boulders, and two cargo crates. None sits on the main route.

## Map (1 m cells, north at top)

```
  19.5 |########################################|
  18.5 |########################################|
  17.5 |##llllllll............PPPPPPPPPPPPPPPP##|
  16.5 |##lllll#ll............PPPPPPPPPLLLLPPP##|
  15.5 |##llllllll............PPPPPPPPPLLLLPPP##|
  14.5 |##llllllll.....###....PPPPPPPPPLLLLPPP##|
  13.5 |##llllllll.....###....PPPPPPPrrLLLLPPP##|
  12.5 |##llllllll............####PrrrrPPPPPPP##|
  11.5 |##llllllll............###PPrrPPPPPPPPP##|
  10.5 |##lllll///............#####rrPPPPPPPPP##|
   9.5 |##.....///............PPP#PrrPPPPPPPPP##|
   8.5 |#######oooo...........PPPPPrrPPPPPPPPP##|
   7.5 |#######oooo...........PPPPPrrPPPPPPPPP##|
   6.5 |#######oooo...........PPPPPrrPPPPPPPPP##|
   5.5 |#######ooo##..............////........##|
   4.5 |#######ooo##..............////........##|
   3.5 |#######ooo##.###..........////........##|
   2.5 |#######ooo##.###..........////........##|
   1.5 |#######ooo##..............////........##|
   0.5 |#######ooo##..............////........##|
  -0.5 |#######ooo##..........................##|
  -1.5 |#######ooo##...............rr.........##|
  -2.5 |#######ooo##...............rr.........##|
  -3.5 |#######ooo##...............rr...#x#xx###|
  -4.5 |#######ooo##..#######......rr...xxxxxx##|
  -5.5 |#######ooo##..#######......rr...xx###x##|
  -6.5 |#######ooo##..#######......rr...xx###x##|
  -7.5 |#######ooo##..#######......rr...xxxxxx##|
  -8.5 |##........##..#######......rr...xxxxxx##|
  -9.5 |##.............######......rr...xxxxxx##|
 -10.5 |##.............lll.........rr...#xxxx###|
 -11.5 |##..SS##SSS................rr...xxxxxx##|
 -12.5 |##..SS##SSSrrrrrrrrrrrrrrrrrr...xxxxxx##|
 -13.5 |##..SSSSSSSrrrrrrrrrrrrrrrrrr...xxxxxx##|
 -14.5 |##..SSSSSSS.....................xxxxxx##|
 -15.5 |##..SSSSSSS.....................xxxxxx##|
 -16.5 |##..#SSSSSS...........###.......#x#xx###|
 -17.5 |##...........##.......###.............##|
 -18.5 |########################################|
 -19.5 |########################################|
legend: # solid  L landmark  / ramp  P plateau  l raised ledge/terrace  r main route  o optional route  S start pad  x disturbed soil  . ground
```

Legend: `#` not walkable (a solid object, plus the 0.5 m player clearance around it), `L` landmark, `/` ramp, `P` plateau
(1.5 m), `l` raised ledge or terrace (0.25 m), `r` main route, `o` optional route, `S` start pad, `x` disturbed soil,
`.` open ground. It is a rough plan view at 1 m resolution.

## Movement rules this layout obeys

The player controller has **no jump**. Every change in height is therefore either a ramp or a step under the controller's
0.3 m step offset:

- Main ramp: 1.5 m rise over 6 m (about 14°). Overlook ramp: 0.25 m rise over 1.5 m (about 9.5°). Both are well under the 45° slope limit.
- Steps: the low terrace (0.25 m), the overlook ledge (0.25 m), the route strips (0.08 m), the start pad (0.04 m), the
  disturbed soil (0.06 m), and the sample tray (0.15 m). All are under 0.3 m.
- Plateau edges are 1.5 m tall, so the plateau is reached only by the main ramp. Its other edges are cliffs.
- The perimeter ridges are 4 m high, so there are no gaps and no walkable exits.
- Passages: the gully is about 3.5 m wide, so the 1 m player capsule passes with room to spare, and the camera
  has space to orbit.

## Collision

- Every walkable surface (ground, route strips, pad, soil, ramps, plateau, ledge, terrace, plinth, sample tray) has a
  `BoxCollider`, as does every obstacle.
- Overlay surfaces sit **flush** on the surface beneath them (bottom equals the surface top). No collider is embedded
  in another walkable collider, so there are no hidden lips.
- Overhead items (the mast crossbar and the solar panel) are above the 2 m head height, so they never block walking.
- The ramps are rotated boxes whose lower ends sink into the ground slab. The upper ends are flush with the plateau.

## Region setup

- Definition: `Assets/_Project/Data/WorldRegions/RegionDef_NacreSurveySite.asset`. Stable ID `nacre/frontier/survey-site`,
  display name "Nacre Frontier Survey Site".
- Catalog: `Assets/_Project/Data/WorldRegions/NacreWorldRegionCatalog.asset`, which lists that definition. A separate
  catalog keeps the Nacre world apart from the development test catalog `DevWorldRegionCatalog`, which is unchanged.
- Locator: `WorldRegionLocator_Nacre` (under `NACRE_09_Region_Lookup`), with `Catalog` set to the Nacre catalog.
- Volume: `WorldRegionVolume_SurveySite`, one box. Local centre `(0, 7, 0)` and size `(40, 18, 40)` under the Nacre root.
  Its world extent is x 40 to 80, y −2 to 16, and z −20 to 20, which covers the whole footprint and the tallest object
  (landmark top 14.4 m).

A single volume covers the region. More volumes of the same definition can be added later, for example one per
ecology plot, without changing the region's identity.

## Prefabs and materials

Repeated elements are prefabs in `Assets/_Project/Prefabs/Nacre/`:

- `NacreRockBlock.prefab`: rock-material cube with a box collider. Used 10 times: four perimeter ridges, two gully walls,
  the central block, and three boulders. Each instance sets its own size and position.
- `NacreSurveyStake.prefab`: orange accent cube with a box collider. Used 14 times: eight plot stakes, one overlook
  marker, and five route markers added by the prop pass.
- Five prop prefabs — `NacreRockSlab`, `NacreRockShard`, `NacreShellPlate`, `NacrePebble`, and `NacreSurveyCase` — added
  by the prop pass. Each bakes in its material, shadow setting, collider, and a default size that already sits correctly
  on a surface. They are listed in [`nacre-graybox-props.md`](nacre-graybox-props.md).

Eleven palette materials are in `Assets/_Project/Materials/Nacre/`. Each is a URP Lit material with one base colour:

| Material | Used for | Colour |
| --- | --- | --- |
| `NacrePearlGround` | Ground | mid pearl grey (0.46, 0.46, 0.49) |
| `NacreStartPad` | Start pad (safe) | mint grey |
| `NacreRoutePath` | Main route strips | pale sand |
| `NacreOptionalPath` | Optional gully path | dusky lilac |
| `NacrePlateauSurface` | Plateau, ramp, ledge, terrace, plinth | warm mid grey (0.62, 0.60, 0.59) |
| `NacreRock` | Ridges, rocks, panel | slate teal |
| `NacreLandmark` | Spire | pearl violet |
| `NacreSurveyStructure` | Hut, mast, crates, sample tray | off-white |
| `NacreSurveyAccent` | Beacon, stakes, crossbar | survey orange |
| `NacreDisturbedSoil` | Soil plot, spoil heap | dark brown-grey |
| `NacreShellShard` | Shell plate props | pale nacreous grey (0.76, 0.78, 0.80), smoothness 0.4 |
| `PlayerPlaceholder` | Player capsule (`Assets/_Project/Materials/`) | near-black charcoal |

The interaction validation cube `INTERACTION_TEST_Target` uses `NacreSurveyAccent` (orange), so it stands out from the
ground. The movement course at the origin keeps `DevelopmentNeutral` and `GrayboxWall`/`GrayboxObstacle`.

## Editing the layout

- Move or resize any object in its group. Keep the start, the route strips, and the ramp aligned. The route strips mark the
  intended path, so move them with the ground they sit on.
- To change all stakes or rock blocks at once, edit the prefab. Individual instances can keep overrides for size and position.
- Keep the region volume covering the footprint. An object outside it still renders, but it is not in the region.
- After any change to a region ID or the locator, select `WorldRegionLocator_Nacre` and run **Validate Region Setup**.

## Boundaries, spawn, and traversal rules

This section records the playable footprint, the spawn clearance, each intended route, the tight spots, and the
height rules that the boundaries and routes depend on. No geometry was changed for this pass: the existing layout
already satisfies the controller's limits (see *Verification* below). Coordinates are **world** coordinates unless
marked local.

### Footprint and perimeter

- **Ground slab:** `Ground_Lowland`, 40 × 40 m, world x 40 to 80 and z −20 to 20.
- **Perimeter:** four `NacreRockBlock` prefab instances, 4 m tall and 1.5 m thick, sitting on the slab edges. Each one
  is named in the hierarchy under `NACRE_00_Ground_and_Boundary`:

  | Object | World extent (x, z) | Inner face |
  | --- | --- | --- |
  | `Boundary_North` | x 40 to 80, z 18.5 to 20 | z 18.5 |
  | `Boundary_South` | x 40 to 80, z −20 to −18.5 | z −18.5 |
  | `Boundary_East` | x 78.5 to 80, z −18.5 to 18.5 | x 78.5 |
  | `Boundary_West` | x 40 to 41.5, z −18.5 to 18.5 | x 41.5 |

- **Intended playable footprint:** the interior of the ridges, world x 41.5 to 78.5 and z −18.5 to 18.5 (local ±18.5).
  A capsule centre can get to about 0.5 m from each inner face, so the reachable area is about x 42 to 78 and z −18 to 18.
- The four ridges meet at the corners with no gap, and the ground slab is continuous under them. There is no reachable
  path to the grid edge in the proxy check (see *Verification*), and no route leaves the footprint.
- The ridges are **solid rock**, not invisible walls. Nothing invisible is added at the perimeter.
- The optional gully, the central rock, and the boulders are natural barriers that sit inside the footprint.

### Plateau and cliffs

- The plateau (`Plateau_NorthEast`) is 1.5 m above the ground. Its west edge (x 62) and south edge (z 6) are
  **cliffs** of 1.5 m. Its north and east edges meet the perimeter ridges. A 1.5 m step is not climbable without a jump,
  so the only way up is the main ramp.
- Falling off a cliff is intentional and survivable. The longest drop in the footprint is 1.5 m, onto the ground.

### Spawn

- **Spawn:** world `(47.5, 1.05, −14)`, facing north. The capsule's bottom sits at y 0.05, just above the pad top of
  0.04, so it settles on the pad.
- **Floor:** `Start_Pad` is flat (0.04 m above the slab), 7 × 6 m, world x 44 to 51 and z −17 to −11. The spawn is
  3 m from the south, north, and east edges of the pad, and 3.5 m from the west edge.
- **Nearest solids:** `INTERACTION_TEST_Target` at 1.5 m (its near face is 1.5 m from the spawn centre), `Start_Beacon`
  at about 3.3 m, `Boundary_South` at 4.6 m, and `Boundary_West` at 6.1 m. None of these is within the capsule radius.
- **Known scaffolding:** the interaction test cube sits in front of the spawn, so the straight line north is blocked
  for about 1 m. The player must step left or right to reach the gully. It is validation scaffolding and can move later.

### Intended routes

Each route below was checked in the proxy (see *Verification*). Checkpoints are world coordinates.

| Route | Expected traversal | Checkpoints (proxy) |
| --- | --- | --- |
| **R1, main route** (primary) | Pale strips east along the south leg, north along the north leg, up the 4 m-wide main ramp (14°, 1.5 m rise), then across the plateau to the spire plinth. | Spawn → (60, −13) → (68, −6) → ramp base (68, 0.5) → ramp top (68, 5.8) → plateau (68, 9) → (70, 13) → plinth north strip (73, 17). |
| **R2, optional gully** | From the start pad north into the lilac gully (3.5 m wide between the west rock and the 1 m gully wall), then up the overlook ramp (about 9°, 0.25 m rise) to the overlook ledge and marker stake. | Spawn → gully mouth (48.75, −8.5) → gully centre (48.75, 0) → overlook ramp (48.75, 9.5) → overlook (48.75, 17). |
| **R2b, overlook by the ledge** | The 0.25 m overlook ledge can also be stepped onto from the open ground to the east. This is intended and within the 0.3 m step offset. | Overlook reachable with its ramp removed. |
| **R3, east lowland** | Across the terrace step (0.25 m) and onto the disturbed plot. The plot is walkable soil, but the stakes and spoil heap are solid. | Terrace (56.5, −10.3) → plot (76, −12) → spoil-heap side (77.5, −6). |
| **R4, survey plateau** | On the plateau, past the hut, supply crate, and mast. The installation is set back from the route. | (65, 14.5) and (65, 16). |

Dead ends. The gully, the overlook, and the east plot all connect back to the central ground, so no route is a
dead end. The plateau has no other entrance: without `Main_Ramp`, the plateau cells in the proxy are unreachable.

### Elevation and steps

| Feature | Rise | Slope or form | Entry |
| --- | --- | --- | --- |
| Main ramp | 1.5 m over 6.2 m | ~14° | Walkable. The only way onto the plateau. |
| Overlook ramp | 0.25 m over 1.5 m | ~9° | Walkable. The ledge is also steppable. |
| Overlook ledge | 0.25 m | Step | Within 0.3 m step offset. |
| Low terrace | 0.25 m | Step | Within 0.3 m step offset. |
| Route strips, pad, soil, tray | 0.04 to 0.15 m | Step | Within 0.3 m step offset. |
| Spire plinth | 0.2 m above the plateau | Step | Walkable, but see *Spire plinth* below. |
| Plateau cliffs | 1.5 m | Vertical | Not climbable. Falling is allowed. |

The largest rise a player can take without a ramp is 0.3 m. The largest height change between two adjacent standable
cells in the proxy is 0.25 m (the overlook ledge). No surface between the spawn and the spire is steeper than 14°.

### Narrow passages and crevices

- **Narrowest route passage:** the gully, about 3.5 m wide between the faces of its walls (3.6 m clear in the proxy). The capsule needs 1.0 m.
  The gap between the central rock and the east gully wall is the same width.
- **Crevices that are not routes.** Each of these is too narrow for the capsule (1.0 m), so it is a dead end. The
  player will not enter it, and no route should be routed through it:

  | Crevice | Width | Location (world) |
  | --- | --- | --- |
  | `Cargo_Crate_A` to `Cargo_Crate_B` | 0.3 m | x 54.6 to 54.9, z 3.1 to 3.6 |
  | `Survey_Hut` to `Survey_Supply_Crate` | 0.5 m | x 64.8 to 65.3 |
  | `Boulder_South_West` to `Boundary_South` | 0.4 m | x 53.2 to 54.8, z −18.5 to −18.1 |
  | `Boulder_South_East` to `Boundary_South` | 0.6 m | x 62.4 to 64.6, z −18.5 to −17.9 |
  | Disturbed-plot east strip (soil, with stakes) to `Boundary_East` | 0.5 m | x 78.0 to 78.5, z −17 to −3 |

- **Spire plinth.** The monolith is 3 m wide, and the plinth is 4 m wide. That leaves a 0.5 m band, where the plinth can
  be stood on, at its north edge (z 17.0 to 17.5). The west, south, and east sides are too narrow for the capsule.
  The plinth is a landmark base, not a platform. The 1.0 m strip between the plinth top and `Boundary_North` is
  walkable only as an edge, because it has no clearance to spare.

### Falls and out-of-bounds

- No invisible kill plane or catch volume is added. The closed perimeter and continuous ground mean the player can
  only fall from the plateau cliffs, onto the slab.
- **Known gap:** nothing recovers a player who is placed outside the footprint, for example by a future save restore or a
  debug teleport. Gravity has no terminal velocity, so such a player falls indefinitely. The save format stores a
  position, but no code applies it to the player yet. This should be fixed when positions are restored (see
  *Known limitations*).

### Editing the boundaries

- Each ridge is a named prefab instance. Select `Boundary_North`, `Boundary_South`, `Boundary_East`, or
  `Boundary_West` and check the Transform and BoxCollider. Keep the inner faces at local ±18.5.
- To check the footprint in Scene view, select `Ground_Lowland` (40 × 40 m) and compare it with the ridges.
- To check traversal, enter Play Mode from the spawn and follow the routes above. The crevices are listed by name
  so they can be found quickly.

### Verification for this pass

**No Unity Editor is available in this sandbox, so no Play Mode test has been run.** The results below come from a
proxy built from the scene file. It extracts the 46 box colliders that touch the Nacre footprint, with their world transforms
(the 16 movement-course objects at the origin are outside it), then models the capsule (radius 0.5 m, height 2 m, step 0.3 m, no jump, unlimited descent) on a 0.1 m grid.

- **Spawn:** the spawn is free and reachable. The nearest solid is 1.5 m away.
- **Routes:** every checkpoint in the routes table is reachable from the spawn. The optional overlook is reachable
  with and without its ramp.
- **Narrowest passage:** the gully is passable at the capsule's radius, with about 1.8 m of clearance on each side of
  its centreline. The crevices in the table above are not passable.
- **Largest elevation change:** the main ramp is walkable. The plateau is unreachable when `Main_Ramp` is removed, so
  the ramp is the only way up. The largest rise between reachable cells is 0.25 m.
- **Outer perimeter:** no reachable cell lies on the grid edge, and the closest reachable cell is about 0.5 m from a ridge
  face. The ridges have no gap.

This is a proxy. It does not model the CharacterController's skin, ground probing, sliding, or camera collision.
Confirm everything below in the Editor.

## Verification

> **Counts below predate the prop pass.** The document and collider counts in this section (326 documents, 19 prefab
> instances, 46 box colliders) were recorded when this region was first blocked out. The scene now holds 438 documents,
> 55 prefab instances, and 64 Nacre box colliders — the prop pass added 19 colliders and removed none. The current
> numbers, and the checks that produced them, are in [`nacre-graybox-props.md`](nacre-graybox-props.md).

### Done in the agent environment (no Unity Editor available)

Unity is not installed here, so the following checks were run on the generated files and the layout data, not in Play Mode:

1. **Scene and asset structure.** Every document in `Prototype.unity` parses, and every in-file `fileID` resolves
   (326 documents, no dangling references). Every project-GUID reference resolves. The only unresolved GUID is the built-in
   URP Lit shader, which the existing `GrayboxWall` material also references. All 19 prefab instances point at real
   prefab objects, and all of their overrides target objects that exist.
2. **Walkability simulation.** On a 0.25 m grid, with a 0.5 m player radius, a 0.3 m step offset, and the ramp and step
   heights above, the start can reach the south leg, the north leg, the ramp, the plateau near the ramp top, the spire
   plaza and plinth, the survey plateau, the terrace, the gully, the overlook, the disturbed plot, and the south-east
   boulder side. The plateau is **not** reachable when the main ramp is removed from the model, so the ramp is the only route up.
3. **Landmark visibility.** A sightline from the start eye height (1.7 m) to the spire top is clear, with no object between
   them in the layout.
4. **Collision overlap.** Checked with axis-aligned bounding boxes. No two non-ground colliders overlap. The only overlaps
   are the ramps' sunken lower ends, which sit inside the ground slab.
5. **Region lookup.** The volume's half-open box test (in the locator's rules) returns inside for the spawn, the start pad,
   the spire top, and the west inner boundary, and outside for the origin movement course, the maximum faces, and any point
   above the volume.

### To run in the Unity Editor (open `Prototype` and check)

1. Open `Prototype.unity`. The Console should show no missing-script, missing-prefab, or missing-reference warnings.
2. Select `WorldRegionLocator_Nacre`, run **Validate Region Setup**, and check that no errors are logged. Select
   `WorldRegionVolume_SurveySite` and confirm the green box covers the footprint.
3. Enter Play Mode from the spawn. Check the start point, then walk the main route: strips, then the ramp, then the plateau
   and the spire plaza. Check the landmark is visible from the start.
4. Take the optional gully north to the overlook. Check the gully width and that the camera does not clip the walls.
5. Walk the survey installation and the disturbed plot, and check the stakes and the heap.
6. Walk into each boundary ridge and obstacle, and confirm the capsule stops against it.
7. In Play Mode, confirm the region lookup for the start is `nacre/frontier/survey-site`. A query outside the footprint
   (for example at the origin course) should return outside.

## Known limitations

- **Not yet run in Unity.** The scene and assets were written as text and checked as described above. Open the scene
  once in the Editor and run the checklist before relying on it. Unity may re-serialise the file on first save.
- **Graybox only.** No textures and no fog. The baseline lighting is documented in [`lighting-baseline.md`](lighting-baseline.md), and no
  atmosphere. The palette is a provisional colour grid.
- **Elevation is ramp- or step-based only.** There is no jump, so a height change above 0.3 m must have a ramp.
- **The overlook can also be reached by stepping up its 0.25 m edge** without its ramp. This is within the step offset, so
  it is harmless, but the ramp is the intended entry.
- **Single region.** One definition covers the whole footprint. Splitting it, for example into a separate ecology region,
  is a later decision.
- **No gameplay in the region.** The survey installation, the disturbed plot, and the beacon are placeholders. The region
  has no effect on state, and the disturbed site has no ecology logic.
- **Scene shared with the movement course.** The older movement test course (`GBX_*` objects) is still at the origin,
  about 60 m from Nacre's centre, and cannot be reached from Nacre. Nacre's boundary keeps the player inside. To test the old course,
  temporarily move the player spawn back to `(0, 1, -10)`.
- **Interaction target moved.** `INTERACTION_TEST_Target` now sits 2 m in front of the Nacre spawn, so the interaction
  check works from the start. It is still validation scaffolding.
- **Start facing.** The player starts facing north, as the camera does by default, so the optional gully is straight
  ahead, while the main route begins to the east. The route strips and the spire are the navigation cues.
- **Movement course is unchanged.** The player controller, camera, input, bootstrap, and region code are untouched.
- **No out-of-bounds recovery.** A player placed outside the footprint (for example by a future save restore) falls
  forever, because nothing recovers them. Clamp restored positions to the footprint, or add a respawn, when save
  restore is wired. A kill zone was not added.
- **Prompt 14 not run in Play Mode.** The routes, spawn, passages, and perimeter were checked only with the proxy above.
- **Spire plinth is not a platform.** The monolith fills most of it, so the player can stand only on the north band.
- **Interaction target blocks the straight line north from the spawn.** It is validation scaffolding and can move.
- **Crevices are dead ends, not gaps.** If creatures are given navigation, their agent radius must be below
  the gap widths in the crevice table, or they will get stuck in them.
- **No jump.** Creatures and the player both need the ramp to reach the plateau, so creature routes must follow the same rules.
- **Camera near the ridges.** The 4 m ridges are above head height. Confirm that orbit-camera collision keeps the view inside
  the footprint and does not show the empty space beyond the ridges.
