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
for the input action map, default bindings, reader API, and verification steps.
See [`docs/third-person-camera.md`](docs/third-person-camera.md) for the reusable camera,
neutral prototype scene, Inspector tuning, cursor/menu lifecycle, and camera verification checklist.
