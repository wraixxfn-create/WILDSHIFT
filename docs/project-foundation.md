# WILDSHIFT: THE WORLD REMEMBERS — Project Foundation

This document records the decisions and state of the initial Unity project. It covers foundation
only: no player character, creatures, terrain, combat, AI, quests, inventory, or world simulation exist yet.

## 1. Summary

| Item | Decision |
| --- | --- |
| Engine | Unity **6000.3.24f1** (Unity 6.3 LTS, revision `4e7b9b5b6244`) |
| Render pipeline | **Universal Render Pipeline (URP) 17.3.0**, Forward rendering |
| Language | C# (.NET Standard 2.1 API level) |
| Input | **Input System 1.19.0** only (legacy Input Manager disabled) |
| Target | Windows 64-bit PC, Steam distribution; IL2CPP scripting backend |
| Namespace root | `Wildshift` |
| Unity project root | this repository (`Assets/`, `Packages/`, `ProjectSettings/`) |

## 2. Unity version

Unity 6.3 LTS was chosen over the alternatives:

- **6.3 LTS (6000.3.24f1, released Sep 10, 2026)** — the current LTS patch line. Security support runs to Dec 4, 2027, with extended LTS support to Dec 4, 2028 ([release page](https://unity.com/releases/editor/whats-new/6000.3.24f1), [support dates](https://eosl.date/eol/product/unity/)).
- **6.0 LTS** — rejected. Security support ends Oct 16, 2026, which is within weeks of this project's start ([support dates](https://eosl.date/eol/product/unity/)).
- **6.6 (6000.6.x)** — rejected for a multi-year project. It is not an LTS release.

Use exactly `6000.3.24f1` when opening the project. Unity's own 6.3 branch of the Graphics repository
is the source for the URP and asset versions listed below.

## 3. Render pipeline evaluation: URP vs HDRP

WILDSHIFT's defining feature is a persistent, changing world with many interacting ecosystems, creatures and
factions. That puts a large share of the frame budget on simulation work, not only on rendering.

| Criterion | URP | HDRP |
| --- | --- | --- |
| Hardware range for a Steam PC release | Broad. URP runs on DirectX 11/12 and Vulkan on Windows, and on Linux and macOS ([Unity feature comparison](https://docs.unity.com/en-us/engine/6000.6/manual/render-pipelines/choose-a-render-pipeline/feature-comparison)). | Narrower. HDRP needs DX11, DX12 or Vulkan on Windows and does not target mobile or Switch. |
| GPU cost baseline | Lower. Suits a large open world that also runs simulation. | Higher. Built for high-end hardware. |
| High-end features (ray-traced GI, reflections, SSS, volumetrics) | Not available. | Available, and ray tracing requires DX12 with RTX-class hardware ([Unity feature comparison](https://docs.unity.com/en-us/engine/6000.6/manual/render-pipelines/choose-a-render-pipeline/feature-comparison), [6.1 docs](https://docs.unity3d.com/6000.1/Documentation/Manual/render-pipelines-feature-comparison.html)). |
| Other platforms | Also supports mobile, WebGL and Nintendo Switch. | Also supports Mac, Linux and Switch 2, but not mobile, WebGL or the original Switch. |
| Team size and shader pipeline | Simpler to maintain with a small team. | Heavier to set up and tune. |

**Decision: URP.** It fits a Steam PC release that has to scale across varied hardware, and it keeps GPU budget
free for the simulation. HDRP's extra fidelity is not required by the current scope. Switching later is
expensive, because HDRP and URP materials and shaders are not interchangeable, so this decision should be
revisited before production art begins if the visual target changes.

Graphics settings in the project:

- Forward rendering, HDR on, MSAA 2x (from the High preset), SRP Batcher on.
- Three quality tiers, `WildshiftURP_Low`, `WildshiftURP_Medium` and `WildshiftURP_High`, mapped to the
  Low, Medium and High quality levels. High is the default.
- Color space: Linear.

## 4. Packages

Declared in `Packages/manifest.json`:

| Package | Version | Why |
| --- | --- | --- |
| `com.unity.render-pipelines.universal` | 17.3.0 | Rendering pipeline (Unity 6.3 release line). |
| `com.unity.inputsystem` | 1.19.0 | Input System. Same version Unity's own 6.3-era HD template uses. |
| `com.unity.test-framework` | 1.1.30 | Edit Mode tests. First-party Unity package. |

Dependencies resolved automatically by Unity's package manager include `com.unity.render-pipelines.core`,
`com.unity.shadergraph` (17.3.0) and `com.unity.render-pipelines.universal-config` (17.0.3). Shader Graph is
included because URP depends on it.

**No third-party packages, paid assets or external dependencies were added.**

`Packages/packages-lock.json` is not committed yet. Unity generates it on first open, and it should be
committed after that first open.

## 5. Project structure

```
Assets/
  _Project/
    Art/            Environments/   Prefabs/    UI/
    Audio/          Materials/      Scenes/     VFX/
    Characters/     Data/           Settings/   Tests/
    Scripts/
      Runtime/
        Core/Bootstrap/       Bootstrap entry flow
        Core/Diagnostics/     Development logging utility
        Player/Input/         Player input reader
        Wildshift.Runtime.asmdef
      Tests/EditMode/         (Wildshift.Tests.EditMode assembly)
    Data/Input/               PlayerInputActions.inputactions
    Settings/                 BootstrapSettings.asset, WildshiftURP_*.asset,
                              WildshiftRenderer.asset, WildshiftRenderPipelineGlobalSettings.asset
ProjectSettings/    Player, Graphics, Quality, Build and editor settings
Packages/           manifest.json
docs/               This document
```

Empty folders are kept with `.gitkeep` files. Folder and asset `.meta` files are committed, which the
project's Visible Meta Files and Force Text serialization settings require.

## 6. Code organization

- **Namespace root:** `Wildshift`. Current bootstrap code lives in `Wildshift.Core.Bootstrap` and the
  development logger in `Wildshift.Core.Diagnostics`.
- **Assemblies:** `Wildshift.Runtime` (runtime code, referencing `Unity.InputSystem`) and
  `Wildshift.Tests.EditMode` (Editor-only tests, guarded by `UNITY_INCLUDE_TESTS`). Split runtime assemblies
  when feature code creates a useful dependency boundary; see [`architecture.md`](architecture.md) for
  namespace, ownership, lifecycle, and dependency rules.
- **Configuration vs runtime state:** `BootstrapSettings` and `PlayerInputActions.inputactions` are authored
  configuration. `BootstrapController` owns the bootstrap sequence's runtime state; `PlayerInputReader` owns
  a per-instance runtime copy of the input action asset and exposes input without applying gameplay behavior.

### Bootstrap and player input foundations

`BootstrapController` is the application entry point. On `Start` it:

1. Reads `BootstrapSettings.TargetSceneName` (default `Prototype`).
2. Checks that the scene is enabled in Build Settings, using `Application.CanStreamedLevelBeLoaded`.
3. Loads the target scene in Single mode. Progress is exposed through `LoadProgress`.
4. On any failure, sets `Phase = Failed`, records `FailureReason`, and logs an error through `WildshiftLog`.

It does not implement any gameplay.

### Player input (input-only)

The configured `Gameplay` map is authored in `Assets/_Project/Data/Input/PlayerInputActions.inputactions`. It
contains Vector2 Move and Look actions and ten button actions, with keyboard/mouse defaults documented in
[`player-input.md`](player-input.md). `Wildshift.Player.Input.PlayerInputReader` enables and disables the map
with its Unity lifecycle and exposes values and press/release notifications. It does not require a character
or change the Prototype scene; no movement, camera, combat, interaction, or UI behavior is included.

## 7. Scenes

| Scene | Build index | Contents | Standalone use |
| --- | --- | --- | --- |
| `Assets/_Project/Scenes/Bootstrap.unity` | 0 | Main Camera, `Bootstrap` object with `BootstrapController`. | Press Play to run the entry flow into `Prototype`. |
| `Assets/_Project/Scenes/Prototype.unity` | 1 | Main Camera, Directional Light. Empty development scene. | Open and press Play on its own. It has no dependency on Bootstrap. |

To test the entry flow, press Play in `Bootstrap`. To work on prototypes, open `Prototype` directly.

## 8. Engineering settings

- Active Input Handling: **Input System Package (New)** (`disableOldInputManagerSupport: 1`).
- Native platform backends for the new Input System: enabled.
- Scripting backend: IL2CPP for Standalone. API compatibility level: the Unity 6.3 template default.
- Product name `WILDSHIFT`. Bundle identifier `com.DefaultCompany.WILDSHIFT`. Version `0.1.0`.
- Editor serialization: Force Text, Visible Meta Files, version control mode Visible Meta Files.
- Source control: `.gitignore` excludes `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `Build/`
  and IDE/OS files. `.gitattributes` normalizes line endings.

## 9. Open items for the team

- **Company name** is still the Unity default `DefaultCompany`. Set it in Player Settings and update the
  bundle identifier before any Steam setup.
- **Display title.** `productName` is `WILDSHIFT`. The full title *WILDSHIFT: THE WORLD REMEMBERS* should be
  set in the Steam store page and marketing materials. The colon is left out because it cannot be used in file
  and executable names.
- **Generated files.** `packages-lock.json` and any editor-upgrade changes should be committed after the first open.

## 10. Verification

Unity Editor, Unity's package registry and Unity's download servers are **not reachable from this sandbox**.
The checks below therefore have not been run in the Editor yet. They must be run on a machine with
Unity 6000.3.24f1 installed.

| # | Check | Status |
| --- | --- | --- |
| 1 | Project opens in Unity 6000.3.24f1 | **Not run in Editor.** |
| 2 | Bootstrap and Prototype scenes open | **Not run in Editor.** |
| 3 | Enter and exit Play Mode | **Not run in Editor.** |
| 4 | No compilation errors | **Not run in Editor.** C# files parse without syntax errors (tree-sitter C# grammar, 0 errors). Semantic compilation still needs Unity. |
| 5 | No missing package dependencies | **Not run in Editor.** Package versions were checked against Unity's 6000.3 branch. Every external GUID in the project's URP, Shader Graph and project-settings files was checked against the URP 17.3.0 and Shader Graph sources. |
| 6 | Player input action tests | **Not run in Editor.** The Edit Mode suite now simulates keyboard state events to check Vector2 movement, button press/release, and gameplay-input gating. |

Static checks completed in the sandbox:

- All YAML files (scenes, assets, ProjectSettings) parse. Scene object references resolve within each scene.
- Every `.meta` file has a unique GUID, and every project GUID resolves to a file in the project or to a
  package asset.
- Every external GUID in the project's URP, Shader Graph and project-settings files exists in URP 17.3.0 or Shader Graph.

### Commands to run the checks

```bash
# Batch project open + Edit Mode tests (run from the repository root)
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
      -testResults TestResults/editmode.xml -logFile -
```

The Edit Mode suite (`Wildshift.Tests.EditMode`) checks that:

- `BootstrapSettings` defaults to `Prototype`.
- Build Settings list `Bootstrap` first and `Prototype` second, both enabled.
- The configured target scene is enabled in Build Settings.
- `PlayerInputReaderTests` verifies keyboard movement as a Vector2, stable Jump press/release transitions,
  and input suppression / held-button clearing when the gameplay map is disabled.

The input-only foundation and its bindings are also described in [`player-input.md`](player-input.md).
Manual check: open `Bootstrap`, press Play, and confirm the console shows no errors and the game moves into
`Prototype`. Then open `Prototype` alone and press Play.
