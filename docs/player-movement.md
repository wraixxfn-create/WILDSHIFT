# Third-person player movement

`Wildshift.Player.Movement.ThirdPersonPlayerMovement` is the basic locomotion component for the temporary
capsule in `Assets/_Project/Scenes/Prototype.unity`. The protagonist model can replace the capsule later;
the input reader, movement controller, and camera remain separate components with Inspector-wired references.

## CharacterController approach

Movement uses Unity's `CharacterController` rather than a dynamic `Rigidbody`. Its swept capsule resolves
walls and solid obstacles during each `Move`, while explicit horizontal acceleration and gravity keep the
movement predictable without force accumulation, collision bounce, or a Rigidbody freeze/rotation setup.
The controller's **Slope Limit**, **Step Offset**, **Skin Width**, and **Center** remain ordinary Inspector
settings. The prototype uses a 45-degree slope limit, a 0.3-unit step, and a capsule centered on the placeholder.
There is no jump, dodge, stamina, combat, or damage behavior.

## Input and camera integration

Assign the Prompt 3 `PlayerInputReader` and the third-person camera's **Transform** in the movement component.
It reads `Move` as a `Vector2` and the existing `Sprint` button action (`PlayerInputButton.Sprint`); it does not
poll Unity's legacy Input Manager or create another action map. When gameplay input is disabled, the reader
returns zero movement and no held sprint state, and the character decelerates to a stop.

Movement uses the camera transform's **horizontal** forward/right axes. Camera pitch is projected away before
direction is built, so looking high or low never adds vertical movement. The combined input vector is clamped
to unit length, preventing diagonal speed gain while retaining analog input magnitude. The camera continues to
own mouse look, orbit, follow, collision, and cursor behavior; movement only reads the camera orientation.
Character facing rotates smoothly toward the intended world-space movement direction.

## Inspector tuning

`ThirdPersonPlayerMovement` exposes:

| Field | Default | Purpose |
| --- | ---: | --- |
| Input | Scene reference | `PlayerInputReader` for Move and Sprint. |
| Camera Transform | Scene reference | Horizontal orientation for camera-relative movement. |
| Walk Speed | 4 m/s | Normal movement speed. |
| Sprint Speed | 7 m/s | Sprint target speed; starts and ramps smoothly from walking speed. |
| Acceleration | 18 m/s² | Rate used to build horizontal velocity toward a faster movement target. |
| Deceleration | 24 m/s² | Rate used to reduce speed when sprint is released or movement input is lost. |
| Rotation Speed | 540 degrees/s | Maximum turn rate toward the movement direction. |
| Gravity | 25 m/s² | Downward acceleration when not grounded. |
| Ground Stick Speed | 2 m/s | Small downward velocity to maintain contact on ordinary slopes. |

The capsule root is at its center (world Y = 1 at the prototype start); the CharacterController is height 2,
radius 0.5, and center zero. `CharacterController.isGrounded` and the `Below` collision flag keep downward
velocity pinned to the ground-stick value after contact, avoiding repeated gravity accumulation and bouncing.

## Prototype scene and controls

Open `Assets/_Project/Scenes/Prototype.unity` (or enter through Bootstrap) and press Play. WASD or the arrow
keys move relative to the camera's yaw; hold **Left Shift** to sprint. Move the mouse to orbit the camera.
The scene includes a floor, wall, block, and a neutral 15-degree ramp for collision and slope checks. The
camera target remains the placeholder Transform, while the movement component uses `CharacterController.Move`.

## Verification

`Assets/_Project/Tests/EditMode/ThirdPersonPlayerMovementTests.cs` sends keyboard events through the authored
Input System asset and exercises the controller's deterministic update seam. Coverage includes:

- Forward, backward, left, and right movement.
- Diagonal speed clamping and camera pitch independence.
- Acceleration, smooth turning, sprint acceleration/release, and stopping after movement input is disabled.
- Gravity, CharacterController collision against a wall, ground settling without bounce, and traversal of a 15-degree slope.

Run the Edit Mode suite in Unity 6000.3.24f1 with:

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
  -testResults /tmp/wildshift-editmode.xml -logFile -
```

Manual Play Mode acceptance pass:

1. Walk forward/backward and strafe left/right; orbit the camera and confirm controls remain camera-relative.
2. Move diagonally and compare travel speed with a cardinal direction; it should not be faster.
3. Look up/down while walking; the capsule should remain on the ground rather than moving with camera pitch.
4. Turn while moving; the capsule should rotate smoothly toward its travel direction.
5. Hold and release Left Shift while walking; speed should ramp to sprint and smoothly return to walking speed.
6. Release all movement keys or disable Gameplay input; the capsule should decelerate and stop.
7. Walk into the wall and up the ramp; confirm the capsule cannot pass through the wall and remains grounded on the ordinary slope.
8. Stand still on the floor and slope; confirm there is no visible bounce or repeated downward jitter.

### Verification status in this agent environment

Unity Editor/player is not installed in this sandbox, so the Unity Edit Mode suite and manual acceptance pass
have **not** been run. No Unity compilation or behavioral-test pass is claimed. Run the commands above in the
specified Editor to verify C# compilation and controller physics.
