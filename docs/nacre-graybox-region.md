# Nacre graybox region: first survey site

This is the first playable blockout of Nacre, built in `Assets/_Project/Scenes/Prototype.unity`. It is a remote frontier
survey site: a safe landing pad, one main route with a ramp onto a plateau, an optional gully route, a violet landmark
that is visible from the start, a human-made survey installation, and a disturbed ecology plot. It is **graybox**: primitives
and flat palette colours only, with no final art, lighting pass, enemies, missions, dialogue, or hazards.

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
| `NACRE_08_Obstacles_and_Boulders` | Central rock block, three boulders, two cargo crates. |
| `NACRE_09_Region_Lookup` | `WorldRegionLocator_Nacre` with its `WorldRegionVolume_SurveySite` child. |

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
- `NacreSurveyStake.prefab`: orange accent cube with a box collider. Used 9 times: eight plot stakes and one overlook marker.

Ten palette materials are in `Assets/_Project/Materials/Nacre/`. Each is a URP Lit material with one base colour:

| Material | Used for | Colour |
| --- | --- | --- |
| `NacrePearlGround` | Ground | pale pearl grey |
| `NacreStartPad` | Start pad (safe) | mint grey |
| `NacreRoutePath` | Main route strips | pale sand |
| `NacreOptionalPath` | Optional gully path | dusky lilac |
| `NacrePlateauSurface` | Plateau, ramp, ledge, terrace, plinth | warm light grey |
| `NacreRock` | Ridges, rocks, panel | slate teal |
| `NacreLandmark` | Spire | pearl violet |
| `NacreSurveyStructure` | Hut, mast, crates, sample tray | off-white |
| `NacreSurveyAccent` | Beacon, stakes, crossbar | survey orange |
| `NacreDisturbedSoil` | Soil plot, spoil heap | dark brown-grey |

## Editing the layout

- Move or resize any object in its group. Keep the start, the route strips, and the ramp aligned. The route strips mark the
  intended path, so move them with the ground they sit on.
- To change all stakes or rock blocks at once, edit the prefab. Individual instances can keep overrides for size and position.
- Keep the region volume covering the footprint. An object outside it still renders, but it is not in the region.
- After any change to a region ID or the locator, select `WorldRegionLocator_Nacre` and run **Validate Region Setup**.

## Verification

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
- **Graybox only.** No textures, no lighting or fog tuning beyond the Prototype scene's existing settings, and no
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
