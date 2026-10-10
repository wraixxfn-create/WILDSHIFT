# Environmental sample interaction (Prompt 17)

Adds one fictional, collectable environmental sample at the disturbed ecology plot in the Nacre
prototype region. The implementation is built on top of the existing Prompt 7 interaction
framework (`IInteractable` + `PlayerInteractionDetector` + the existing Interact input) without
introducing a separate interaction system.

## Scope

- One interactable sample (`ENV_Sample_DisturbedSoil_A`) placed on the sample tray at the disturbed
  ecology site (world approximately `(73.5, 0.18, -12.5)`, local to the Nacre root `(13.5, 0.18, -12.5)`).
- A single press of **Interact (E)** while aiming at it within the detector's 2.5 m range collects it.
- After collection the sample shows no prompt, rejects further interaction, and the event log
  contains exactly one record per play session.
- No inventory, crafting, quest, dialogue, or research tree. No new input actions. No persistent
  save (collection resets on scene reload, matching the existing save model).

## Components added

### Runtime code

| Type | Namespace | Purpose |
| --- | --- | --- |
| `EnvironmentalSampleDefinition` | `Wildshift.Environment.Samples` | ScriptableObject authored asset. Holds the stable ID, display name, interact prompt, and short collected description. Treated as read-only at runtime. |
| `EnvironmentalSampleInteractable` | `Wildshift.Environment.Samples` | `MonoBehaviour`, `IInteractable`, `IInteractionPromptProvider`. Per-instance runtime `_collected` flag, one-shot collection, event recording, and feedback dispatch. |
| `IInteractionPromptProvider` | `Wildshift.Interaction` | Optional contract on top of `IInteractable` that lets an object supply its prompt text without changing the detector. |
| `PlayerActionEventRecorderHost` | `Wildshift.World.Events` | Scene-owned host that creates the authoritative `PlayerActionEventRecorder` (mirrors the `WorldClockHost` pattern). |
| `InteractionPromptHud` | `Wildshift.UI` | IMGUI prompt that reads `PlayerInteractionDetector.CurrentTarget` and draws `[E] Press E to collect sample` near the bottom-centre of the screen while a valid `IInteractionPromptProvider` is aimed at. |
| `SampleCollectionFeedback` | `Wildshift.UI` | Singleton-style IMGUI panel that shows a short confirmation banner ("Collected: …" plus the authored description) for a few seconds when `EnvironmentalSampleInteractable.Interact` fires. |

### Authoring data

- `Assets/_Project/Data/Environment/Samples/NacreDisturbedSoilSample.asset` — the definition for
  the Nacre disturbed-soil scraping. Stable ID `nacre/sample/disturbed-soil-a`. Add new samples by
  creating another `EnvironmentalSampleDefinition` asset and dropping another
  `EnvironmentalSampleInteractable` in the scene — no code change, no scene-name dependency.

### Scene (Prototype.unity) changes

- Added `PlayerActionEventRecorderHost` (fileID 40004) as a component on the existing `World Clock`
  GameObject so the clock and recorder share one "scene services" object.
- Added `InteractionPromptHud` (3008) and `SampleCollectionFeedback` (3009) as components on the
  existing player GameObject (`Player Placeholder (CharacterController)`). They read the
  detector via Inspector reference so no scene lookup is required each frame.
- Added a new sample cube `ENV_Sample_DisturbedSoil_A` under `NACRE_07_Disturbed_Ecology_Site`,
  sitting on the existing `Sample_Tray`. It uses the `NacreShellShard` material (pale nacreous
  grey), a `BoxCollider` (so the detector's aim ray hits a solid collider — trigger colliders are
  deliberately ignored by `PlayerInteractionDetector`), and the
  `EnvironmentalSampleInteractable` component referencing the authored definition asset. The
  clock host and recorder host are wired via Inspector reference; the region locator is
  auto-resolved at interaction time.

## Interaction behaviour

1. **Approach + aim.** When the player looks at the sample within range and line of sight, the
   `PlayerInteractionDetector` treats it as any other valid target (same raycast and range logic
   from Prompt 7). `CurrentTarget` becomes the sample.
2. **Prompt.** Each frame `InteractionPromptHud.OnGUI` reads `CurrentTarget`; if the target
   implements `IInteractionPromptProvider` it draws the authored prompt. Looking away, stepping
   out of range, a wall blocking aim, or collecting the sample clears the prompt.
3. **Interact.** Pressing **E** invokes the detector's existing `TryInteract()`, which refreshes
   the target and calls `Interact` on it. The sample:
   - Double-checks `CanInteract` (defensive idempotency).
   - Sets `_collected = true` so subsequent `CanInteract` returns false.
   - Resolves world time from `WorldClockHost` (auto-resolved if not wired) and the containing
     region from `WorldRegionLocator` (auto-resolved).
   - Records a `PlayerActionEventType.ResourceExtraction` event with:
     - stable `id` (new GUID via `PlayerActionEvent.NewId()`),
     - `eventType = ResourceExtraction`,
     - `elapsedWorldTime` from the clock,
     - `regionId = nacre/frontier/survey-site` (from the locator),
     - `targetId = nacre/sample/disturbed-soil-a` (from the definition, never the GameObject name),
     - `magnitude = 1f`,
     - parameter `sample-collected = 1f`.
   - Calls `SampleCollectionFeedback.Instance.Show(definition)` to surface the short confirmation
     banner for a few seconds.
   - Logs a concise informational message to the console via `WildshiftLog`.
4. **Post-collection.** `CanInteract` returns false, so the detector no longer treats the cube as
   a valid target — the prompt disappears, the object still renders and collides (so later
   iterations can add a "collected" visual), and further presses of E produce nothing. No second
   event is ever recorded, even if `Interact` is called directly by another system, because the
   idempotency guard at the top of `Interact` returns immediately.

## Duplicate-collection guards

- `CanInteract` returns false after collection (detector-side guard).
- `Interact` re-checks `CanInteract` and returns before mutating state or recording an event
  (defensive guard against direct calls).
- `GetInteractionPrompt` returns null after collection (UI guard).
- The `PlayerActionEventRecorder` itself rejects duplicate IDs and out-of-order times, as a final
  safety net at the record boundary.

## Persistence

No save schema changes. Collection lives only in the `EnvironmentalSampleInteractable._collected`
field and in the in-memory `PlayerActionEventRecorder`. Scene reload (which destroys the host and
the interactable) resets both, matching the "do not make collection permanently persistent yet"
restriction.

## Replacing / duplicating samples

Because the stable ID and authored text live on the `EnvironmentalSampleDefinition` asset and
the sample is located by the detector's aim ray (not by hierarchy path), authors can:

- Move, rename, or reparent the `ENV_Sample_*` GameObject; it keeps working as long as it still
  has a collider and the component.
- Duplicate the GameObject and swap its `_definition` reference to another
  `EnvironmentalSampleDefinition` asset (with its own unique stable ID) to add a second sample
  anywhere in the world.
- Replace the mesh/cube with a prefab or art asset without changing interaction logic — the
  collider is all the detector needs.

No code references scene hierarchy names.

## Tests

Edit-mode tests: `Assets/_Project/Tests/EditMode/EnvironmentalSampleInteractableTests.cs`. They
construct the component, a definition asset (via `ScriptableObject.CreateInstance`), and a
`PlayerActionEventRecorderHost`, then verify:

- `CanInteract` is true before collection and false afterwards.
- The prompt text comes from the authored definition and becomes null once collected.
- `Interact` is idempotent: multiple calls record exactly one event, with the correct
  `ResourceExtraction` type, sample stable ID as `targetId`, magnitude `1f`, and a
  `sample-collected=1f` parameter.
- Null interactors and disabled components are rejected.
- A definition whose stable ID is blank is rejected.

The existing `PlayerInteractionDetectorTests` continue to cover aim, range, line of sight, and
input routing — the sample uses the same pipeline, so those tests apply unchanged.

## Verification checklist (to run in Editor)

1. Open `Prototype.unity`; Console should show no missing-script warnings. The new components
   should appear on the player (InteractionPromptHud, SampleCollectionFeedback), on the World
   Clock (PlayerActionEventRecorderHost), and on the small pearlescent cube at the sample tray
   inside `NACRE_07_Disturbed_Ecology_Site` (EnvironmentalSampleInteractable, referencing
   `NacreDisturbedSoilSample`).
2. Enter Play Mode. No new errors on load.
3. Walk to the disturbed ecology site and look at the small pearlescent cube on the sample tray.
   The "Press E to collect sample" prompt should appear near the bottom of the screen when you
   are within ~2.5 m and aimed at it, and disappear when you look away or step back.
4. Press **E**. The cube should:
   - Show the "Collected: Iridescent soil scraping" banner with the short description at the top
     of the screen for ~4 seconds.
   - Log a single `[Wildshift] Collected environmental sample 'Iridescent soil scraping' …` line.
   - Record exactly one event in the recorder (visible if you pause and inspect
     `World Clock → PlayerActionEventRecorderHost → Recorder` in the Inspector, or by adding a
     breakpoint in tests). The event must have `eventType = ResourceExtraction`,
     `regionId = nacre/frontier/survey-site`, `targetId = nacre/sample/disturbed-soil-a`,
     `magnitude = 1`, and a `sample-collected = 1` parameter.
5. After collection look back at the cube: no prompt appears. Pressing E again does nothing,
   logs nothing extra, and records no additional event.
6. Walk out of range (>2.5 m) and aim: no prompt, pressing E does nothing. Step back in range:
   (sample already collected — still no prompt.) Restart Play Mode to collect again.
7. Aim at the orange `INTERACTION_TEST_Target` cube near spawn — it should still log its test
   message (it does not implement `IInteractionPromptProvider`, so no prompt is drawn for it;
   this is intentional and proves the prompt UI only lights up for providers).
