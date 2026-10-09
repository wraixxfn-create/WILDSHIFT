# Local save foundation

This is the first persistence layer for WILDSHIFT: one local save file that stores a small, versioned
slice of prototype state and restores it safely. It is deliberately **not** a save menu, multiple save
slots, autosave scheduling, cloud saves, achievements, or Steam integration, and it never serializes
scenes, GameObjects, components, or arbitrary object graphs.

## What is saved today

Only state that already exists and changes at runtime is stored:

| Section | Source system | Stored data |
| --- | --- | --- |
| `player` | the player transform (`ThirdPersonPlayerMovement` capsule) | world position and yaw in degrees |
| `regions` | `WorldStateService` / `RegionState` (Prompt 8) | one entry per registered region: its stable region ID and the foundation-only `testValue` |
| `events` | `PlayerActionEventRecorder` (Prompt 9) | a limited, chronological history of recorded player-action events |

Authored data is **not** saved. A region's display label and starting value come from its
`RegionDefinition` asset on every run, so designers can retune them without invalidating saves. Camera
pitch, animation, input, and UI state are not owned by a persistent system yet and are not saved.

## Types

| Type | Namespace | Responsibility |
| --- | --- | --- |
| `SaveSchema` | `Wildshift.Persistence.Model` | Schema version numbers, size limits, and the compatibility rules. |
| `SaveGameData` | `Wildshift.Persistence.Model` | Root payload: schema version, metadata, player, regions, events. |
| `PlayerSaveData`, `RegionSaveData`, `WorldEventSaveData`, `WorldEventParameterSaveData` | `Wildshift.Persistence.Model` | `[Serializable]` data-transfer records holding numbers and stable IDs only. |
| `SaveDataValidator` | `Wildshift.Persistence` | Pure validation of a payload before it is written or applied. |
| `SaveLocation` | `Wildshift.Persistence` | Resolves the save, temporary, and backup paths; exposes player-safe wording. |
| `SaveSerializer` | `Wildshift.Persistence` (internal) | The only type that knows the file is JSON. |
| `LocalSaveService` | `Wildshift.Persistence` | Explicit `Save` / `Load` / `LoadBackup`, atomic writes, version checks, throttling, logging. |
| `PrototypeSaveMapper` | `Wildshift.Persistence` | Copies live prototype state into a payload and applies a validated payload back. |
| `SaveResult`, `LoadResult`, `SaveStatus`, `LoadStatus`, `SaveRestoreSummary` | `Wildshift.Persistence` | Machine-checkable outcomes plus separate developer and player messages. |

The data model is separate from the service on purpose: the model describes the file, the service
describes the file *operations*, and the mapper is the only type that knows about both gameplay
systems and durable data.

## Where the file lives

Saves are written to `Application.persistentDataPath/Saves/prototype-save.json` — the per-user,
per-application folder Unity guarantees is writable on PC (on Windows,
`%userprofile%\AppData\LocalLow\<company>\<product>`). Never write saves next to the executable or
inside the project folder; an installed build cannot rely on those being writable.

Three paths are used:

| File | Purpose |
| --- | --- |
| `prototype-save.json` | the live save |
| `prototype-save.json.tmp` | the in-progress write; swapped in only once it is complete |
| `prototype-save.json.bak` | the contents the last successful save replaced |

Paths are developer data. Gameplay UI must show `SaveLocation.UserFacingDescription` ("the local save
folder for this computer") or the `UserMessage` on a result — never `FilePath`. Full paths appear only
in console/developer logs, which is where they are useful for bug reports.

## File format

Unity's `JsonUtility` writes the serialized field names, so the private field names in the model
**are** the on-disk keys:

```json
{
    "schemaVersion": 1,
    "savedAtUtc": "2026-10-09T12:34:56.7890123Z",
    "applicationVersion": "0.1",
    "player": {
        "positionX": 12.5,
        "positionY": 1.0,
        "positionZ": -4.25,
        "yawDegrees": 135.0
    },
    "regions": [
        { "stableId": "nacre/coast/north", "testValue": 3 }
    ],
    "events": [
        {
            "id": "7b1f...",
            "eventType": 5,
            "elapsedWorldTime": 9.0,
            "regionId": "nacre/coast/north",
            "targetId": "nacre/dev/test-object",
            "hasMagnitude": true,
            "magnitude": 3.0,
            "parameters": [ { "id": "units-extracted", "value": 3.0 } ]
        }
    ]
}
```

Rules that keep the format readable across versions:

- `schemaVersion` is the first thing a loader checks. It is written by `SaveGameData`'s constructor and
  is never taken from the caller.
- `eventType` is the integer value of `PlayerActionEventType`. Those values are explicitly numbered;
  **never renumber or reuse them**. An unknown number is rejected, not guessed.
- Identity is always a stable ID (`nacre/coast/north`), never a scene name, GameObject name, display
  label, or array index. Regions are matched back by ID, so registration order can change freely.
- An absent optional ID round-trips as `""`, because the JSON writer does not distinguish null from
  empty. Readers treat empty as "not present".
- `savedAtUtc` and `applicationVersion` are informational metadata for developers; no gameplay decision
  may depend on them.

## Save and load behaviour

Both operations are explicit; nothing saves or loads on its own.

```csharp
LocalSaveService saveService = new LocalSaveService();           // default location and 5s write interval

// Save
SaveGameData payload = PrototypeSaveMapper.CaptureState(
    PrototypeSaveMapper.CapturePlayer(playerTransform), worldState, eventRecorder);
SaveResult saveResult = saveService.Save(payload);               // pass ignoreWriteInterval: true on quit
if (!saveResult.IsSuccess) { ShowToast(saveResult.UserMessage); }

// Load
LoadResult loadResult = saveService.Load();
if (loadResult.IsSuccess &&
    PrototypeSaveMapper.TryRestoreWorldState(loadResult.Data, worldState, freshRecorder,
        out SaveRestoreSummary summary, out string restoreError))
{
    PrototypeSaveMapper.TryApplyPlayer(loadResult.Data.Player, playerTransform, out string applyError);
}
```

What the service guarantees:

1. **Validation on both sides.** A payload is validated before it is written and again after it is
   read. Parsing proves nothing on its own: `JsonUtility` silently fills missing fields with defaults.
2. **Safe writes.** The payload is written to the `.tmp` file, flushed to the device, and only then
   swapped in with `File.Replace` (falling back to moves on filesystems that lack an atomic replace).
   An interrupted or failing write leaves the previous save intact, and the replaced contents remain in
   the `.bak` file.
3. **Missing files are normal.** `Load` on a first run returns `LoadStatus.NoSaveFile`, logs an
   informational line, creates nothing, and does not prevent the next save.
4. **Damaged saves are never silently destroyed.** Any load failure other than "no file" sets
   `IsOverwriteBlocked`; further saves return `SaveStatus.OverwriteBlocked` until an explicit,
   user-driven call to `AllowOverwriteAfterFailedLoad()`. The replaced file is still kept as `.bak`.
5. **Version mismatches are reported, not interpreted.** A file from a newer build, a retired version,
   or a file with no version is rejected with a clear message. The safe recovery path is to continue
   from default state, with `LoadBackup()` available as an explicit — never automatic — fallback.
6. **Disk writes are bounded.** Saves closer together than `MinimumSecondsBetweenSaves` (5 s by
   default) return `SaveStatus.Throttled` and touch nothing, so a per-frame caller cannot hammer the
   disk. Deliberate, rare saves pass `ignoreWriteInterval: true`. The clock is injectable, so tests stay
   deterministic.
7. **Clear development logs.** Successful saves and loads log one informational line with the path;
   failures log an error with the reason and what was left untouched. Ordinary throttling is a verbose
   log only, so it never spams the console.

Restoring is equally conservative. `PrototypeSaveMapper.TryRestoreWorldState` re-validates the payload,
refuses to restore into an event recorder that already recorded anything (two mixed histories could
break chronological order), applies region values by stable ID, and counts regions that no longer exist
as *skipped* in the returned `SaveRestoreSummary` instead of applying them somewhere else.

## Adding fields later

The format is designed to grow by addition. For a new persistent system:

1. **Own the runtime state first.** Persistence stores what a system already owns; do not invent state
   in the save model.
2. **Add a serializable record, not a reference.** Put new fields on an existing `*SaveData` record, or
   add a new `[Serializable]` record under `Wildshift.Persistence.Model` and reference it from
   `SaveGameData`. Store numbers, enums-as-integers, and stable IDs. Never store a `GameObject`,
   component, scene name, hierarchy path, or absolute filesystem path.
3. **Make it default-safe.** A field missing from an older file deserializes to `0`, `false`, `""`, or
   `null`. Choose a meaning where that default is correct, and read collections through a property that
   substitutes an empty array (see `SaveGameData.Regions`). Additive, default-safe fields do **not**
   need a new schema version.
4. **Extend the validator.** Every new field gets a rule in `SaveDataValidator`: finite numbers,
   non-blank and unique IDs, defined enum values, and bounded collection sizes (add a limit to
   `SaveSchema`). Loaded data is untrusted input.
5. **Map it in one place.** Capture and apply the field in `PrototypeSaveMapper`, so gameplay systems
   stay unaware of the file format.
6. **Bump the version only for breaking changes.** Renaming or removing a field, changing a unit, or
   changing the meaning of a value requires `SaveSchema.CurrentVersion + 1`. Until a migration step
   exists, also raise `MinimumSupportedVersion` so the old file is reported instead of misread; when a
   migration is written, leave `MinimumSupportedVersion` where it is and convert on load.
7. **Cover it with tests.** Extend the Edit Mode suite with a round trip plus at least one rejection
   case for the new field.

Out of scope for this foundation, and intentionally left for later prompts: save slots and a save menu,
autosave policy, cloud/Steam saves, compression or encryption, cross-session migration tooling, and
thread-offloaded writes.

## Tests and verification

Edit Mode tests under `Assets/_Project/Tests/EditMode/`:

| File | Covers |
| --- | --- |
| `SaveDataValidatorTests.cs` | well-formed payloads, missing schema version, non-finite player values, blank and duplicate region IDs, unknown/unset event types, out-of-order and duplicate events, negative times and magnitudes, duplicate parameter IDs, oversized histories, missing collections. |
| `LocalSaveServiceTests.cs` | save/load round trip, documented field names, missing file, malformed and empty files, newer and missing schema versions, parseable-but-invalid data, the overwrite block and its explicit release, backup creation and explicit backup loading, write throttling and the deliberate bypass, rejection of invalid payloads before any write, and that the default location sits under `persistentDataPath` without leaking the path to players. |
| `PrototypeSaveMapperTests.cs` | region IDs keyed correctly regardless of registration order, skipped retired regions, event ordering and detail preservation, absent optional IDs, newest-events budget, refusal to restore into a used recorder or to restore an invalid payload, player placement round trip (including a `CharacterController` capsule), and one full capture → save → load → restore cycle. |

Run them with the project's Edit Mode suite in Unity 6000.3.24f1:

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
      -testResults TestResults/editmode.xml -logFile -
```

Unity is not installed in the agent environment, so the Edit Mode tests must still be run in the Unity
Editor. The tests write only to a per-test temporary directory and delete it afterwards; they never
touch the real `persistentDataPath` save.
