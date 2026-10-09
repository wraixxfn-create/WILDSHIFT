# World-state data foundation

This is the first data-only foundation for region state on Nacre. It gives regions stable identities and isolated,
mutable runtime state, but it is **not** a simulation or save system. There is no planet generation, region streaming,
ecology, faction logic, wildlife AI, or environmental transformation in this layer.

## Current types

| Type | Namespace | Responsibility |
| --- | --- | --- |
| `RegionDefinition` | `Wildshift.World` | Shared ScriptableObject authoring data: a stable ID, optional display label, and starting value for the foundation test. It is read-only at runtime. |
| `RegionState` | `Wildshift.World` | `[Serializable]` plain C# runtime data created as a fresh copy for one registered region. Currently holds the stable ID, display label, and one foundation-only mutable integer (`TestValue`). |
| `WorldStateService` | `Wildshift.World` | Non-global in-memory owner of explicitly registered region states. Uses stable IDs for lookup, enumerates its states in registration order as a snapshot (`GetRegisteredRegions`), and returns descriptive errors for invalid, duplicate, or unknown IDs. |

The stable ID is the identity (`RegionDefinition.StableId` / `RegionState.StableId`); the ScriptableObject name,
display label, scene name, and GameObject name are not. IDs are compared ordinally and are case-sensitive. Choose
an authored ID that can remain unchanged across scene and save-format changes, for example `nacre/coast/north`.

## Configuration and mutable state

`RegionDefinition` contains only authored starting data. `WorldStateService.TryRegisterRegion` copies that data into
a newly allocated `RegionState`; gameplay updates never write back into the definition asset. Each service owns its
own dictionary and each ID in that dictionary has its own state object. Create a service for the world/session that
owns those values rather than storing runtime state in a static field or shared asset.

A minimal use from a composition root or test looks like this:

```csharp
var worldState = new WorldStateService();
if (!worldState.TryRegisterRegion(regionDefinition, out string error))
{
    // Report the configuration error to the caller/log.
}

if (worldState.TryGetRegion("nacre/coast/north", out RegionState region, out error))
{
    worldState.TryUpdateTestValue(region.StableId, region.TestValue + 1, out error);
}
```

`TryGetRegion` and `TryUpdateTestValue` return `false` and a descriptive `error` for an unknown ID instead of
throwing. The service deliberately does not log on the caller's behalf: callers decide whether and where to surface
the returned error. Duplicate IDs and definitions with blank IDs are also rejected without replacing existing data.
`TestValue` exists only to exercise registration and mutation; it has no gameplay or ecological meaning.

## Extending the model later

Keep future changing data inside the serializable runtime state (or serializable subobjects owned by it), not in a
`RegionDefinition` asset. Add only the data contract needed when a system's terms and ownership are defined; this
foundation assigns no scales, rules, or behaviors to these concepts.

| Future concern | Possible data owned by `RegionState` | Keep out of this foundation for now |
| --- | --- | --- |
| Ecological condition | A small serializable ecology-state value/object once ecology defines its vocabulary. | Ecology calculations, species populations, or environmental simulation. |
| Human settlement influence | Region-local settlement influence data referencing stable settlement IDs. | Settlement behavior, expansion, or cross-region propagation. |
| Faction influence | Region-local influence data referencing stable faction IDs. | Faction AI, diplomacy, territory simulation, or faction lifecycle. |
| Known environmental changes | A region-owned collection of serializable change records once change IDs and provenance are designed. | Applying transformations or simulating their consequences. |
| Persistent world events | A region-owned collection of durable event records with stable IDs. | Event scheduling, quests, or save-format decisions (those belong to `Wildshift.Persistence`). |

When adding collections, initialize a separate collection per `RegionState` and avoid shared mutable defaults. Keep
references between regions and other systems as stable IDs or serializable values, not scene-object references.
`RegionState` is marked `[Serializable]` so it can remain a data-transfer boundary, but disk persistence, migration,
and save-version handling belong to `Wildshift.Persistence`. That layer saves region state by stable ID through its
own `RegionSaveData` record rather than serializing `RegionState` itself, so a new runtime field is only persisted
once persistence adds a matching, validated field; see `local-save-foundation.md`.

## Tests and verification

`Assets/_Project/Tests/EditMode/WorldStateServiceTests.cs` covers stable-ID registration/retrieval, test-value updates,
unknown-ID error reporting, duplicate-ID rejection, deterministic registration-order enumeration via
`GetRegisteredRegions`, independence between region IDs, and independence between two
services initialized from the same definition asset. Run it with the project's Edit Mode suite in Unity 6000.3.24f1:

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
      -testResults TestResults/editmode.xml -logFile -
```

Unity is not installed in the agent environment, so the Edit Mode tests must still be run in the Unity Editor.
