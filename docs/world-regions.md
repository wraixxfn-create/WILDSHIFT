# Stable world region registry

This layer lets the prototype answer two questions reliably: **which authored region is this ID?** and **which region
contains this world position?** It builds on the world-state foundation in [`world-state.md`](world-state.md) and reuses
its authored asset, `RegionDefinition`, instead of introducing a second region type.

It is **not** streaming, procedural generation, a region graph, or ecology. Regions have no gameplay effect yet.

## Types

| Type | Kind | Namespace | Responsibility |
| --- | --- | --- | --- |
| `RegionDefinition` | ScriptableObject (existing, extended) | `Wildshift.World` | The authored region: stable ID, display name, short description, and the Prompt 8 foundation test value. Read-only at runtime. |
| `WorldRegionCatalog` | ScriptableObject | `Wildshift.World.Regions` | Authored list of the `RegionDefinition` assets that make up one world. Configuration only. |
| `WorldRegionRegistry` | Plain C# | `Wildshift.World.Regions` | Read-only index from stable ID to definition. Built from a catalog; all-or-nothing validation. |
| `WorldRegionIdRules` | Static | `Wildshift.World.Regions` | Text rules for stable IDs (non-blank, no whitespace or control characters). |
| `WorldRegionVolume` | MonoBehaviour | `Wildshift.World.Regions` | An oriented box in world space that belongs to one `RegionDefinition`, with an overlap priority. |
| `WorldRegionLocator` | MonoBehaviour | `Wildshift.World.Regions` | Scene-owned list of volumes plus a catalog. Validates the setup and answers `FindRegionAt`. |
| `WorldRegionQueryResult` | Readonly struct | `Wildshift.World.Regions` | Result of a position query: status, chosen volume, and overlap count. |
| `WorldRegionBox` | Readonly struct | `Wildshift.World.Regions` | Plain box geometry: containment and overlap (separating-axis test). |
| `WorldRegionSetupValidator` | Static | `Wildshift.World.Regions` | Pure validation shared by the Inspector, runtime initialization, and tests. |
| `IWorldRegionContext` | Interface | `Wildshift.World.Regions` | Read-only action-attribution seam implemented by the player integration; requests the authoritative stable ID at commit time. |

`WorldStateService` (runtime state) and `PlayerActionEventRecorder` (event log) remain separate. The registry holds no runtime
state, and nothing here generates IDs. `PlayerRegionAssociation` composes the locator for player actions without adding new
lookup rules; see [`player-region-integration.md`](player-region-integration.md).

## Stable IDs

The stable ID (`RegionDefinition.StableId`) is the region's identity in saves, events, and gameplay code.

- It is typed by an author and never generated. Nothing in the project fills it in on creation, on import, or during play.
- It must be **non-blank** and must contain **no whitespace or control characters**. Use `-` for words and `/` for levels,
  for example `nacre/coast/north`.
- It is compared **ordinally and case-sensitively**.
- It is **independent** of the asset file name, the display name, the GameObject name, the scene, the hierarchy position, and
  the order of any list. Renaming an asset does not change its ID.
- It must be **unique within a catalog**. Changing an ID after saves or other data reference it breaks those references,
  so treat IDs as permanent once they are used.

`WorldRegionRegistry.TryCreate` enforces these rules and reports every violation at once.

## Authoring a new region in the Unity Editor

1. **Create the region asset.** In the Project window, open `Assets/_Project/Data/WorldRegions/`. Choose
   *Create > Wildshift > World > Region Definition* and name the file for people, for example `RegionDef_CoastNorth`.
2. **Fill in the Inspector.**
   - *Stable Id*: a permanent ID such as `nacre/coast/north`. It must be unique in the catalog and must not contain spaces.
   - *Display Name*: the label shown to people. It is not used as the key.
   - *Description*: a short designer note.
   - *Initial Test Value*: foundation-only; leave it at 0 unless a test needs otherwise.
3. **Add it to a catalog.** Select (or create, with *Create > Wildshift > World > Region Catalog*) the catalog for your
   world, such as `DevWorldRegionCatalog`, and add the new definition to its *Regions* list. Right-click the catalog's
   Inspector header menu and choose **Validate Region Catalog**. The Console reports any empty, duplicate, or invalid entries,
   and a valid catalog logs a short confirmation.
4. **Place the volume in the scene.** Create an empty GameObject with a *WorldRegionLocator* component (one per world or
   test area). Assign its *Region Catalog*. Under that object, create one child per box of the region:
   - Add a *WorldRegionVolume* component to each child.
   - Set *Region Definition* to the asset from step 1.
   - Set *Local Center* and *Local Size*. The box is in the child's local space, so move, rotate, and scale the child to place
     it. Every size axis must be greater than zero.
   - Leave *Priority* at 0 unless this volume must win over an overlapping region (see below).
   - Drag the child into the locator's *Volumes* list. A volume under the locator that is missing from that list is an error.
5. **Check the setup.** Select the locator and choose **Validate Region Setup** from its component menu. Selected volumes draw
   a green wireframe box in the Scene view, so you can check their extent visually.
6. **Enter Play Mode** and check the Console. A valid setup logs nothing. Errors are logged once at startup. Warnings, such
   as an equal-priority overlap, are logged alongside them.

A region may be covered by several volumes. Add one child per box, all with the same *Region Definition*.

## Position queries

```csharp
WorldRegionQueryResult region = locator.FindRegionAt(player.transform.position);

switch (region.Status)
{
    case WorldRegionQueryStatus.Found:
        string id = region.RegionId;                 // e.g. "nacre/coast/north"
        RegionDefinition definition = region.Definition;
        bool contested = region.IsOverlapping;       // more than one region contains the point
        break;

    case WorldRegionQueryStatus.OutsideAllRegions:
        // Normal: the point is in no authored region. Not an error.
        break;

    case WorldRegionQueryStatus.Unavailable:
        // The setup is invalid. See locator.ValidationErrors and the Console.
        break;
}
```

`FindRegionAt` does not allocate and does not log, so it can be called every frame from `Update` or later. If the locator has
not initialized yet (its `Awake` has not run), the first query initializes it.

### Rules

| Situation | Result | Notes |
| --- | --- | --- |
| The position is inside one active volume. | `Found`, that region. | `OverlappingRegionCount` is 1. |
| The position is inside no active volume. | `OutsideAllRegions` | Expected. It is not an error and it does not log. |
| The setup is invalid. | `Unavailable` | Every query returns this, so it is never mistaken for "outside". The reasons are in `ValidationErrors`. |
| The position is inside volumes of **one** region, several times. | `Found`, that region. | Several volumes of one region count as one region. Not an overlap. |
| The position is inside volumes of **several** regions. | `Found`, the region of the volume with the **highest `Priority`**. | `IsOverlapping` is true and `OverlappingRegionCount` is the number of distinct regions. |
| Several regions tie on priority. | `Found`, the region with the **lower stable ID** (ordinal). | Deterministic for any list or hierarchy order. The editor and runtime log a warning for this case, because it is usually unintended. |

Other details:

- **Boundaries are half-open.** A point is inside a box when, on every axis, the minimum face is included and the maximum
  face is excluded. Two adjacent regions that share a face therefore never both contain the same point. The position
  exactly on the shared face belongs to the region on its positive side.
- **Transforms apply.** Position, rotation, and scale on the volume's transform are used at query time, so moving a volume
  at runtime takes effect immediately. Skew inherited from a rotated, non-uniformly scaled parent is not supported.
- **Disabled volumes are ignored.** A volume whose component or GameObject is inactive is skipped.
- **Overlap is allowed.** Overlapping volumes of different regions are not an error. Use priority for the intended winner, and
  shrink the volumes where the overlap is unintended.

## Validation and console reporting

Validation is shared by the Inspector, runtime startup, and tests (`WorldRegionSetupValidator`). It reports:

**Errors** (make the locator `Unavailable`):

- No Region Catalog assigned.
- A catalog entry that is empty, has an invalid stable ID, or duplicates an ID (both assets are named).
- A volume entry that is empty, or listed more than once.
- A volume with no Region Definition, or a definition with an empty or invalid stable ID.
- A volume whose definition is not **the registered asset** in the catalog. A different asset that only shares the ID is rejected.
- A volume with a non-finite center, or with a size that is non-finite or zero or negative on any axis.
- A `WorldRegionVolume` under the locator that is not in its *Volumes* list.

**Warnings** (the locator stays available):

- Volumes of different regions that overlap at equal priority.

Errors and warnings are logged with `WildshiftLog`, so clicking a message selects the object to fix. A catalog logs its own
errors when it is edited. A locator logs every problem it finds, including catalog and volume problems, when it is edited, when
**Validate Region Setup** runs, and once at startup.
Editing a region's own ID does not re-run the locator's validation until the locator is edited or the game starts. Use the
context-menu validators after changing IDs.

## Relationship to world state

The registry answers *which region is this?* It does not hold runtime values. To register authored regions in a world state, use the
same catalog in the composition root:

```csharp
List<string> errors = new List<string>();
if (!catalog.TryCreateRegistry(errors, out WorldRegionRegistry registry))
{
    // Report errors and stop; do not start with a partial region set.
}

var worldState = new WorldStateService();
foreach (RegionDefinition definition in registry.GetAllDefinitions())
{
    worldState.TryRegisterRegion(definition, out string error);
}

// Later, with a position query:
WorldRegionQueryResult region = locator.FindRegionAt(position);
if (region.HasRegion && worldState.TryGetRegion(region.RegionId, out RegionState state, out _))
{
    // Read or update state for the region the position is in.
}
```

Authored values (name, description, starting value) come from the definition. Changing values belongs in `RegionState`. The
definition asset is never written to during play, and the registry is never a store for changing values.

## Verification

Edit Mode tests live in `Assets/_Project/Tests/EditMode/`:

- `WorldRegionRegistryTests.cs`: lookup by ID independent of names and list order; the dev catalog asset defines two regions
  with distinct IDs; empty, whitespace, invalid, duplicate, repeated, and null entries are rejected with actionable messages;
  unknown and blank lookups return errors; a different asset with the same ID is not treated as registered.
- `WorldRegionBoxTests.cs`: half-open containment on axis-aligned and rotated boxes; shared faces belong to one box; zero-size
  boxes contain nothing; overlap, touching-face, and diagonal separating-axis cases.
- `WorldRegionLocatorTests.cs`: position queries inside, outside, and on boundaries; priority and tie resolution, independent of
  list order; several volumes of one region; disabled volumes; transform position, rotation, and scale; and each invalid setup
  (missing catalog, missing or uncatalogued or invalid definitions, duplicate catalog IDs, unlisted, empty, duplicate, and
  zero-size volumes) making the locator unavailable.
- `PlayerRegionAssociationTests.cs`: player entry, leave, direct cross-region movement, no repeated same-region updates,
  delegation to the shared boundary rule, completion-time lookup, and explicit null attribution outside all regions.

The Edit Mode suite runs in Unity 6000.3.24f1 with the command from [`world-state.md`](world-state.md):

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
      -testResults TestResults/editmode.xml -logFile -
```

Unity is not installed in the agent environment, so these tests must be run in the Unity Editor. The locator fixture sets
`LogAssert.ignoreFailingMessages` for its duration, because invalid setups intentionally log errors. Those errors are checked
through `ValidationErrors` and `Initialize()`'s return value instead.

### Manual check in the Editor

1. Create a locator (see the authoring steps) with `DevWorldRegionCatalog`. Add two child volumes: `RegionDef_DevTestNorth`
   at local center `(0, 0, 0)` and `RegionDef_DevTestSouth` at `(10, 0, 0)`, both with local size `(10, 4, 10)`.
2. Select the locator and run **Validate Region Setup**. The Console should show no errors.
3. Select each volume and confirm its green box in the Scene view. Its *Region Definition* field shows which region it belongs to.
4. Enter Play Mode and check the Console. Then query the locator from a temporary script at `(2, 0, 0)` (north),
   `(5, 0, 0)` (south, the shared face), and `(20, 0, 0)` (outside). Remove the temporary script afterwards.

## Extending later

| Future concern | Where it goes | Keep out of this layer |
| --- | --- | --- |
| Region-specific runtime data | `RegionState` in `WorldStateService`, keyed by the same stable ID. | Changing values in `RegionDefinition` or the catalog. |
| Ecology, faction, or settlement influence | Their own systems, reading `FindRegionAt` or the region ID. | Reactions inside the locator. |
| Large or streamed worlds | A spatial index in front of the locator, with the same query contract. | Replacing the validated authored list without updating these rules. |
| Non-box regions | A new volume shape that implements the same containment check. | Changing the stable ID or overlap rules. |
