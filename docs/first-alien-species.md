# First alien species: the Siltveil Grazer

This is the first creature in the WILDSHIFT ecology prototype. It adds **one** original species, a
reusable species-definition data model, and one instance placed in the Nacre survey site. It does not
add breeding, hunger, animation rigs, loot, combat, or population dynamics, and it does not add a
second species.

Everything here follows [`architecture.md`](architecture.md): authored configuration is a
`ScriptableObject` under `Assets/_Project/Data/`, runtime state lives in the object that owns it, and
gameplay reads its numbers from the definition instead of hard-coding them.

| Concern | Where it lives |
| --- | --- |
| Authored species data | `Assets/_Project/Scripts/Runtime/Ecology/Creatures/CreatureSpeciesDefinition.cs` (`Wildshift.Ecology.Creatures`) |
| Authored species asset | `Assets/_Project/Data/Ecology/Species/NacreSiltveilGrazer.asset` |
| Authoring rules | `CreatureSpeciesIdRules.cs`, `CreatureSpeciesValidator.cs` |
| Per-instance runtime state | `CreatureInstanceState.cs` (plain C#) |
| Scene component | `CreatureBehaviorController.cs` |
| Placeholder prefab | `Assets/_Project/Prefabs/Nacre/Creatures/SiltveilGrazerPlaceholder.prefab` |
| Placeholder material | `Assets/_Project/Materials/Nacre/NacreCreaturePlaceholder.mat` |
| Placed instance | `ECO_Grazer_Siltveil_A` under `NACRE_12_Ecology_Creatures` in `Prototype.unity` |

## The creature

**Siltveil Grazer** — `nacre/species/siltveil-grazer`, size class **Small** (about 0.6 m tall, 1 m long).

A low, broad-bodied grazer of the east lowlands. It sifts the pearlescent microbial mat that spreads
over disturbed soil through a soft fringe along its belly, so it feeds on ground that has recently
been turned over — which is why this one lives at the disturbed ecology plot the player already
samples from. A dorsal fan of thin plates folds flat against rock, so a resting grazer reads as
another shell shard among the shards. It has no eyes to speak of: a ring of pressure pits around its
snout reads footfalls through the ground.

That last detail is the design constraint the whole behaviour set hangs on. Because it senses
**movement through the ground** rather than shapes:

- it notices a *moving* actor from farther away than a still one — a surveyor who stops walking can
  be approached;
- cover works, because terrain between the creature and the actor blocks the ray the perception
  check casts (`Requires Line Of Sight`);
- a **sudden disturbance** — a fall, a bang, anything abrupt — carries much farther than the sight of
  movement, so the startle radius is deliberately larger than the notice radius;
- it is cautious rather than territorial. It never charges, never attacks, and never defends a spot.
  Its only responses are to stop and watch, or to leave.

Intended read for the player: a small animal that is aware of you before you are close, that freezes
when you close in, and that bolts if you keep coming — and that you can actually get near if you
stop moving and use the rock.

## Behaviour

Four states, driven entirely by the species asset. `CreatureInstanceState` decides *intent*;
`CreatureBehaviorController` turns intent into `CharacterController` movement.

```
        idle interval elapsed                reached target / leg timed out
Idle  ───────────────────────────►  Wander  ────────────────────────────►  Idle
  ▲                                   │                                      ▲
  │ threat gone, hold and             │ stimulus inside Notice Radius        │
  │ calm-down both elapsed            ▼                                      │
  └──────────────────────────────  Alert  ─────────────────────────────────┘
                                    │  ▲
        stimulus inside Flee Radius │  │ after the flee duration, if the
                                    ▼  │ threat is only inside Notice Radius
                                  Flee ┘
```

- **Idle** — stands and grazes in place for a random time between `Idle Min Seconds` and
  `Idle Max Seconds`, then picks a new spot.
- **Wander** — walks at `Wander Speed` toward a point inside `Wander Radius` of the position it
  spawned at (its home). Reaching the point returns it to Idle. A leg that takes longer than the
  straight-line crossing time plus two seconds is treated as blocked and abandoned, so a grazer
  pressed against a rock slab re-picks a spot instead of grinding forever.
- **Alert** — stops, turns to face the stimulus, and holds. It leaves only when *both*
  `Alert Hold Seconds` (no flicker on a passing actor) and `Calm Down Seconds` (wariness outlasts the
  sighting) have run out.
- **Flee** — runs at `Flee Speed` directly away from the threat for `Flee Duration Seconds`, then
  looks again: still inside the flee radius it flees again, only inside the notice radius it becomes
  Alert, and with no stimulus left it stops and grazes. Flight may carry it out to twice
  `Wander Radius` from home, but no further, so a chase cannot push it out of the region.

Two inputs drive all of it:

- **Perception.** Every `Perception Interval` seconds the controller measures how far each watched
  actor moved. An actor above `Minimum Actor Speed`, inside `Notice Radius`, with line of sight, is
  reported to the state as a stimulus.
- **Sudden disturbances.** Any system can call `CreatureBehaviorController.NotifySuddenDisturbance(position)`.
  Inside `Startle Radius` the creature flees immediately, even from beyond the notice radius. Nothing
  calls it yet; it is the seam for footsteps, gunfire, falling rock, or a designer's debug key.

## Species parameters

Every field below is authored on the asset and read at runtime. Nothing species-specific is
duplicated on the prefab, the controller, or any other script.

### Identity

| Field | Authored | Purpose |
| --- | --- | --- |
| Stable Id | `nacre/species/siltveil-grazer` | Identity in logs, events, and any future save data. Typed once by an author, never generated from a prefab, scene object, or asset name, and not changed afterwards. |
| Display Name | `Siltveil Grazer` | Short label for logs and future UI. Never the identity key. |
| Lore Description | 3 sentences | In-world text for designers and players. Drives no rules. |
| Size Class | `Small` | Coarse band (`Tiny`…`Enormous`) so species can be compared without inventing a body-mass system. Visible size comes from the prefab. |

### Movement

| Field | Authored | Purpose |
| --- | --- | --- |
| Wander Speed | 0.7 m/s | Grazing pace. Slow enough that the player walks alongside it; a grazer that keeps up with the player reads as a predator. |
| Flee Speed | 3.4 m/s | Escape pace. Faster than the player's 4 m/s walk is not required, but it must beat the grazer's own pace or flight gains nothing. At 3.4 m/s a walking player closes on it and a sprinting player outruns it. |
| Acceleration | 9 m/s² | How quickly it reaches either speed. Used for speeding up and slowing down. |
| Turn Rate | 260 °/s | How sharply it can corner. Low values read as heavy, high values as darting. |
| Maximum Slope | 40° | Steepest climbable slope. Applied to the `CharacterController` when the creature initializes, so it is species data and not a prefab setting. |
| Wander Radius | 4 m | Radius of the home range around its spawn point. Flight is allowed out to twice this. |

### Detection

| Field | Authored | Purpose |
| --- | --- | --- |
| Notice Radius | 9 m | Distance at which a moving actor is noticed and raises an alert. Measured horizontally, from the creature. |
| Flee Radius | 3.5 m | Distance at which a noticed stimulus becomes a threat. Must not exceed the notice radius, so the creature never flees from something it has not noticed. |
| Startle Radius | 12 m | Distance at which a *sudden disturbance* forces immediate flight. Larger than the notice radius because an abrupt event carries farther than the sight of movement. |
| Minimum Actor Speed | 0.6 m/s | Slowest movement that counts. A standing player is invisible to it, which is what makes "stop and it lets you approach" work. |
| Perception Height Offset | 0.3 m | Height above the creature's pivot that perception rays start from, near its sensory pits, so props occlude at the right height. |
| Requires Line Of Sight | on | Terrain and props on the occluder layers block perception. This is the "uses the terrain for cover" rule, and it applies to the creature hiding as much as to the player hiding. |
| Perception Interval | 0.15 s | Seconds between perception samples. Cheaper than per-frame raycasts; above 0.5 s is reported as a warning because fast actors would be noticed late. |

### Behaviour timing

| Field | Authored | Purpose |
| --- | --- | --- |
| Idle Min / Max Seconds | 2.5 / 7 s | Grazing interval between wander legs. The spread is what stops a group of creatures marching in step. |
| Alert Hold Seconds | 1.2 s | Minimum time spent Alert after noticing something, even if the stimulus stops at once. Stops a passing actor causing a flicker. |
| Calm Down Seconds | 3.5 s | Time without a stimulus before an alert creature wanders again. This is the species' wariness dial. |
| Flee Duration Seconds | 1.6 s | Time spent fleeing before it looks again. Long enough to break line of sight, short enough that it does not leave the region. |

## Authored data vs. runtime state

The split is explicit, and it is the same one `RegionDefinition` / `RegionState` already uses:

- `CreatureSpeciesDefinition` is **shared, read-only configuration**. One asset describes the whole
  species and every instance reads it. It holds no timers, no targets, no "is it alert right now".
- `CreatureInstanceState` is **per-instance, mutable runtime state**, created by
  `CreatureSpeciesDefinition.CreateInitialState(homePosition, randomRange)`. It owns the current
  behaviour, the behaviour timers, the wander target, the flee target, and the last stimulus. It
  reads the species asset and never writes to it. It is plain C# with no Unity lifecycle, so a test
  can step it with a fixed delta time and a deterministic random function.
- `CreatureBehaviorController` is the **owner**. It creates the state in `Awake`, feeds it delta time
  and the creature's position, and applies the resulting intent. It exposes the state read-only.

Randomness is injected as a `Func<float, float, float>`; production passes `UnityEngine.Random.Range`,
tests pass a function that returns a fixed bound.

## Validation

`CreatureSpeciesValidator.Validate(species, errors, warnings)` is pure logic with no console output,
so the same rules run from the asset's **Validate Species** context-menu item, from a creature's
initialization, and from tests. It appends to caller-supplied lists and returns false when it added
an error.

**Errors** (a creature refuses to run, and the component disables itself after logging each message):

- no species asset assigned;
- blank or whitespace-bearing Stable Id (`CreatureSpeciesIdRules`, the same rules the region IDs use);
- blank Display Name; an undefined Size Class;
- any non-finite number;
- a non-positive speed, radius, duration, or interval; a negative slope limit, perception height, or
  minimum actor speed;
- Idle Max below Idle Min; Maximum Slope at or above 90°;
- **contradictions**: Flee Speed not greater than Wander Speed (it could never escape), and Flee
  Radius greater than Notice Radius (it would flee from stimuli it never noticed).

**Warnings** (legal, but usually a mistake):

- no lore text;
- Startle Radius smaller than Flee Radius, so a sudden disturbance carries less far than an
  approaching actor;
- Perception Interval above 0.5 s, so fast actors are noticed late.

## What was added to the scene

- `NACRE_12_Ecology_Creatures`, a new group under `NACRE_GRAYBOX_REGION_ROOT` (the region root now
  has 13 children). Groups 00–11 are untouched.
- `ECO_Grazer_Siltveil_A`, an instance of `SiltveilGrazerPlaceholder.prefab` at world
  **(74.0, 0.08, −8.5)**, on the disturbed ecology plot: 1.8 m from the plot's centre and 5.0 m from
  the nearest edge of `Main_Route_North`, so a player walking the route passes inside the 9 m notice
  radius. Its `_perceivedActors` list holds the player transform, assigned on the instance rather
  than on the prefab so the prefab stays scene independent.

The placeholder is five Unity cubes on one material — body, snout, dorsal fan, and the two siltveil
flaps — inside a 0.55 m × 0.25 m `CharacterController`. It casts shadows, and it has no animation:
`CreatureBehaviorController` moves and turns the root, which is all the behaviour needs.

## Adding a second species later

No change to the data model, no new component, no editor tooling:

1. **Create the asset.** `Assets > Create > Wildshift > Ecology > Creature Species Definition`. Give
   it a new Stable Id (`nacre/species/<name>`), a display name, lore, and its own numbers.
2. **Validate it.** Select the asset and choose **Validate Species** from the Inspector's context
   menu. Every error and warning is reported with the fix, and the rules are the same ones a creature
   applies at runtime.
3. **Make a prefab.** Duplicate `SiltveilGrazerPlaceholder.prefab`, reshape the placeholder meshes and
   resize the `CharacterController` to the new body, and point `Species` at the new asset. The
   capsule dimensions belong on the prefab because they follow the mesh; every behaviour number stays
   on the asset.
4. **Place instances.** Drop the prefab in the scene and set `_perceivedActors` per instance.

The same `CreatureBehaviorController` and `CreatureInstanceState` run it unchanged, because neither
holds a species value. A species that needs a parameter the model does not have yet is a field on
`CreatureSpeciesDefinition` plus a rule in `CreatureSpeciesValidator` plus a use in
`CreatureInstanceState` — an addition to the shared model, not a parallel model.

## Verification

### Done in the agent environment (no Unity Editor available)

Unity 6000.3.24f1 is not installed here, and neither is any .NET or Mono toolchain, so the Edit Mode
suite was **not** run and no C# was compiled. These checks were run against the committed files, with
a collider proxy built from the scene the way [`nacre-graybox-props.md`](nacre-graybox-props.md) does:

0. **Syntax.** All 95 `.cs` files in `Assets/` — the 10 new ones and the 85 already in the project —
   were parsed with the `tree-sitter-c-sharp` grammar and contain no `ERROR` or missing nodes. Three
   copies of `CreatureInstanceState.cs` with a deliberately removed brace, a removed semicolon, and a
   removed closing paren were parsed as controls and were all reported as broken, so the clean result
   is not a vacuous one. This is a syntax check, not a compile: it does not type-check.
1. **Asset integrity.** All 187 `.meta` GUIDs are unique. Every GUID and in-file `fileID` reference in
   `Prototype.unity`, `SiltveilGrazerPlaceholder.prefab`, and `NacreSiltveilGrazer.asset` resolves;
   the only external GUIDs are the built-in cube mesh and the URP Lit shader the palette already uses.
2. **Schema agreement.** The 22 `[SerializeField]` fields declared by `CreatureSpeciesDefinition.cs`
   and the 22 keys in `NacreSiltveilGrazer.asset` match exactly — no missing field, no stale key. The
   4 serialized fields of `CreatureBehaviorController.cs` match the 4 the prefab stores, and the
   prefab stores no species tuning values at all.
3. **Wiring.** The asset resolves to `CreatureSpeciesDefinition.cs`; the prefab's MonoBehaviour
   resolves to `CreatureBehaviorController.cs` and references `NacreSiltveilGrazer.asset`; the scene
   instance references the prefab, is named `ECO_Grazer_Siltveil_A`, watches the player transform
   (`fileID: 3002`), sits under `NACRE_12_Ecology_Creatures`, and that group is listed by
   `NACRE_GRAYBOX_REGION_ROOT`.
4. **Placeholder validity.** The `CharacterController` height (0.55) is at least twice its radius
   (0.25), which Unity requires. All five renderers use the one new material, and every mesh is the
   built-in cube.
5. **Placement.** The spawn capsule spans y 0.08–0.63 and overlaps none of the 66 solid Nacre box
   colliders. The nearest solids are `Survey_Case_Plot_Sample` at 1.97 m, `Spoil_Heap` at 2.06 m, and
   `Plot_Stake_04` at 2.13 m. The 4 m wander disc crosses no boundary ridge. The ground sampled
   across it is the plot surface at 0.06 m and the surrounding lowland at 0.00 m, plus the sample
   tray top at 0.21 m — the one step the capsule cannot climb at its 0.15 m step offset, and a
   blocked leg there is abandoned and re-picked, which is the designed behaviour. The creature is
   inside the `WorldRegionVolume_SurveySite` box, so a future event would resolve to
   `nacre/frontier/survey-site`.
6. **Ranges.** Every authored value is finite and inside the ranges the validator accepts: flee
   (3.4) > wander (0.7); flee radius (3.5) ≤ notice radius (9) < startle radius (12); idle max (7) ≥
   idle min (2.5); perception interval (0.15) ≤ 0.5. The asset validates with **0 errors and 0
   warnings**.

This is a proxy. It does not model the `CharacterController`'s skin width, ground probing, or
sliding, and it cannot execute C#.

### To run in the Unity Editor

1. Open `Prototype.unity`. No missing-script, missing-prefab, or missing-material warnings. The
   Console shows one line as the scene loads:
   `[Wildshift] Creature 'ECO_Grazer_Siltveil_A' configured as 'Siltveil Grazer'
   (nacre/species/siltveil-grazer), size class Small: notice 9 m, flee 3.5 m, startle 12 m,
   wander 0.7 m/s, flee 3.4 m/s.` — that line is the confirmation that the configuration is assigned
   and passed validation.
2. Select `ECO_Grazer_Siltveil_A`. Its Inspector shows **Species** = `NacreSiltveilGrazer` and no
   speed, radius, or timing fields. With the object selected, the Scene view draws the notice (amber),
   flee (red), and startle (violet) circles, the home range and its flight limit (blue), and a line to
   the current movement target.
3. Enter Play Mode. The grazer alternates between grazing in place and short walks inside its 4 m
   home range, and logs each transition (`Idle -> Wander`, and so on).
4. Walk toward it. At about 9 m it stops and turns to face you (`-> Alert`). Keep walking to within
   about 3.5 m and it runs (`-> Flee`), then re-evaluates after 1.6 s.
5. Stand still inside 9 m. It stays alert for roughly `max(1.2 s, 3.5 s after the last movement)` and
   then goes back to grazing — approaching a stopped player works.
6. Break line of sight behind `Spoil_Heap` or a rock slab and walk: it does not notice you.
7. Select `NacreSiltveilGrazer.asset` and choose **Validate Species** from the Inspector's context
   menu: one info line, no errors or warnings. Then set Flee Speed to 0.2 and validate again: an error
   naming both speeds. Undo it.
8. Edit Mode tests: `CreatureSpeciesDefinitionTests`, `CreatureInstanceStateTests`, and
   `CreatureBehaviorControllerTests`.

### Tests

- `CreatureSpeciesDefinitionTests` — authored values exposed; a coherent species passes with no
  errors and no warnings; a missing species, an unusable Stable Id, a blank Display Name, each
  non-positive required value, each negative optional value, non-finite numbers, and a slope limit at
  or above 90° are errors; the flee/wander speed and flee/notice radius contradictions and an idle
  maximum below the minimum are errors; missing lore, a startle radius inside the flee radius, and a
  slow perception interval are warnings; validation appends without clearing and requires both
  message lists; the shipped `NacreSiltveilGrazer.asset` itself validates clean.
- `CreatureInstanceStateTests` — starts Idle at home with no stimulus; grazes for the authored idle
  interval then wanders; the wander target is inside the home range; it grazes again on arrival; a
  blocked leg is abandoned; a stimulus inside the notice radius raises an alert and one beyond it is
  ignored; a stimulus inside the flee radius starts flight; flight leads away from the threat and
  stays near home; it keeps fleeing while the threat is close, becomes alert when the threat drops
  back, and grazes when the threat is gone; alert waits out both the hold and the calm-down; a
  stimulus seen during the calm-down restarts it; a sudden disturbance inside the startle radius
  starts flight from beyond the notice radius and one outside it is ignored; height does not extend
  perception; behaviour changes fire once per transition; the idle interval comes from the authored
  range; and it works with no random function supplied.
- `CreatureBehaviorControllerTests` — a running creature reports the species it was configured from;
  the species slope limit is applied to the `CharacterController`; a creature with no species, or with
  a contradictory one, does not run and says why; a warning still runs; ticking and disturbances
  before configuration are safe; a sudden disturbance inside the startle radius starts flight and one
  outside it changes nothing; it notices a moving actor inside the notice radius, ignores a still one
  and a distant one; terrain hides an actor, and a species that senses through obstacles ignores
  cover; it wanders at the speed the species authors, live from the asset.

## Known limitations

- **Not saved.** Creature position and behaviour are session state. Loading a save restores the
  player, not the grazer; it stays where it wandered to. Wiring it into `GameSaveMapper` is a
  separate step.
- **One instance.** This pass places one creature. Nothing tracks a population, and nothing prevents
  two instances of the same species from being placed — which is intended, but no spawner exists.
- **No navigation.** Wander targets are picked in a disc and walked toward directly. A grazer can
  press against a rock slab until its leg times out and it picks another spot. It also has no
  pathfinding, so it will not route around an obstacle. The dead-end crevices listed in
  [`nacre-graybox-region.md`](nacre-graybox-region.md) are 0.3 m to 0.6 m wide against this
  creature's 0.5 m capsule diameter: it cannot enter the four narrowest, but it can enter the 0.6 m
  one between `Boulder_South_East` and `Boundary_South`. Nothing in this pass puts one there — its
  home range is on the plot — but a future species with a smaller capsule would need the same check.
- **No animation or sound.** The placeholder is rigid cubes. Turning is the only readable motion.
- **The flee clamp does not know about geometry.** Twice the wander radius is 8 m here, and that disc
  reaches the east boundary ridge at x 78.5. A grazer fleeing east can press against the ridge until
  its 1.6 s flee leg ends and it re-evaluates. It cannot leave the region, but the stop is a collision
  rather than a decision; teaching the flee target about obstacles is part of the navigation step.
- **Nothing calls `NotifySuddenDisturbance` yet.** The disturbance path is exercised only by tests
  until a system produces events.
- **The player is watched by reference, not by type.** `_perceivedActors` is a scene-assigned
  `Transform` list. A future actor that should startle wildlife needs to be added to that list, or the
  reference replaced by a shared actor registry.
