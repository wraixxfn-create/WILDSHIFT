# Graybox movement test environment

`Assets/_Project/Scenes/Prototype.unity` contains the temporary movement test environment. It is deliberately primitive and is not part of Nacre's final world art.

## Hierarchy and replacement

All environment geometry is grouped under `--- GRAYBOX MOVEMENT TEST ENVIRONMENT ---`. Geometry uses clearly prefixed `GBX_` names and standard mesh renderers/box colliders. The player controller does not reference individual environment objects, so the entire group can be replaced without controller changes.

The scene includes:

- a 36 m × 30 m flat floor and open sprint area;
- four perimeter collision walls;
- a 2 m-wide narrow passage;
- three differently sized obstacle blocks and a collision-test wall;
- two moderate 15-degree ramps;
- a 2 m-high elevated platform reached by one ramp;
- a low terrain step;
- contrasting neutral floor, dark-wall, and warm obstacle materials.

The temporary CharacterController player spawns at `(0, 1, -10)`, facing into the course. `Gameplay Input`, `Main Camera`, and the player remain separate from the environment group so environment replacement cannot break their serialized references.

## Controls

- **Move:** WASD, arrow keys, or left gamepad stick
- **Sprint:** Left Shift or gamepad left-stick press
- **Camera:** Mouse delta or gamepad right stick
- **Release/resume captured cursor:** Escape

## Play-mode verification checklist

1. Open `Prototype` and enter Play Mode.
2. Walk and sprint around the perimeter and across the open starting/run area.
3. Approach every wall and obstacle and confirm the CharacterController does not pass through it.
4. Walk through the narrow passage in both directions and orbit the camera while inside it.
5. Traverse both 15-degree ramps in both directions; use the approach ramp to reach and leave the elevated platform.
6. Step onto and off the low terrain step.
7. Orbit the camera near walls and obstacles and confirm camera collision pulls the camera inward rather than clipping through geometry.

The layout is intentionally bounded and compact so a complete pass takes less than a minute.

## Relationship to the Nacre graybox region

The Prototype scene also contains the first Nacre region ([`nacre-graybox-region.md`](nacre-graybox-region.md)), offset to
world `(60, 0, 0)`. The player now spawns at the Nacre start pad, `(47.5, 1.05, -14)`, so the course above is not
reachable from the default spawn. Nacre's perimeter ridges keep the player inside Nacre. To rerun the checklist above, temporarily
set the player's spawn back to `(0, 1, -10)`, or use the Scene view. The course geometry itself is unchanged.
