# WILDSHIFT Architecture Conventions

This document sets lightweight conventions for future code. It does **not** add gameplay systems. The goal is to let player actions, world simulation, ecology, factions, interaction, persistence, and presentation grow without turning into one tightly coupled scene or manager.

## Namespaces and placement

`Wildshift` is the root namespace. Runtime folders follow the namespace; feature subfolders add matching namespace segments (for example, `Runtime/World/Climate/` contains `Wildshift.World.Climate`). Keep types in their owning feature rather than creating broad `Common`, `Utils`, or catch-all namespaces.

| Area | Namespace | Put future code here |
| --- | --- | --- |
| Core | `Wildshift.Core` | Small cross-cutting foundations and application wiring; it must not depend on game features. Subsystems use namespaces such as `Wildshift.Core.Bootstrap` and `Wildshift.Core.Diagnostics`. |
| Player | `Wildshift.Player` | Player-specific actions, movement, and state. |
| World | `Wildshift.World` | Shared world/environment rules and world-state ownership. |
| Ecology | `Wildshift.Ecology` | Wildlife, plants, habitats, and ecological rules. |
| Factions | `Wildshift.Factions` | Human groups, relationships, and faction state. |
| Interaction | `Wildshift.Interaction` | Reusable interaction contracts and orchestration; avoid depending on a particular player or world implementation. |
| Persistence | `Wildshift.Persistence` | Save/load boundaries, durable data transfer objects, and format/version handling. |
| UI | `Wildshift.UI` | User-facing presentation and input-to-command adapters; never the authority for gameplay state. |

Tests use `Wildshift.Tests...` and remain outside runtime assemblies. The current runtime code stays in `Wildshift.Runtime`; namespaces organize code but do not, by themselves, enforce assembly boundaries. Add separate runtime asmdefs as feature code arrives when doing so gives a real dependency boundary.

## Separate the responsibilities

- **Static/authored configuration:** Put designer-authored definitions in assets under `Assets/_Project/Data/` or project-level `Settings/`. ScriptableObjects are appropriate for shared configuration, such as tuning values and definitions. Treat these assets as read-only during play. Do not use them to hold per-run health, inventory, current world changes, or other mutable state shared between instances or play sessions.
- **Runtime state:** Keep changing state in the runtime object/system that owns it. Use plain C# state objects where Unity scene behavior is unnecessary and components where Unity lifecycle or scene integration is needed. Initialize a fresh runtime state from configuration; make ownership and lifetime explicit.
- **Gameplay logic:** Put rules beside the feature that owns the state they change. Prefer small composed behaviors and collaborators to deep inheritance or all-purpose managers. Keep gameplay rules out of views and scene setup code.
- **Presentation:** UI, animation, audio, VFX, and other view code display state and forward user intent as commands. They should not become a second authority or silently implement domain rules.
- **Persistence:** Persistence translates owned runtime state to and from explicit durable data. It owns serialization details and save-format versioning, not the live game state. Persist stable identifiers and data, not `GameObject` references, hierarchy paths, or scene-specific object names.

## Ownership and lifecycle

- A creator/composition root owns objects it creates and is responsible for disposing of them. A scene owns its scene-bound components; an application-lifetime object is an explicit exception, not a default.
- Give mutable state one clear authoritative owner. Shared configuration assets are not runtime-state containers. Avoid singletons, service locators, mutable static gameplay state, and static events that can retain scene objects.
- Pass dependencies through serialized references for scene-authored components, or through constructors/factories for plain C# objects. Use explicit initialization when construction order matters; do not rely on arbitrary `Awake` ordering between unrelated objects.
- Subscribe when enabled/initialized and unsubscribe when disabled/disposed. Release owned resources in the matching teardown path. Do not use `DontDestroyOnLoad` without an explicit lifetime owner.

## Communication and dependency direction

Keep `Core` independent of feature namespaces. Feature areas may use `Core`; unrelated features should not reach into each other's implementation. Prefer this order of decisions:

1. Use a direct method or small interface when a caller needs a synchronous answer or command and the abstraction represents a real boundary.
2. Use an event for a fact that has already happened and may have multiple listeners. The publisher remains the source of truth; subscribers own their subscription lifetime.
3. Put cross-feature coordination and concrete wiring at an explicit composition/integration boundary, not in a feature's static global state.

Do not add interfaces merely to wrap every class, or events for simple one-to-one calls. If a dependency would create a cycle, move the shared contract to an appropriate stable boundary or let a composition root coordinate both sides. When modules become substantial, use asmdef references to make this one-way graph enforceable; do not split assemblies just for their own sake.

Gameplay code should receive references or stable domain identifiers, not find objects by scene name, hierarchy path, or `GameObject.name`. Names are for editor/debug presentation, not identity. Use Inspector references for scene wiring and authored/stable IDs for data that must survive scene changes or saves.

## Development logging

`Wildshift.Core.Diagnostics.WildshiftLog` is a small adapter over `UnityEngine.Debug` with `Info`, `Warning`, and `Error` methods that preserve Unity log entries, context objects, and native stack traces. Calls to `Verbose` are removed by default; define `WILDSHIFT_VERBOSE_LOGGING` in the calling assembly to include them. Prefer logging state transitions and actionable failures, never emit routine messages from per-frame methods. Do not catch or rewrite exceptions solely to log them; preserve Unity's useful exception stack traces.

## Current code

The existing bootstrap flow is in `Wildshift.Core.Bootstrap`. Its settings asset is authored configuration; its controller owns only bootstrap runtime state. The player input foundation is in `Wildshift.Player.Input`; `PlayerInputReader` owns a private runtime copy of the authored action asset, exposes values and button transitions, and does not implement gameplay. `ThirdPersonPlayerMovement` lives in `Wildshift.Player.Movement` and owns only camera-relative CharacterController locomotion; `ThirdPersonCamera` remains in `Wildshift.Player.Camera` and owns follow/orbit behavior. Both consume the reader through Inspector references without depending on each other's implementation. The basic interaction framework adds the `IInteractable` contract in `Wildshift.Interaction` and the player-side `PlayerInteractionDetector` in `Wildshift.Player.Interaction`, which consumes the reader and camera; see `interaction-framework.md`. The initial world-state foundation in `Wildshift.World` separates authored `RegionDefinition` assets from serializable, service-owned `RegionState` objects and uses stable region IDs; see `world-state.md`. The player-action event log in `Wildshift.World.Events` adds immutable, serializable `PlayerActionEvent` records and a non-global, history-limited `PlayerActionEventRecorder` that only validates, stores, and queries; recording never changes world, ecology, or faction state; see `player-action-events.md`. `Wildshift.Runtime` remains the single runtime assembly for now, and the Edit Mode assembly references it and the Input System package. The Prototype scene contains a temporary capsule, floor, wall, block, and ramp for movement/camera verification. The world-state layer only registers, retrieves, and updates a foundation test value; no world simulation, ecology, factions, quests, inventory, save system, combat, or AI is introduced. See `player-movement.md` and `third-person-camera.md` for component responsibilities and verification status.
