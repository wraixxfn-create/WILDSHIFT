# Third-person camera foundation

`Wildshift.Player.Camera.ThirdPersonCamera` is a reusable component on a Unity Camera. Assign any
**Target** Transform and the Prompt 3 **PlayerInputReader** in the Inspector. The camera owns mouse look,
orbit, follow, collision, and cursor behavior; it does not implement movement. The separate
`ThirdPersonPlayerMovement` component reads the camera Transform's horizontal orientation for locomotion.
The camera is scene-owned and updates in LateUpdate, after target movement. Keep it unparented (or under an
unscaled development root). See [`player-movement.md`](player-movement.md) for the character controller.

## Behavior and tuning

- **Sensitivity** is degrees per mouse pixel (default 0.15). Mouse delta comes exclusively from
  `PlayerInputReader.Look`; it is not multiplied by frame time. Rotation is immediate, not damped.
- Separate horizontal/vertical inversion; normal mouse-up looks up. Pitch defaults to -35 through
  +75 degrees. Initial yaw/pitch do not inherit target rotation. World-up is fixed; roll defaults to
  zero and changes only through the explicit Roll field.
- **Follow Distance** defaults to 4 units; **Camera Height** is the world-up orbit-pivot offset from
  target position. The scene capsule has its origin at its center (y=1), so the scene uses height 0.5.
- **Follow Smooth Time** defaults to 0.06 seconds. Zero disables positional damping. Rotation is
  independent of this setting, avoiding mouse latency. A new/reassigned target initializes its pivot
  immediately instead of flying across the scene.
- Sphere casts shorten distance immediately against solid geometry. **Obstruction Recovery Time**
  defaults to 0.15 seconds and damps only outward recovery. No minimum distance forces the camera
  through a wall. Padding keeps a small gap from contact.
- **Collision Layers** should include environment solids and exclude the player layer. The prototype
  uses Default for solids and Ignore Raycast for the temporary target; the camera renders both.
  Target descendants and camera descendants are also filtered; triggers are ignored.
- Collision Radius is a minimum. The actual radius expands to enclose the perspective near plane
  (including its corners), avoiding near-plane clipping. The prototype uses a 0.1 near clip plane.
  This foundation is intended for perspective cameras, not orthographic projection.
- Fixed 32-element non-alloc cast/overlap buffers are reused. Saturation conservatively blocks rather
  than missing an unknown wall. Smoothed pivot motion is checked from the clear target pivot too.
  If the target pivot itself is embedded in solid geometry, the camera holds its last valid pose until
  the pivot clears; it does not guess an escape route through a wall. Targets should stay outside solids.
- Missing/destroyed targets hold the last pose without exceptions or recurring log messages. Assign
  another Transform via the Inspector or `Target` property to resume.

## Cursor/menu lifecycle

Cursor capture is optional and guarded by `Application.isPlaying`. It requires an active camera,
a target, focus, and enabled gameplay input. The previous lock and visibility state are saved on
capture and restored on disable, destruction, focus loss, missing target, or gameplay input disable.
There is no ExecuteAlways/ExecuteInEditMode behavior.

Menus continue to own input via `reader.SetGameplayInputEnabled(false)` / `true`. The reader now emits
`GameplayInputEnabledChanged` when its effective availability changes, allowing synchronous cursor
release even if the menu opens after camera LateUpdate. Input behavior and bindings are unchanged.
This camera does not implement a menu or globally pause the game.

In the standalone camera prototype, Escape toggles camera input/cursor suspension using the existing
Pause action; Escape again resumes. Editor/external cursor unlock is respected rather than immediately
relocked. After returning from focus loss, Escape or `ResumeGameplayCursor()` explicitly resumes capture.
A menu that disables gameplay input must use its own UI input to close, then re-enable the reader.
Use only one cursor-owning gameplay camera at a time; turn Capture Cursor off on secondary cameras.

## Movement prototype scene

Open `Assets/_Project/Scenes/Prototype.unity`, directly or via Bootstrap, and enter Play Mode.
The scene contains the capsule placeholder with a `CharacterController` and separate movement component,
a floor, wall, block, 15-degree ramp, neutral directional light, the input reader, and the configured Main
Camera. WASD/arrow keys now move the capsule and Left Shift sprints; mouse look continues to control only the
camera. No UI, post-processing, shake, lock-on, or cinematic effects were added. See
[`player-movement.md`](player-movement.md) for movement tuning and its verification checklist.

## Verification

Automated Edit Mode cases in `ThirdPersonCameraTests.cs` cover all seven requested checks:

| Check | Test |
| --- | --- |
| Horizontal rotation | Immediate orbit; frame-duration independence; actual rotation; real mouse events through Prompt 3 reader |
| Vertical limits | Both pitch clamps |
| Moving target | Smooth intermediate follow and convergence |
| Wall collision | Immediate contraction against a BoxCollider |
| Obstruction recovery | Intermediate outward motion and convergence after removing wall |
| Sensitivity | Changed sensitivity, zero sensitivity, both inversions |
| Missing target | Destroyed target over repeated ticks, unchanged pose, successful reassignment |

Additional tests cover height/distance, no inherited roll, triggers/self-colliders, embedded pivots,
menu look suppression, input-availability transition notifications, and no cursor changes in Edit Mode.
`Tests/PlayMode/CameraCursorTests.cs` loads the authored Prototype additively and checks capture,
synchronous menu release, resume, component disable, and target removal. Run this test in a focused
interactive Game view; it explicitly skips headless batch mode because there is no meaningful OS cursor.

Run in Unity 6000.3.24f1, using Test Runner (Window > General > Test Runner), or:

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
  -testResults /tmp/wildshift-camera-editmode.xml -logFile -
```

Manual Play Mode acceptance pass:
1. Move the mouse left/right; check responsive orbit and a level horizon.
2. Move up/down beyond both pitch limits; verify no flip.
3. Change the capsule Transform while playing; verify follow remains smooth.
4. Orbit toward the wall/block; verify camera and near plane stay on the target side.
5. Orbit away or disable the wall; verify smooth outward recovery, not a snap.
6. Change Sensitivity and both inversion fields in the Inspector; confirm changes immediately.
7. Clear/destroy Target; confirm no recurring exceptions and a stable pose, then assign a replacement.
8. Escape to release, Escape to resume; disable Gameplay input to simulate a menu. Confirm visible,
   restored cursor on disable, focus loss, and leaving Play Mode. Profile LateUpdate after warm-up
   for GC Alloc (runtime camera code uses no per-frame managed allocations).

### Verification status in this agent environment

Passed: C# syntax parsing, assembly-definition JSON parsing, scene YAML parsing, unique object IDs,
local scene reference resolution, asset GUID reference checks, unique metadata GUIDs, `git diff --check`.
Unity Editor/player is not installed here. These checks are not Unity compilation or behavioral tests;
the Edit/Play Mode suites and manual acceptance checks remain **unrun**, including visual reliability
and profiler verification. No Unity test pass is claimed.
