# World clock

The world clock is the shared foundation for "how much world time has passed". Future ecology, faction, and
schedule systems can ask it for elapsed time and subscribe to advancement events without knowing how time is
driven. It is **only** a time service: it has no day/night visuals, weather, creature schedules, ecology,
faction logic, or calendar. Its purpose is to make world time reliable, deterministic, and decoupled.

## Current types

| Type | Namespace | Responsibility |
| --- | --- | --- |
| `WorldClock` | `Wildshift.World.Clock` | Plain C# owner of elapsed world time, time scale, and pause state. Deterministic; reads no system clock. |
| `WorldTimeAdvance` | `Wildshift.World.Clock` | Immutable, read-only description of one effective advance, delivered to subscribers. |
| `WorldTimeAdvanceSource` | `Wildshift.World.Clock` | Enum saying whether an advance was `RealTime` or `Manual`. |
| `WorldClockHost` | `Wildshift.World.Clock` | Scene `MonoBehaviour` that creates the one authoritative `WorldClock` and feeds it frame time. |

The Prototype scene contains one `World Clock` GameObject with a `WorldClockHost` (defaults: running, time scale 1,
frame-delta cap 0.25 s). No other code should create a `WorldClock` for the live world.

## Time representation

- Elapsed time is stored as an integer `long` of **ticks**, where one tick is one microsecond
  (`WorldClock.TicksPerSecond` = 1,000,000). Integer storage prevents accumulated floating-point drift.
- `ElapsedTime` is a `double` in seconds derived from the tick count. It is the same unit as
  `PlayerActionEvent.ElapsedWorldTime`, so the event log can be stamped with `clock.ElapsedTime` directly.
- Each advance is multiplied out as `seconds × ticksPerSecond + carriedRemainder`. The whole ticks are added to
  the total and the fraction below one tick is carried into the next advance. Rounding error therefore never
  accumulates across frames, and sub-tick advances (for example, a 10⁻⁹ s step) are not lost.
- `MaxElapsedTicks` (`long.MaxValue / 2`, about 146,000 world years) caps the total. An advance that would exceed it throws
  `InvalidOperationException` and leaves the clock unchanged.

## Controls

| Member | Affected by pause | Affected by time scale | Notes |
| --- | --- | --- | --- |
| `AdvanceRealTime(realSeconds)` | Yes (no-op while paused) | Yes (`realSeconds × TimeScale`) | Called once per frame by `WorldClockHost`. |
| `AdvanceManually(worldSeconds)` | No | No | Exact advance for tests and development validation. Works while paused. |
| `SetTimeScale(scale)` | n/a | n/a | Finite, non-negative. Zero freezes time without pausing. Applies to later real-time advances. |
| `Pause()` / `Resume()` | n/a | n/a | Idempotent. Pausing does not raise events. |
| `RestoreElapsedTicks(ticks)` | No | No | Sets elapsed time to a saved value (Prompt 20). May move time backwards, raises no event, and clears the sub-tick remainder. Only a save restore should call it. Refused from inside a `TimeAdvanced` subscriber. |

Read-only state: `ElapsedTicks`, `ElapsedTime`, `TimeScale`, and `IsPaused`.

Invalid arguments (negative, NaN, or infinite values) are programmer errors. They throw `ArgumentOutOfRangeException`
and change nothing. Valid gameplay values never throw.

## The time-advanced event

`WorldClock.TimeAdvanced` is an `Action<WorldTimeAdvance>` event. It is raised **after** the clock has moved, only for
advances that add at least one whole tick. Paused real-time calls, zero-length advances, and sub-tick advances raise nothing.

`WorldTimeAdvance` exposes `PreviousElapsedTicks`, `ElapsedTicks`, `DeltaTicks`, `ElapsedTime`, `DeltaTime`, and
`Source`.

Subscribers must not call `AdvanceRealTime` or `AdvanceManually` from inside the callback. That call throws
`InvalidOperationException`, so events always stay in chronological order. Setting the time scale, pausing,
or resuming from a callback is allowed.

## Ownership and lifetime

- **One authority per loaded world.** `WorldClockHost.Awake` creates the clock. If another live host already exists in
  the loaded scenes, the new one logs an error through `WildshiftLog` and destroys itself. The first host to
  awaken wins.
- **Scene-scoped.** The clock is destroyed with its scene. Reloading a scene creates a fresh clock at zero, and no
  clock object survives in static or `DontDestroyOnLoad` state. Persistence of world time is out of scope here; see
  the open questions below.
- **No singleton, no static state.** Other systems get the clock through an Inspector reference to the host, or a
  constructor parameter, and read `host.Clock` from `Start` or later.
- Only the host knows about `MonoBehaviour` or frame timing. `WorldClock` itself is a plain C# object that can be
  constructed and tested directly.

## Determinism and independence from the system calendar

- The clock never reads `DateTime`, `Environment`, or any wall-clock source. Its state depends only on the
  sequence of `AdvanceRealTime`, `AdvanceManually`, `SetTimeScale`, `Pause`, and `Resume` calls. Identical
  sequences produce identical ticks and event logs.
- The host is the only place where frame time enters, through `Time.deltaTime`, clamped to
  `_maxFrameDeltaSeconds` so a long hitch cannot jump world time. Frame times differ between machines, so
  reproducible runs should call `AdvanceRealTime` or `AdvanceManually` with fixed steps instead of relying on the host.
- Because the host uses `Time.deltaTime`, setting Unity's `Time.timeScale` to 0 also stops world time. Use the clock's own
  `Pause()` or `SetTimeScale(0)` when you want to control world time only.

## How other systems use it

```csharp
using UnityEngine;
using Wildshift.World.Clock;

public sealed class ExampleTimeListener : MonoBehaviour
{
    [SerializeField] private WorldClockHost _worldClock; // Assigned in the Inspector.

    private WorldClock _subscribedClock;

    private void Start()
    {
        _subscribedClock = _worldClock.Clock;
        _subscribedClock.TimeAdvanced += OnWorldTimeAdvanced;
    }

    private void OnDisable()
    {
        if (_subscribedClock != null)
        {
            _subscribedClock.TimeAdvanced -= OnWorldTimeAdvanced;
            _subscribedClock = null;
        }
    }

    private void OnWorldTimeAdvanced(WorldTimeAdvance advance)
    {
        // React to advance.DeltaTime or read advance.ElapsedTime. Do not advance the clock from here.
    }
}
```

## Verification

Edit Mode tests live in `Assets/_Project/Tests/EditMode/WorldClockTests.cs`. They cover:

- the zero start state, including a start-paused clock;
- exact real-time accumulation, time-scale changes, and zero time scale without pausing;
- pause and resume, including idempotence;
- manual advancement that ignores pause and time scale and reports its source;
- event payloads, including ordering and suppression for zero-length, paused, and sub-tick advances;
- sub-tick carry-over (1,100 steps of 10⁻⁹ s produce exactly one tick and one event);
- drift: one hour of 60 Hz frames lands within one tick of 3,600 s;
- determinism: two clocks with the same call sequence produce identical logs;
- a hand-calculated scripted sequence;
- rejection of invalid arguments and maximum-time overflow, with the clock left unchanged;
- blocking of re-entrant advances from a subscriber, and unsubscription.

Play Mode tests live in `Assets/_Project/Tests/PlayMode/WorldClockHostTests.cs`. They verify that the host creates a clock,
advances it from frame time, holds still while paused, resumes, and destroys a second host while the first is alive.

Run the Edit Mode suite in Unity 6000.3.24f1 as described in `world-state.md`. For the host, enter Play Mode in `Prototype`
and check that the `World Clock` object's `WorldClockHost` shows no errors. Then use a temporary script that
calls `Clock.Pause()` and `Clock.SetTimeScale(2)` to check that `ElapsedTime` behaves as described above.

The agent environment has no Unity Editor, so neither suite has been run in Unity yet. The Edit Mode arithmetic and
drift expectations were cross-checked with a line-by-line numerical mirror of the advance algorithm.

## Open questions

- **World time across scene transitions.** The clock still resets when its scene is reloaded on its own. Prompt 20
  added the save side: `PrototypeSaveController` stores `ElapsedTime` in the save (`_elapsedWorldTime`) and restores
  it with `RestoreElapsedTicks` on load, never earlier than the newest saved event. A reload without a load starts
  again at zero, as before. A world-session owner that carries time across scene loads does not exist yet. See
  [`prototype-save-load.md`](prototype-save-load.md#why-the-clock-is-restored).
- **Pause and time scale for menus.** Gameplay menus should decide whether world time stops (`Pause()`) or
  continues. The clock does not make that choice.
- **Pause events.** Subscribers can read `IsPaused` but are not notified when it changes. Add an event only when a
  consumer needs it.
- **Frame-delta cap.** `_maxFrameDeltaSeconds` (0.25 s) discards excess real time after a hitch. Tune it once real frame
  pacing is measured.
