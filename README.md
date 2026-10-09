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
validation-only test target) is documented in [`docs/interaction-framework.md`](docs/interaction-framework.md).
