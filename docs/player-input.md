# WILDSHIFT Player Input Foundation

This document describes the behavior-free input reader for keyboard and mouse. The reader only exposes
input state; the separate third-person camera and movement components consume that state without moving
input responsibilities into the reader. It does not execute combat or interaction behavior or present UI.
The project uses the configured Input System package (`com.unity.inputsystem` 1.19.0).

## Action asset

`Assets/_Project/Data/Input/PlayerInputActions.inputactions` contains a `Gameplay` action map and a
`Keyboard&Mouse` control scheme. The action asset is kept separate from code so bindings can be inspected
and edited in Unity's Input Actions editor.

| Action | Type | Default binding |
| --- | --- | --- |
| Move | Value / Vector2 | W, A, S, D; arrow keys as an alternate 2D direction set |
| Look | Value / Vector2 | Mouse delta |
| Sprint | Button | Left Shift |
| Jump | Button | Space |
| Interact | Button | E |
| LightAttack | Button | Left mouse button |
| HeavyAttack | Button | Right mouse button |
| Dodge | Button | Left Ctrl |
| AbilityPrimary | Button | 1 (reused by the environmental scanner; see [`environmental-scanner.md`](environmental-scanner.md)) |
| AbilitySecondary | Button | 2 |
| AbilityTertiary | Button | 3 |
| Pause | Button | Escape |

No distinct gameplay actions share a default key or mouse button. The W/A/S/D and arrow-key bindings are
intentional alternatives for the same Move action. All input is read through actions rather than legacy
Input Manager polling, leaving the binding layer open to additional devices later.

## Runtime reader

`Wildshift.Player.Input.PlayerInputReader` is a small, behavior-free `MonoBehaviour`. Add it to a player-owned
object and assign `PlayerInputActions.inputactions` to its **Input Actions** field. It expects
a `Gameplay` map with all 12 actions above and validates the action names, action types, and expected
control types at initialization. Missing or mismatched configuration is reported to the Console with the
component as context; the Gameplay map stays disabled rather than partially enabling an invalid configuration.

In the Editor, adding the component or choosing **Reset** from its context menu assigns
`PlayerInputActions.inputactions` automatically (editor-only; runtime code never looks the asset up). If the Console reports
`has no Input Action Asset assigned`, the field on that object is empty (the Inspector shows **None**): drag
`Assets/_Project/Data/Input/PlayerInputActions.inputactions` into it and save the scene. **Missing** means the
reference points to an asset Unity cannot find, so reassign the asset the same way.

Consumers can read `Move` and `Look` as `Vector2`, poll held buttons with
`IsButtonPressed(PlayerInputButton)`, or subscribe to the generic `ButtonPressed` and `ButtonReleased`
events. A button press is emitted once on the action's performed transition and release once on its
canceled transition. Disabling gameplay input also clears held states and emits releases for any held
buttons, so listeners do not get stuck if a menu opens mid-press.

Call `SetGameplayInputEnabled(false)` while a menu should block gameplay input and pass `true` when that
menu no longer owns input. The reader also enables its action map only while its component is active and
disables the map when the component is disabled. Each reader owns a runtime copy of the assigned asset,
so enabling one reader does not mutate the imported asset or another reader. Consumers may subscribe to `GameplayInputEnabledChanged` for effective availability transitions
(including component disable); repeated requests for the same state do not emit duplicates. The camera
uses this to restore cursor state synchronously for menus. There is no per-frame polling
loop or per-frame input allocation in the reader.

## Verification

The Edit Mode tests under `Assets/_Project/Tests/EditMode/PlayerInputReaderTests.cs` load the authored
asset, add a virtual keyboard, and send keyboard state events through the Input System. They verify that
W+D produces a non-zero Vector2 Move value (with a normalized diagonal), release returns Move to zero,
Jump produces one press and one release, and disabling gameplay input suppresses reads while clearing a
held button. `ThirdPersonPlayerMovementTests.cs` separately verifies that movement consumes Move and Sprint,
clamps diagonal speed, and stops when gameplay input is disabled; see [`player-movement.md`](player-movement.md).

Run the suite in Unity 6000.3.24f1 with:

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
      -testResults TestResults/editmode.xml -logFile -
```
