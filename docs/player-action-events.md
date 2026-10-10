# Player action event log

This is the first record of meaningful player actions on Nacre, built so a future world can respond
to patterns such as repeated violence, wildlife disturbance, resource extraction, sabotage, and
assistance to settlements. It is **only** a log. There is no adaptive AI, no ecological simulation,
no faction behaviour, no resource extraction gameplay, no cloud service, and no analytics here.
Recording an event has no side effects beyond appending a validated record to a bounded in-memory
list, and no system is notified when an event is recorded.

## Current types

| Type | Namespace | Responsibility |
| --- | --- | --- |
| `PlayerActionEventType` | `Wildshift.World.Events` | Enum of action kinds (region entry, object interaction, violence, wildlife disturbance, resource extraction, sabotage, settlement assistance, environment scan). `None` is the unset default and is rejected by the recorder. |
| `PlayerActionEventParameter` | `Wildshift.World.Events` | Immutable named numeric value attached to an event: a stable ID plus one finite `float`. |
| `PlayerActionEvent` | `Wildshift.World.Events` | Immutable, serializable record of one action. Holds only stable IDs, an enum, and numbers — never Unity object references. |
| `PlayerActionEventRecorder` | `Wildshift.World.Events` | Non-global in-memory log owner. Validates and stores events chronologically, answers queries, and keeps the history bounded. |
| `DevelopmentPlayerActionEvents` | `Wildshift.World.Events` | Development-only example events for exercising the recorder. Not gameplay; must not be wired into shipping paths. |

## The event record

`PlayerActionEvent` contains only basic, serializable fields:

| Field | Required | Notes |
| --- | --- | --- |
| `Id` | yes | Stable event ID, assigned once at creation (use `PlayerActionEvent.NewId()`). Blank or whitespace IDs are rejected. IDs must be unique among the events a recorder currently retains; once an event is trimmed out, its ID may be used again. |
| `EventType` | yes | `PlayerActionEventType` value other than `None`. Free-form strings are deliberately not used. |
| `ElapsedWorldTime` | yes | Elapsed world time in seconds from the caller's world clock; the event system owns no clock. Must be finite, non-negative, and not earlier than the previously recorded event's time. |
| `RegionId` | no | Stable region ID (same vocabulary as `RegionDefinition.StableId`), or null. Blank strings are rejected — leave the region out instead. |
| `TargetId` | no | Stable ID of the affected creature, object, structure, or settlement, or null. Blank strings are rejected. |
| `Magnitude` | no | Non-negative finite `float` amount, exposed through `HasMagnitude`/`Magnitude`. What a magnitude means for one event type is decided by the system that reacts to that type. |
| `Parameters` | no | At most `PlayerActionEventRecorder.MaxParametersPerEvent` (8) structured parameters, each with a unique non-blank ID and a finite value. |

The record is a `[Serializable]` plain C# class and copies its parameter collection at construction,
so it is a safe data-transfer boundary. IDs, enums, and numbers only: persisting or moving these
records never involves `GameObject` references, hierarchy paths, or scene object names.

## Player region attribution

Real sample-collection and environmental-scan producers resolve `RegionId` from the acting player's
`PlayerRegionAssociation` at successful action commit time. That service delegates boundaries and
overlaps to `WorldRegionLocator`; event producers do not derive IDs from target positions, display
names, or hierarchy names. If the player is outside all registered regions, lookup is unavailable,
or the integration is missing, the action is still recorded with the existing optional `RegionId`
left null. No sentinel ID is fabricated. See
[`player-region-integration.md`](player-region-integration.md).

This required no event or save schema migration: `RegionId` was already optional and persisted.
Recording still only appends a record and never mutates world, ecology, or faction state.

## The recorder

`PlayerActionEventRecorder` follows the same Try-pattern as `WorldStateService`: `TryRecord` returns
`false` with a descriptive error for every ordinary validation failure instead of throwing, and a
rejected event changes nothing. Events are stored strictly in chronological order (equal timestamps
keep insertion order), and queries return snapshots:

```csharp
var recorder = new PlayerActionEventRecorder(historyLimit: 256);

PlayerActionEvent extraction = DevelopmentPlayerActionEvents.CreateSimulatedResourceExtraction(worldClock.ElapsedTime);
if (!recorder.TryRecord(extraction, out PlayerActionEvent recorded, out string error))
{
    // Surface the validation failure; the log is unchanged.
}

IReadOnlyList<PlayerActionEvent> recentExtractions =
    recorder.GetRecentEvents(maxCount: 32, PlayerActionEventType.ResourceExtraction);
```

- `GetRecentEvents(maxCount)` returns the newest tail of the whole log, oldest first.
- `GetRecentEvents(maxCount, eventType)` filters by one event type.
- `GetRecentEvents(maxCount, regionId)` filters by stable region ID; events without a region never
  match, and a null/blank region ID yields an empty list.

Memory stays bounded: the recorder keeps at most `historyLimit` events (`DefaultHistoryLimit` is
512), dropping the oldest first, plus O(1) scalars and one retained-ID string per kept event.
`Count` reports retained events and `RecordedEventCount` reports the total accepted since creation.
The recorder is a plain C# object created and owned by a composition root or system that needs it —
it is not a singleton, holds no Unity objects, and is intended for main-thread use. It deliberately
has no subscribe/notify mechanism: reacting systems should query the log on their own cadence so
recording stays decoupled from reacting.

## Development-only examples

`DevelopmentPlayerActionEvents` builds three well-formed records for testing the log:

- `CreateTestRegionEntry` — a `RegionEntered` event for the placeholder region `nacre/dev/test-region`.
- `CreateTestObjectInteraction` — an `ObjectInteraction` event with the placeholder target
  `nacre/dev/test-object`.
- `CreateSimulatedResourceExtraction` — a `ResourceExtraction` event with a magnitude and a
  `units-extracted` parameter. This simulates the *record only*; no resource extraction gameplay is
  implemented.

These helpers exist so the event pipeline can be exercised before real gameplay emits events. Do not
reference them from shipping gameplay paths.

## Verification

Edit Mode tests live in `Assets/_Project/Tests/EditMode/PlayerActionEventRecorderTests.cs` and
`PlayerActionEventModelTests.cs`. They cover:

- chronological ordering, including equal timestamps and rejection (without side effects) of events
  earlier than the last recorded one;
- filtering by region ID and by event type, including unknown regions, blank/null region IDs, and
  undefined event-type filter arguments;
- history limits: the newest events are retained, `RecordedEventCount` keeps counting, sustained
  recording (2000 events against a limit of 16) stays bounded, and retired IDs may be reused after
  their events were trimmed while duplicates among retained events are rejected;
- invalid events: null events, blank stable IDs, undefined event types, non-finite or negative times,
  blank region/target IDs, non-finite or negative magnitudes, too many parameters, and null, blank,
  duplicated, or non-finite parameters are each rejected with a descriptive error and no log change;
- constructor and query argument guards (non-positive history limits, negative max counts);
- query snapshots are unaffected by later recording;
- `PlayerActionEvent` round-trips through `JsonUtility` with all fields intact and optional fields
  staying absent, and generated IDs are unique;
- the development-only examples validate, record, and carry the expected region, target, magnitude,
  and parameter data.

Run them with the project's Edit Mode suite in Unity 6000.3.24f1:

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
      -testResults TestResults/editmode.xml -logFile -
```

Unity is not installed in the agent environment, so the Edit Mode tests must still be run in the
Unity Editor.

## Extending later

| Future concern | Possible addition | Keep out of this foundation for now |
| --- | --- | --- |
| Reacting systems (ecology, factions, adaptive world) | Read-only consumers that query the log on their own cadence from a composition boundary. | Automatic reactions, ecosystem changes, faction strengthening, or any write into world state triggered by this layer. |
| Persistence | Durable snapshots of retained events with format/versioning in `Wildshift.Persistence`; a cross-session ID scheme if records leave the log. | Save/load code, cloud services, or analytics. |
| Region-owned history | `RegionState` collections that reference stable event IDs recorded here (see `world-state.md`). | Duplicating the log per region or coupling the recorder to `WorldStateService`. |
| New event vocabulary | New `PlayerActionEventType` values or parameter IDs when a reacting system defines the term. `EnvironmentScan` was added when the handheld scanner started recording real inspections. | Speculative event types, stringly typed kinds, or free-form payloads. |
