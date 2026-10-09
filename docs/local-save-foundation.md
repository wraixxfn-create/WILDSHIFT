# Local save foundation

This is the first durable storage for WILDSHIFT: one local save file holding the small amount of
prototype state that already exists. It is **only** a save/load foundation. There is no save menu, no
slot list, no autosave, no cloud or Steam storage, no achievements, no migration tool, and no
serialization of scenes, `GameObject`s, components, or arbitrary object graphs. Nothing here changes
world, ecology, or faction state on its own; loading restores data into the systems that own it.

## Current types

| Type | Namespace | Responsibility |
| --- | --- | --- |
| `GameSaveData` | `Wildshift.Persistence` | Versioned, serializable save model (the data contract). Validates its own contents; touches no disk. |
| `PlayerSaveData` | `Wildshift.Persistence` | Saved player position and orientation as plain values. |
| `RegionSaveData` | `Wildshift.Persistence` | Saved state of one region, keyed by stable region ID. |
| `SaveLoadStatus` | `Wildshift.Persistence` | Enum describing the outcome of one save or load. This, not a path, is what UI may show. |
| `LocalSaveService` | `Wildshift.Persistence` | Owns the file: where it lives, how it is written safely, which versions are readable, and what is logged. |
| `GameSaveMapper` | `Wildshift.Persistence` | The only place that knows both sides: captures live runtime state into `GameSaveData` and applies a save back through the owning systems. |
| `SaveWriteThrottle` | `Wildshift.Persistence` | Policy that keeps periodic saving off the per-frame path. Performs no I/O. |

The data model and the service are deliberately separate. `GameSaveData` is pure data plus
validation; `LocalSaveService` is pure storage; `GameSaveMapper` is the translation layer. Nothing in
`Wildshift.World` or `Wildshift.Player` knows a save system exists — the dependency runs one way, from
persistence to the data models it persists.

## Where saves live

`LocalSaveService` defaults to `Application.persistentDataPath`, which on a Windows standalone build is
`%USERPROFILE%\AppData\LocalLow\<company>\<product>` — stable across patches, writable without
elevation, and outside the install folder. A directory can be passed explicitly for tests, tools, or a
future "saves folder" setting; the directory is created lazily on the first save, so constructing the
service touches no disk.

Three names are used inside that one directory:

| File | Purpose |
| --- | --- |
| `wildshift_save.json` | The primary save. The only file gameplay reads in the normal case. |
| `wildshift_save.json.bak` | The previous successful save, kept automatically as a recovery point. |
| `wildshift_save.json.tmp` | Transient write target. It only exists mid-save and is removed by the save path. |

`SaveDirectory`, `SaveFilePath`, and `BackupFilePath` exist for development and diagnostics (logs,
editor tooling, "open save folder"). **They must never be rendered in gameplay UI**: present a
`SaveLoadStatus` to the player instead, for example "Saved", "No save found", or "This save is from a
different version of the game". A save directory reveals the local user's account name and machine
layout, which is not the player's information to display.

## Save format

One UTF-8 (no BOM) JSON object, written with Unity's `JsonUtility` in pretty-printed form. Field names
are the serialized field names, so they carry a leading underscore. Schema version **1**:

```json
{
    "_schemaVersion": 1,
    "_savedAtUtc": "2026-10-09T15:04:05.1234567Z",
    "_gameVersion": "0.1.0",
    "_player": {
        "_position": { "x": 1.5, "y": 2.0, "z": -3.25 },
        "_orientation": { "x": 0.0, "y": 0.7071068, "z": 0.0, "w": 0.7071068 }
    },
    "_regions": [
        { "_stableId": "nacre/basin/central", "_testValue": 7 },
        { "_stableId": "nacre/coast/north", "_testValue": 41 }
    ],
    "_worldEvents": [
        {
            "_id": "6f1c0f1a9c0b4d2e9a1b2c3d4e5f6071",
            "_eventType": 1,
            "_elapsedWorldTime": 1.5,
            "_regionId": "nacre/coast/north",
            "_targetId": null,
            "_hasMagnitude": false,
            "_magnitude": 0.0,
            "_parameters": []
        },
        {
            "_id": "9a2b3c4d5e6f708192a3b4c5d6e7f809",
            "_eventType": 5,
            "_elapsedWorldTime": 9.5,
            "_regionId": "nacre/basin/central",
            "_targetId": "nacre/node/iron-1",
            "_hasMagnitude": true,
            "_magnitude": 4.0,
            "_parameters": [ { "_id": "units-extracted", "_value": 4.0 } ]
        }
    ]
}
```

| Field | Required | Notes |
| --- | --- | --- |
| `_schemaVersion` | yes | Written as `GameSaveData.CurrentSchemaVersion`. Checked before anything else is interpreted. |
| `_savedAtUtc` | yes | Round-trip ("o") UTC timestamp. Diagnostics only; gameplay must not depend on it. |
| `_gameVersion` | no | `Application.version` at save time, used to explain an incompatible save. |
| `_player._position` | yes | World-space `Vector3`. All components must be finite. |
| `_player._orientation` | yes | World-space `Quaternion`. Must be finite and a real rotation (an all-zero quaternion is rejected rather than silently applied). |
| `_regions[]` | yes (may be empty) | One entry per registered region, ordered by stable ID. Authored data (display label, initial value) is **not** saved: it belongs to the `RegionDefinition` asset. |
| `_regions[]._stableId` | yes | Same vocabulary as `RegionDefinition.StableId`; non-blank and unique within the save. |
| `_regions[]._testValue` | yes | The foundation-only mutable region value from `RegionState.TestValue`. |
| `_worldEvents[]` | yes (may be empty) | The newest `DefaultMaxWorldEvents` (128) records from `PlayerActionEventRecorder`, oldest first. |
| `_worldEvents[]._*` | — | A serialized `PlayerActionEvent` (see `player-action-events.md`). IDs, enums, and numbers only. |

Only stable IDs, enums, numbers, and small fixed-size blocks are stored. No `GameObject` references,
no hierarchy paths, no scene names, no components, and no whole scenes.

## Versioning and compatibility

`GameSaveData.CurrentSchemaVersion` is the version this build writes;
`GameSaveData.OldestSupportedSchemaVersion` is the oldest one it can still read. Both are `1` today.

On load the version is checked **before** content validation. A save outside the supported range
returns `SaveLoadStatus.IncompatibleVersion` with an explanatory error, and its bytes are deliberately
not interpreted — guessing at an unknown layout is how a save system silently turns old data into
wrong state. The file is left exactly as it was, so a build that does understand it can still read it
later.

**Adding fields safely** (the process future persistent systems should follow):

1. Add the field to the data model (`GameSaveData`, `RegionSaveData`, `PlayerSaveData`, or a new
   `[Serializable]` block) with a value that is safe when absent, and give it a private
   `[SerializeField]` backing field like the existing ones.
2. Extend `GameSaveData.TryValidate` to check it, and `GameSaveMapper` to capture and apply it
   through the owning system's own API rather than writing its state directly.
3. Keep the field optional for old files. `JsonUtility` leaves unknown fields out and missing fields
   at their default, so an additive field does not need a version bump as long as "absent" is a valid
   value.
4. Bump `CurrentSchemaVersion` only when the change is *not* additive — a renamed or retyped field, a
   changed meaning, or a newly required block. In that case, either keep reading the old version by
   lowering nothing and adding a migration step keyed on the version number, or raise
   `OldestSupportedSchemaVersion` and accept that older saves are reported as incompatible.
5. Never reuse or renumber an existing field to mean something else. Retire it and add a new one.

Ceilings that guard against corrupt or hostile files: `MaxSupportedRegions` (1024) and
`MaxSupportedWorldEvents` (4096). Both sit far above the normal working set (`DefaultMaxWorldEvents`
is 128) so raising a cap is not a breaking change.

## Validation

`GameSaveData.TryValidate` is the single gate for both directions — `TrySave` refuses to write invalid
data, and `GameSaveMapper.TryApply` refuses to apply it, in both cases before touching live state or
disk. It rejects:

- an unsupported `_schemaVersion`;
- a missing or unparseable `_savedAtUtc`;
- a missing player block, non-finite position or orientation, or a zero-length (unset) orientation;
- null region or world-event collections, too many of either, null entries, blank stable IDs, or the
  same stable ID twice;
- any world event a live `PlayerActionEventRecorder` would reject. Validation replays the stored
  history through a throwaway recorder, so a save that passes is guaranteed restorable: valid records,
  chronological order preserved, no duplicated event IDs.

Validation returns `false` with a descriptive error instead of throwing, and it never mutates the data.

## Failure handling

`LocalSaveService` never throws for an ordinary save or load problem; it returns a status and an error
string, and it logs through `WildshiftLog` (info for normal outcomes, warning for recovery, error for
failures).

| Status | Meaning | What happens on disk |
| --- | --- | --- |
| `Success` | Saved, or the primary save loaded. | Save written, or nothing written for a load. |
| `NoSaveFile` | No save exists yet. Normal on a first run; logged at info level. | Nothing written. |
| `RecoveredFromBackup` | The primary file was missing or unusable, so the previous save was loaded. Logged as a warning. | Nothing written. |
| `InvalidArgument` | Null save data. | Nothing written. |
| `InvalidData` | Parsed, but failed validation. | Nothing written; a rejected save is left untouched. |
| `MalformedData` | Empty, truncated, or not save JSON. | Nothing written; the file is left untouched. |
| `IncompatibleVersion` | Schema version outside the supported range. | Nothing written; the data is not interpreted. |
| `ReadFailed` | The file exists but its bytes could not be read. | Nothing written. |
| `WriteFailed` | The disk refused the write (missing permissions, full disk, bad path). | Any previous save is still intact. |

Two rules follow from this and are covered by tests:

- **A failed load never writes.** Loading does not repair, truncate, delete, or overwrite the file it
  failed on, so the save a player already has survives a bad read.
- **A failed write never destroys the previous save.** Saves are written to `wildshift_save.json.tmp`
  first; only after the full write succeeds is the current save moved to `.bak` and the temporary file
  renamed into place. A crash, a full disk, or an exception mid-save leaves the last good save
  readable, and the temporary file is cleaned up.

`GameSaveMapper.TryApply` applies region values through `WorldStateService` and world events through
`PlayerActionEventRecorder`, so restored data passes the same checks as live data and no saved value
bypasses the system that owns it. Saved regions this session has not registered are skipped and logged
rather than failing the load, because the registered set is authored configuration. Apply into a fresh
session: applying into a session that already holds data can stop part way through, and the returned
error says how much was restored.

## Saving without touching the disk every frame

`LocalSaveService` performs no automatic or scheduled I/O: a save happens only when a caller calls
`TrySave`. For a system that wants periodic saving, `SaveWriteThrottle` decides *when* a write is worth
doing while `LocalSaveService` still does the writing:

```csharp
// Composition root: owns the service, the throttle, and the state being saved.
var saves = new LocalSaveService();
var throttle = new SaveWriteThrottle(minimumIntervalSeconds: 30d);

// The system that changed state marks it dirty; this is not a per-frame call.
throttle.MarkDirty();

// Cheap, allocation-free, and safe to ask every frame.
if (throttle.ShouldWrite(worldClock.ElapsedTime))
{
    GameSaveData save = GameSaveMapper.Capture(
        playerTransform.position, playerTransform.rotation, worldState, eventRecorder);

    if (saves.TrySave(save, out string error) == SaveLoadStatus.Success)
    {
        throttle.MarkWritten(worldClock.ElapsedTime); // only on success, so failures are retried
    }
}
```

The throttle owns no clock — the caller passes its own world time, exactly as
`PlayerActionEventRecorder` does — and it holds no Unity objects.

## Explicit save and load

```csharp
// Save
GameSaveData save = GameSaveMapper.Capture(
    playerTransform.position, playerTransform.rotation, worldState, eventRecorder);
SaveLoadStatus status = saves.TrySave(save, out string error);

// Load (into a freshly created session)
status = saves.TryLoad(out GameSaveData loaded, out error);
if (status == SaveLoadStatus.Success || status == SaveLoadStatus.RecoveredFromBackup)
{
    if (GameSaveMapper.TryApply(loaded, worldState, eventRecorder, out PlayerSaveData player, out error))
    {
        playerBody.SetPositionAndRotation(player.Position, player.Orientation);
    }
}
else if (status == SaveLoadStatus.NoSaveFile)
{
    // First run: start from authored state, and do not treat this as an error.
}
else
{
    // Surface the status to the player. Never show the path, and never delete or rewrite the file.
}
```

There is intentionally no save UI, no slot picker, and no autosave wiring yet; the composition root
decides when these calls happen.

## Tests and verification

Edit Mode tests live in `Assets/_Project/Tests/EditMode/`:

- `LocalSaveServiceTests.cs` writes real files into a throwaway temporary directory and covers a valid
  save/load round trip; a missing save file; an empty and an unparseable file; newer (999) and older
  (0) schema versions; duplicated region IDs and a non-chronological event history; invalid and null
  data on save; the backup rotation and the absence of leftover temporary files; recovery from the
  backup when the primary file is corrupted or missing; region-ID and event-ordering consistency
  across a round trip (including replaying the restored history into a live recorder); a write that
  cannot reach the disk; the save path layout; and constructor argument guards.
- `GameSaveMapperTests.cs` covers schema/timestamp/build stamping, player transform capture, stable
  region ordering, world-event ordering and history caps, argument guards, restoring region values and
  event order into a fresh session, skipping saved regions this session has not registered, and
  rejecting an invalid or null save without changing the session.
- `SaveWriteThrottleTests.cs` covers the dirty flag, the first write, interval spacing, retrying after
  a write that was not reported as done, a non-finite clock, and interval argument guards.

Run them with the project's Edit Mode suite in Unity 6000.3.24f1:

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
      -testResults TestResults/editmode.xml -logFile -
```

Unity is not installed in the agent environment, so the Edit Mode suite still has to be run in the
Unity Editor. The new and changed C# files were syntax-checked there with a tree-sitter C# parse;
type checking and the tests themselves happen in Unity.

## Extending later

| Future concern | Possible addition | Keep out of this foundation for now |
| --- | --- | --- |
| Save slots and a save menu | A slot identifier in the file name plus a UI layer that presents `SaveLoadStatus` values. | Multiple concurrent saves in one service, or gameplay UI that shows paths. |
| Autosave and quit-save | A caller-driven schedule on top of `SaveWriteThrottle` (for example on region change or on quit). | Per-frame writes, or autosave that overwrites a save the player chose deliberately. |
| Region-owned durable data | New `[Serializable]` fields or subobjects on `RegionSaveData`, captured and applied through `RegionState`. | Serializing `RegionDefinition` assets or authored configuration. |
| Full world-event history | A larger `DefaultMaxWorldEvents`, or a rolling archive file written by a separate service. | Unbounded growth inside the main save file. |
| Cross-version migration | A migration step keyed on `_schemaVersion` that upgrades old data before validation. | Silently reading an unknown version, or renumbering existing fields. |
| Cloud / Steam storage | A second service implementation behind the same `GameSaveData` model, plus conflict rules. | Replacing the local file, or storing account data in gameplay UI. |
| Delete save | Removal of **both** the primary file and the `.bak`, so a deleted save is not recovered. | Deleting only the primary file. |
