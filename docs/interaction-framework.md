# Basic interaction framework

This framework lets the player find and activate world objects through one shared contract. It is deliberately
minimal: it has no prompt UI, no quests, dialogue, inventory, loot, crafting, terminals, or missions. Its only
concrete object is `TestInteractable`, which logs a message and exists to validate the pipeline.

## Components

| Type | Namespace | Responsibility |
| --- | --- | --- |
| `IInteractable` | `Wildshift.Interaction` | Contract: `CanInteract(GameObject interactor)` answers availability, `Interact(GameObject interactor)` executes. |
| `PlayerInteractionDetector` | `Wildshift.Player.Interaction` | Player-side aim detection, range and line-of-sight checks, and routing of the Interact button. Owns no object state. |
| `TestInteractable` | `Wildshift.Interaction` | Validation-only object. Logs `[Wildshift] Test interaction on '...' succeeded from '...'`. Optional single-use mode. |

Detection and behaviour are separate. The detector knows nothing about what an object does; each object decides
through `CanInteract` and `Interact`. Future examination, machine operation, sample collection, and device
activation objects implement `IInteractable` without changing the detector.

## How detection works

- **Aim ray:** each frame, `PlayerInteractionDetector` casts a single ray from the view camera along its forward
  axis (`Physics.RaycastNonAlloc`, fixed buffer). There is no scene scan and no per-frame allocation.
- **Own body ignored:** colliders on the detector's own GameObject are skipped, so the player capsule cannot block
  its own aim from the third-person camera.
- **Nearest hit decides:** the nearest remaining hit is the only candidate. If it is not an enabled, active
  `IInteractable` that reports `CanInteract`, there is no target. A non-interactable hit therefore blocks line of
  sight, and so does a spent or disabled interactable.
- **Range:** measured from the detector's GameObject position to the hit point, not from the camera. The camera sits
  several metres behind the player, so measuring from it would make the effective range depend on camera distance.
  The ray length is `range + distance(camera, player)`, which cannot miss anything within range.
- **Trigger colliders** are ignored (`QueryTriggerInteraction.Ignore`). Interactables need a solid collider.
- **Fail closed:** if the fixed hit buffer saturates, the result is no target rather than a guess.
- **Destroyed or disabled targets:** the cached target is re-validated on every read. A destroyed object, a disabled
  component, or an inactive GameObject is never returned.

## Input

The detector subscribes to `PlayerInputReader.ButtonPressed` and reacts only to `PlayerInputButton.Interact`
(the existing **Interact** action, bound to **E**). A press refreshes the target, then calls `Interact` on it
if it is still valid. Holding the key does not repeat the interaction, because the reader emits one press per
press. Each new press is a new attempt; whether the object accepts it is up to `CanInteract`. When gameplay input
is disabled, for example by a menu, the reader emits no presses, so no interaction runs.

## Inspector setup

`PlayerInteractionDetector` (on the player root):

| Field | Default | Purpose |
| --- | ---: | --- |
| Input | Scene reference | `PlayerInputReader` that supplies Interact. |
| View Camera | Scene reference | Camera whose forward axis aims the ray. Use the Main Camera. |
| Interaction Range | 2.5 m | Maximum distance from the player to the target's hit point. |
| Detection Layers | Default raycast layers | Layers the aim ray can hit. Include interactable layers and environment layers that should block sight. The player's own layer can stay excluded. |

`TestInteractable`:

| Field | Default | Purpose |
| --- | ---: | --- |
| Single Use | off | When on, only the first successful interaction is accepted. |

The Prototype scene contains one `INTERACTION_TEST_Target` cube, 2 m in front of the Nacre spawn point (world `(47.5, 1, -12)`, see `nacre-graybox-region.md`), with repeatable
`TestInteractable` behaviour. It is validation scaffolding and should be removed or moved once real interactables exist.

## Verification

Edit Mode tests live in `Assets/_Project/Tests/EditMode/PlayerInteractionDetectorTests.cs`. They use real physics
raycasts and simulated keyboard input (the same technique as the existing input and movement tests), at a fixture
offset far from the graybox level. They cover:

- successful interaction runs once on an Interact press, with the player as interactor;
- repeated input: holding E does not repeat, and each new press does;
- out-of-range targets are not interacted with, and restoring range makes them valid again;
- a non-interactable wall blocks line of sight, and removing it restores the target;
- the player's own collider does not block aim;
- disabled targets (disabled behaviour and inactive GameObject) are ignored;
- a destroyed cached target returns no target and throws nothing;
- `CanInteract` returning false filters the object out;
- an aimed object with no `IInteractable` produces no target and no error;
- single-use `TestInteractable` accepts only its first interaction.

### Verification status

These tests were written in an agent environment without a Unity editor, so no Unity test run or Play Mode
check has been performed. Before relying on this:

1. Run the Edit Mode suite in Unity 6000.3.24f1 and confirm the new tests pass.
2. Open `Prototype`, enter Play Mode, and walk toward `INTERACTION_TEST_Target`. Press Interact in front of the
   target: the `Test interaction` message logs once per press. Nothing should log when you are out of range, when a
   wall is between you and the target, or when you look away.
3. Confirm the `Prototype` scene loads without missing-script warnings.
