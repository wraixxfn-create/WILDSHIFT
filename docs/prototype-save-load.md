# Prototype save and load (Prompt 20)

This connects the local save foundation ([`local-save-foundation.md`](local-save-foundation.md)) to the Nacre
prototype scene (`Assets/_Project/Scenes/Prototype.unity`). It is a **development tool**, not a player feature:
there is no save menu, no slots, no autosave, no Steam Cloud, and no achievements. A developer presses a key to
save and another to load, and the result is shown in one line on screen.

## What is saved and restored

| Item | Saved as | Restored how | Notes |
| --- | --- | --- | --- |
| Player position and orientation | `_player._position`, `_player._orientation` | Moved with the `CharacterController` disabled, after the pose is checked for validity and obstruction | May be replaced by the safe spawn (see Recovery). |
| Current region | `_player._regionId` (or absent when outside) | Checked against the region catalog. The position decides the region; the saved ID is a claim | An unregistered ID is reported and ignored. |
| Region values | `_regions[]` | Applied to a fresh `WorldStateService` built from the catalog | Unchanged from the foundation. |
| Elapsed world time | `_elapsedWorldTime` | Restored into the scene's `WorldClock` | Never earlier than the newest saved event (see below). |
| Environmental sample collected | `_collectedSampleIds[]` (stable IDs) | Each sample's private collected flag is set, with no event and no feedback | The sample is identified by its definition's stable ID, not its object name. |
| Action history | `_worldEvents[]`, newest 128 only | Replaces the recorder's log | Bounded by `GameSaveData.DefaultMaxWorldEvents`. Never unbounded. |

Not saved: scan state of environmental scan targets, camera orbit, input state, inventory (none exists), and
anything else that the prototype does not yet store. Authored assets are never changed by a save or a load.

## Development keys

| Key | Action | Default |
| --- | --- | --- |
| **F5** | Save the session | Set on `PrototypeSaveController` as `_saveKey` |
| **F9** | Load the session | Set on `PrototypeSaveController` as `_loadKey` |

- The keys are read in editor play mode and in development builds only. The code is compiled out of other
  builds, so release builds have no save keys.
- Set either key to *None* in the Inspector to unbind it. Neither key is used by the gameplay input map
  (`docs/player-input.md`).
- The overlay at the top-left shows the key hints and the last result (for example *Saved.*, *No save was
  found.*, or *Loaded, with recovery. See the notes.*). Recovery notes appear beneath it. Paths are never shown.

Programmatic use goes through `PrototypeSaveController.SaveGame()` and `LoadGame()`. Each returns a
`SaveOperationResult` with a `Status`, a `Message`, and `Recoveries`.

## Scene wiring

Two objects were added to `Prototype.unity`. Nothing else in the scene changed.

- **`World Clock`** (the existing scene-services object, which already holds the clock and the event recorder
  hosts) has a new `PrototypeSaveController` component. Its references are: the Nacre region catalog
  (`NacreWorldRegionCatalog`), the player's `PlayerRegionAssociation` and `ThirdPersonPlayerMovement`, the
  `WorldClockHost`, the `PlayerActionEventRecorderHost`, the sample `ENV_Sample_DisturbedSoil_A`, and the safe
  spawn point below. Its minimum valid height is −20 m, and its keys are left at their defaults.
- **`NACRE_01_Safe_Spawn_Point`** is a new empty Transform under `NACRE_01_Start_Safe_Zone`. It sits at the
  player's start position, `(47.5, 1.05, −14)` in world space, facing north. It is inside the
  `nacre/frontier/survey-site` region on the start pad. Move it if the start changes.

If any reference is missing, the controller logs what is missing once at start, and each save or load then
returns `InvalidArgument` with the same explanation. It never writes a partial save.

## Load sequence

A load is staged, so a failure leaves the running session exactly as it was:

1. **Read.** `LocalSaveService.TryLoad` reads and validates the file (primary, then backup). A failure returns
   its status, and nothing else changes. A file is never written, repaired, or deleted on load.
2. **Stage.** A fresh `WorldStateService` is built from the catalog. A new event recorder is filled through
   `GameSaveMapper.TryApply` with the saved history, and the saved values are checked. Any failure here returns
   `InvalidData` before the live session is touched.
3. **Decide.** The region claim is checked against the catalog. The pose is checked for height and obstruction,
   and the body is switched off during the obstruction test so the player's own capsule does not count. The
   saved sample IDs are compared with the samples in this scene.
4. **Commit.** The staged world state and recorder replace the live ones. The world clock is set to the saved
   time, each sample's collected flag is set, and the player is moved. The region association is then
   refreshed. This all runs in one call, before the next frame, so a collected sample cannot be collected a
   second time.

A second request made while one is running (for example a second key press during a load) is refused with
`OperationInProgress`. The file layer also serializes saves and loads on the same file.

## Recovery

| Situation | Result | What the player sees |
| --- | --- | --- |
| No save file | `NoSaveFile`. Nothing changes. | *No save was found.* |
| File is empty, truncated, or not JSON | `MalformedData`. Nothing changes; the file is kept. | *The save file is not a readable WILDSHIFT save, so nothing was applied.* |
| Schema version the build does not support | `IncompatibleVersion`. Nothing changes. | *This save was written by a different version of the game…* |
| Primary is unusable but the backup is good | `RecoveredFromBackup`. The backup is loaded; the primary is kept as is. | *The newest save could not be read, so the previous save was loaded.* |
| Data fails validation (for example duplicate IDs, a non-finite position, a malformed region ID) | `InvalidData`. Nothing changes. | *The save failed validation and was not applied.* |
| Saved region ID is well formed but not in this build's catalog | Loaded. The region is taken from the position. | Recovery note: the saved region is not in this build's catalog. |
| Saved position is below −20 m, or overlaps solid geometry | Loaded. The player is placed at the safe spawn. | Recovery note: the saved position could not be used, so the player was placed at the safe spawn point. |
| Saved sample ID is not in this scene | Loaded. That ID is ignored. | Recovery note. |
| Saved region value for a region this build does not register | Loaded. That value is skipped and logged, as in the foundation. | Log warning only. |
| Saved region differs from the restored position's region | Loaded. The position's region is used. | Recovery note. |

Two cases are deliberately **not** handled by placing the player at the safe spawn. A non-finite position
or orientation means the file is corrupt, so the whole save is refused as `InvalidData`. Placing a
corrupt save at the spawn would hide the corruption. A position that is finite but unusable (below the world,
or inside geometry) is a valid file with a bad place in it, so the file loads and the player is moved.

When a save is written over an unusable primary (for example one that was corrupted on disk), the primary is
moved aside to `wildshift_save.json.unusable-<timestamp>` rather than deleted, and the backup is kept.
See [`local-save-foundation.md`](local-save-foundation.md#failure-handling).

## Why the clock is restored

The world clock belongs to the scene and starts at zero each play session. The event recorder refuses an
event that is earlier than one it already holds. Without restoring the clock, a save made late in one session
would block new events in the next session until the clock caught up. So the load sets the clock to the saved
time, or to the newest saved event if that is later, which covers any rounding in the saved value.

`WorldClock.RestoreElapsedTicks` is the only way to do this. It is not an advance. It may move time backwards,
it ignores pause, it raises no `TimeAdvanced` event, and it cannot be called from inside a subscriber.

## Obstruction check

The check is a capsule overlap with the `CharacterController`'s dimensions (radius, height, and centre, scaled by
the transform), shrunk by 5 cm so standing on the ground does not count as overlap. It ignores triggers, and
its layers default to all layers. It runs in the physics scene at load time, which is the only place these
checks happen.

## Verification

**Run in this environment:**

- Every changed or new C# file (17 runtime and test files) parses with a tree-sitter C# grammar, and so do the
  79 tracked C# files in the repository. No syntax errors.
- The scene edits were checked by script: the new Transform is in its parent's child list, the controller is in
  `World Clock`'s component list, every referenced fileID exists, and the script GUID matches its `.meta`.
- Every external API the new code calls was checked against the existing signatures in the repository.

**Not run here, and must be run in Unity 6000.3.24f1:**

- **No compile.** Unity is not installed, and the .NET SDK and Unity package sources could not be downloaded in
  this environment. Type errors are still possible, so the first Unity compile is the real check.
- **The Edit Mode suite**, including all new and changed tests. Run it as described in
  [`local-save-foundation.md`](local-save-foundation.md#tests-and-verification).
- **The manual play-mode run** below. It was not performed.

### Manual check in the Prototype scene

1. Enter play mode in `Prototype`. Note the start position. Press **F5** and expect *Saved.*
2. Walk to the disturbed-soil plot and press **E** to collect `ENV_Sample_DisturbedSoil_A`. Expect the collection
   banner.
3. Press **F5** again. Expect *Saved.*
4. Walk well away, to the far side of the plateau, and scan something with **1** so the history has an extra record.
5. Press **F9**. Expect *Loaded.* The player should be back at the second save's position and facing. The
   sample should show no prompt, and pressing **E** on it should do nothing.
6. Check the Console: the event count after the load should be one (the collection), not two. Confirm that
   `WorldClockHost` time is about the time at the second save. Scanning again should record new events without
   any ordering error.
7. Stop play, play again, and press **F9** (with a save from the first session still on disk). The clock should
   jump to the saved time, and new scans should be accepted.
8. Edge checks: delete `wildshift_save.json` and press **F9** (expect *No save was found.*). Replace it with
   `{ garbage` and press **F9** (expect *not a readable WILDSHIFT save*, with the file unchanged). Press **F5**
   while that file exists (expect *Saved.* and a `.unusable-` copy of the garbage).

The save folder is `Application.persistentDataPath` for the project (the Unity Console prints it on save).
