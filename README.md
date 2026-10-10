# WILDSHIFT

**WILDSHIFT: THE WORLD REMEMBERS** — an original PC action-adventure set on the alien planet Nacre in 2194.

## Project setup

- **Engine:** Unity 6000.3.24f1 (Unity 6.3 LTS)
- **Rendering:** Universal Render Pipeline 17.3.0
- **Input:** Input System 1.19.0
- **Target:** Windows PC first (Steam distribution)

Open the repository root in Unity Hub with **6000.3.24f1**. Start from `Assets/_Project/Scenes/Bootstrap.unity`
for the entry flow, or open `Assets/_Project/Scenes/Prototype.unity` to work on prototypes directly.

See [`docs/project-foundation.md`](docs/project-foundation.md) for the engine and rendering decisions,
package list, folder structure, and verification status. See [`docs/architecture.md`](docs/architecture.md)
for namespace, ownership, lifecycle, and dependency conventions, and [`docs/player-input.md`](docs/player-input.md)
for the input action map, default bindings, reader API, and verification steps. See
[`docs/player-movement.md`](docs/player-movement.md) for the temporary CharacterController capsule,
camera-relative movement, sprint tuning, and movement verification. See
[`docs/third-person-camera.md`](docs/third-person-camera.md) for the reusable camera, prototype scene,
Inspector tuning, cursor/menu lifecycle, and camera verification checklist. The temporary `Prototype`
movement course and its rapid play-mode checklist are documented in
[`docs/graybox-movement-test.md`](docs/graybox-movement-test.md). The basic interaction framework (`IInteractable`, aim detection, and the
validation-only test target) is documented in [`docs/interaction-framework.md`](docs/interaction-framework.md). The initial region identity,
runtime-state ownership, and extension guidance are documented in [`docs/world-state.md`](docs/world-state.md).
The stable region registry — authored region catalogs, validated stable IDs, scene region volumes, and position queries — is documented in [`docs/world-regions.md`](docs/world-regions.md). Player position association and completion-time region attribution for sample and scan events are documented in [`docs/player-region-integration.md`](docs/player-region-integration.md).
The player-action event log — serializable `PlayerActionEvent` records, a bounded non-global
`PlayerActionEventRecorder`, and development-only example events — is documented in
[`docs/player-action-events.md`](docs/player-action-events.md). The local save foundation — a versioned
save model, a safe local save/load service, backup recovery, and development logs — is documented in
[`docs/local-save-foundation.md`](docs/local-save-foundation.md). The development save/load integration for the Nacre prototype — the F5 save and F9 load keys, staged restore of the player pose, region claim, world clock, sample collection, and bounded event history, and the recovery rules — is documented in [`docs/prototype-save-load.md`](docs/prototype-save-load.md).
The world-time foundation — a deterministic, scene-owned `WorldClock` with pause, time scale, manual advancement, and a time-advanced event — is documented in [`docs/world-clock.md`](docs/world-clock.md).
The first Nacre graybox region — a small survey-site blockout with a start pad, main ramp route, optional gully, landmark, survey installation, and a disturbed ecology plot, registered as `nacre/frontier/survey-site` — is documented in [`docs/nacre-graybox-region.md`](docs/nacre-graybox-region.md). Its playable footprint, spawn clearance, routes, crevices, and traversal rules are in the section *Boundaries, spawn, and traversal rules* of the same file. The baseline lighting for that region (URP sun and shadows, ambient gradient, background, and the palette changes that separate terrain, route, landmark, and player) is documented in [`docs/lighting-baseline.md`](docs/lighting-baseline.md). The reusable environmental props for that region — the prop prefabs, the prop groups, the collision policy, the placement rules, and the optional editor audit tool — are documented in [`docs/nacre-graybox-props.md`](docs/nacre-graybox-props.md). The first collectable environmental sample at the disturbed ecology plot is documented in [`docs/environmental-sample-interaction.md`](docs/environmental-sample-interaction.md). The handheld environmental scanner — Ability Primary, scan targets, range and line of sight, and event-log recording — is documented in [`docs/environmental-scanner.md`](docs/environmental-scanner.md).
