# Minimal environmental scanner (Prompt 18)

Adds a handheld environmental scanner so the player can inspect a designated scan target —
starting with the collected-sample location at the disturbed ecology plot — instead of only
moving through the graybox. Scanning is a separate mechanic from Interact: it reuses an existing
ability action, the existing camera raycast conventions, the world-region locator, and the
player-action event log.

## Scope

- Press **Ability Primary (keyboard 1)** while aiming at a valid `IScanTarget` within 8 m to
  scan it.
- The Nacre disturbed-soil sample (`ENV_Sample_DisturbedSoil_A`) is the first scan target. It
  remains scannable after collection.
- Readable IMGUI feedback for a successful scan, an invalid target, and an out-of-range target.
- A successful first scan records one `PlayerActionEventType.EnvironmentScan` event with the
  target's stable ID and the acting player's stable region ID at successful commit time.
- Repeat scans of the same target are allowed (the player can re-read the result) but are **not**
  recorded again unless `Record Repeat Scans` is enabled.
- No weapon behaviour, codex, quest rewards, upgrade tree, or scanning minigame. No new packages.

## Input assumption

The project already had unused ability actions. The scanner **reuses `AbilityPrimary`** (bound to
**1** in `PlayerInputActions.inputactions`) rather than adding a new action or duplicating the
input asset. Interact (**E**) is unchanged and still collects the sample. The scanner component
exposes a `Scan Button` field defaulting to `AbilityPrimary` so a later ability shuffle can point
at `AbilitySecondary` / `AbilityTertiary` without editing the asset.

Light Attack, Heavy Attack, and Dodge are left unused; they remain reserved for combat.

## Components

| Type | Namespace | Responsibility |
| --- | --- | --- |
| `IScanTarget` | `Wildshift.Interaction` | Contract: stable target ID, display name, description, scan result, availability. Independent of `IInteractable`. |
| `EnvironmentalScanDefinition` | `Wildshift.Environment.Scanning` | ScriptableObject authored asset. Read-only at runtime. |
| `EnvironmentalScanTarget` | `Wildshift.Environment.Scanning` | Scene component that implements `IScanTarget` from a definition. |
| `PlayerEnvironmentalScanner` | `Wildshift.Player.Scanning` | Player-side aim ray, range and line-of-sight checks, repeat policy, and event recording. Owns no presentation. |
| `PlayerRegionAssociation` | `Wildshift.Player.Regions` | Shared player-position-to-stable-region integration used at scan commit time; see [`player-region-integration.md`](player-region-integration.md). |
| `ScanAttemptResult` / `ScanAttemptOutcome` | `Wildshift.Player.Scanning` | Immutable scan outcome consumed by UI. No Unity object references. |
| `EnvironmentalScannerFeedback` | `Wildshift.UI` | IMGUI listener for `ScanAttempted`. Replaceable without rewriting scan logic. |

### Authoring data

- `Assets/_Project/Data/Environment/Scanning/NacreDisturbedSoilScan.asset` — scan definition for
  the disturbed-soil scraping. Stable ID `nacre/sample/disturbed-soil-a` (the same ID as the
  collectable sample, so collection and scan events correlate). Add further targets by creating
  another definition asset and dropping another `EnvironmentalScanTarget` on a solid collider.

## How detection works

The scanner copies the interaction detector's raycast rules (see `interaction-framework.md`):

- **Aim ray:** one `Physics.RaycastNonAlloc` from the view camera along its forward axis. No
  scene scan, no per-frame work (the ray runs only when the scan button is pressed).
- **Own body ignored:** colliders on the scanner's GameObject are skipped.
- **Nearest hit decides:** the nearest remaining hit is the only candidate. A wall, the floor, or
  any object that is not an enabled `IScanTarget` is an invalid target. Solid obstacles always
  block scans.
- **Range:** measured from the player to the hit point, not from the camera. The ray length is
  `probeRange + distance(camera, player)` so an in-range object cannot be missed, and a farther
  scan target can still be classified as out of range instead of "nothing to scan".
- **Trigger colliders** are ignored. Scan targets need a solid collider.
- **Fail closed:** a saturated hit buffer yields an invalid target.

Default ranges on the Prototype player: **scan range 8 m**, **probe range 24 m**.

## Repeat-scan policy

| Inspector field | Default | Behaviour |
| --- | --- | --- |
| Allow Repeat Scans | on | The player may scan the same target again and see its authored result. When off, further in-range scans of that target ID return `RepeatRejected` ("Already scanned."). |
| Record Repeat Scans | off | Only the first successful scan of each target ID is written to the event log. When on, every successful scan appends a new event. |

Both flags are session-scoped on the scanner component. Reloading the scene clears the scanned-ID
set, matching the existing in-memory event log.

## Event record

A successful recorded scan appends:

- `eventType = EnvironmentScan`
- `elapsedWorldTime` from `WorldClockHost`
- `regionId` from `PlayerRegionAssociation.TryResolveRegionForAction` at the player's position when the scan succeeds (`nacre/frontier/survey-site` in the prototype); null if the player is outside registered regions
- `targetId` from the scan definition (never the GameObject name)
- `magnitude = 1`
- parameter `environment-scan = 1`

Recording uses the existing `PlayerActionEventRecorderHost` on the World Clock object. Invalid,
out-of-range, and unlogged repeat attempts do not touch the log.

## Feedback (presentation only)

`EnvironmentalScannerFeedback` subscribes to `PlayerEnvironmentalScanner.ScanAttempted` and maps:

| Outcome | Player-facing copy |
| --- | --- |
| Success | `Scan: {display name}` plus the authored description and scan result |
| InvalidTarget | `Nothing to scan.` |
| OutOfRange | `{display name} is out of range.` (or `Target is out of range.`) |
| RepeatRejected | `Already scanned.` |

Replacing this IMGUI panel later means subscribing to the same event; scan logic does not
reference the UI type.

## Scene (Prototype.unity)

- `PlayerEnvironmentalScanner` (3010), `EnvironmentalScannerFeedback` (3011), and
  `PlayerRegionAssociation` (3012) on the existing player object. The scanner is wired to the
  association; the association is wired to the existing Nacre region locator.
- `EnvironmentalScanTarget` (2003007) on `ENV_Sample_DisturbedSoil_A`, referencing
  `NacreDisturbedSoilScan`.

The orange `INTERACTION_TEST_Target` is not a scan target: aiming at it and pressing **1** shows
"Nothing to scan." Collecting the sample with **E** does not remove the scan target.

## Tests

Edit Mode tests:

- `Assets/_Project/Tests/EditMode/PlayerEnvironmentalScannerTests.cs` — valid scan via Ability
  Primary (**1**), Interact does not scan, invalid target, empty aim, range restore, solid
  obstacle blocking line of sight, own collider ignored, disabled/unavailable targets, default
  repeat scans not flooding the log, optional repeat recording, optional repeat rejection, and
  player-region attribution through `PlayerRegionAssociation` (including player/target regions
  differing and the player being outside every region).
- `Assets/_Project/Tests/EditMode/EnvironmentalScanTargetTests.cs` — authored fields, blank ID,
  missing definition, disabled component.

Unity is not installed in the agent environment, so the Edit Mode suite must still be run in
Unity 6000.3.24f1.

## Verification checklist (to run in Editor)

1. Open `Prototype.unity`. No missing-script warnings. The player has `PlayerEnvironmentalScanner`
   and `EnvironmentalScannerFeedback`; the sample cube has `EnvironmentalScanTarget` referencing
   `NacreDisturbedSoilScan`.
2. Enter Play Mode. Walk to the disturbed ecology site and look at the pearlescent cube on the
   sample tray. Press **1** within ~8 m: the scan panel shows the display name, short description,
   and scan result. The console logs one scan line. The recorder holds one `EnvironmentScan`
   event with `targetId = nacre/sample/disturbed-soil-a` and `regionId = nacre/frontier/survey-site`.
3. Press **1** again on the same target: the panel shows the result again, and the log still has
   exactly one scan event.
4. Aim at the orange test cube, the ground, or empty sky and press **1**: "Nothing to scan.", no
   new event.
5. Stand more than 8 m from the sample with a clear line of sight and press **1**: out-of-range
   feedback, no event. Walk closer and scan successfully.
6. Put a solid prop between the camera and the sample and press **1**: invalid target, no event.
   Step around the prop and scan successfully.
7. Collect the sample with **E**. Press **1** on the same cube: it still scans. Collection and
   scan remain separate events (`ResourceExtraction` vs `EnvironmentScan`).
