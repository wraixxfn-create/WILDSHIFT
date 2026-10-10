# Player actions and world regions (Prompt 19)

This integration gives the player one current spatial region association and uses it when recording
sample collection and successful environmental scans. It composes the existing region locator with
the existing player-action event log; it does not add a second geometry algorithm, region state,
ecological consequences, or faction reactions.

## Components and ownership

| Type | Namespace | Responsibility |
| --- | --- | --- |
| `IWorldRegionContext` | `Wildshift.World.Regions` | Read-only contract used by action producers to request a stable region ID at action commit time. |
| `PlayerRegionAssociation` | `Wildshift.Player.Regions` | Player-side integration service. Queries the assigned `WorldRegionLocator`, caches the current status/stable ID, and publishes actual association transitions. |
| `PlayerRegionAssociationChange` | `Wildshift.Player.Regions` | Immutable old/new status and stable-ID transition. It distinguishes entry, exit, and direct movement between regions. |

`PlayerRegionAssociation` is attached to the player in `Prototype.unity` and has an Inspector
reference to `WorldRegionLocator_Nacre`. It owns no region definition or simulation state. In
particular, it has no reference to `WorldStateService`, `RegionState`, ecology, factions, or the
event recorder. `RegionAssociationChanged` is an observation boundary only; movement never mutates
a reacting system.

## Resolution and transition rules

The association delegates every spatial decision to `WorldRegionLocator.FindRegionAt(player
position)`. Consequently it uses the registry's established rules without duplicating them:

- box boundaries are half-open;
- higher overlap priority wins;
- equal priority uses ordinal stable-ID order;
- disabled/inactive volumes are ignored;
- a locator with invalid setup is `Unavailable` rather than "outside".

The service queries in `LateUpdate`, after the normal `Update`-based character movement. Querying is
cheap and allocation-free over the locator's already-authored volume list; there is no per-frame
scene search. The cached association and transition event update only when status or stable ID
actually changes:

| Old association | New association | Result |
| --- | --- | --- |
| none/outside | stable ID A | one entry transition |
| stable ID A | stable ID A | no update/event/log |
| stable ID A | stable ID B | one direct move transition |
| stable ID A | outside | one leave transition; current ID becomes null |
| outside | outside | no repeated update |

Transition values are `RegionDefinition.StableId` strings, never display names, asset names, or
GameObject names.

## Action attribution rule

**The player's region at successful action commit/completion is recorded.**

`TryResolveRegionForAction` always performs a fresh locator query rather than trusting the previous
frame's cache. Thus, if a future interaction begins in region A, the player crosses into region B
while it is in progress, and success is committed in B, its event records B. Cancellation records no
action. The current sample collection and scanner are immediate, but both call this same completion
seam so adding a progress phase later does not change the rule.

The region belongs to the acting player, not the target. A player can scan or collect an object over
a region boundary; the event receives the player's stable region ID at commit time. Target identity
continues to come from the sample/scan definition's stable ID.

## Outside and invalid behaviour

No sentinel such as `unknown`, no display name, and no nearest-region fallback is used.

- `Found`: the stable region ID is attached to the event.
- `OutsideAllRegions`: the successful action is still recorded, with `regionId = null` and
  `HasRegionId = false`.
- `Unavailable` (invalid locator) or a missing player region context: the successful action is still
  recorded without a region ID.
- An invalid ID returned despite locator validation is rejected by the association and treated as
  unavailable rather than entering the event log.

The association logs a missing locator as an error, and unavailable/invalid lookups as diagnostics.
An action requested while outside or unavailable emits one warning per unchanged association state;
it does not warn every frame or repeatedly for every action during the same outside stay. Ordinary
transitions use `WildshiftLog.Verbose`, which is compiled out unless verbose logging is enabled.

## Event records and compatibility

No event-log or save schema changed. `PlayerActionEvent.RegionId` was already optional and already
persisted by the existing save foundation. Existing records with a region remain compatible, and
outside-region records use the existing null representation.

The two action producers now resolve through the player context at commit time:

- `EnvironmentalSampleInteractable`: gets `IWorldRegionContext` only from the supplied interactor's
  parent chain (never a scene-wide search), then records `ResourceExtraction`.
- `PlayerEnvironmentalScanner`: uses the player object's serialized `PlayerRegionAssociation`, then
  records `EnvironmentScan`.

Neither action producer asks the target's transform which region it occupies.

## Verification

Edit Mode coverage:

- `PlayerRegionAssociationTests` walks a test player from west to east to outside, checks no repeated
  transitions within one association, checks the shared half-open boundary, and verifies a fresh
  completion-time action query.
- `EnvironmentalSampleInteractableTests` places the player and sample target in different test
  regions, checks collection follows the player's region, moves the player immediately before
  commit, and verifies an outside action has no region ID.
- `PlayerEnvironmentalScannerTests` places the player and scan target on opposite sides of a test
  boundary and verifies the scan event follows the player; it also verifies outside scans record a
  null region.

Manual Editor check in `Prototype.unity`:

1. Enter Play Mode at the normal start position (inside `nacre/frontier/survey-site`). Collect or
   scan the disturbed-soil target and confirm the event uses that stable ID.
2. Walk west past world x=40, outside the authored survey volume. Perform a successful action after
   placing/using a reachable test target and confirm the event has no region ID plus one diagnostic
   warning, not a warning every frame.
3. For a two-region walk, duplicate the test setup in a temporary scene or use the automated
   west/east fixture. Move across the shared face and confirm exactly one direct region transition;
   actions on either side carry the corresponding stable ID.
4. Temporarily overlap two test volumes with different priorities and confirm association agrees
   with `WorldRegionLocator` (higher priority, then stable-ID tie-break), rather than introducing a
   different answer. When you are done, delete the temporary volumes and run **Remove Empty Volume
   Entries** from the locator's component menu, so the rows they used do not stay behind as empty
   *Volumes* entries in the saved scene (see the troubleshooting note in
   [`world-regions.md`](world-regions.md#troubleshooting-volumes-entry-n-is-empty)).

Unity 6000.3.24f1 is required to run the Edit Mode suite.
