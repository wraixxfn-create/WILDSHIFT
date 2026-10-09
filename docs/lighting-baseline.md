# Nacre lighting baseline

This is the baseline lighting for the Nacre prototype region in `Assets/_Project/Scenes/Prototype.unity`. Its goal is a
stable, readable starting point for navigation, screenshots, and performance comparisons. It is not final art. There is
no cinematic lighting, weather, volumetrics, or global illumination experiment.

## Render pipeline (unchanged)

- **Universal Render Pipeline 17.3.0**, Forward rendering, Linear colour space, HDR on, MSAA 2x on High.
- Quality tiers `WildshiftURP_Low`, `WildshiftURP_Medium`, and `WildshiftURP_High`. The default quality level is High.
- Nothing about the pipeline, the renderer asset, or the quality tiers was changed. Lighting is authored in the scene and
  in the material colours.

## Scene settings (stored in `Prototype.unity`)

Lighting is serialized in the Prototype scene's `RenderSettings` and its lights, so it loads with the scene. The
bootstrap code does not write any lighting values. `BootstrapController` loads `Prototype` in Single mode, so the scene's
settings apply after that load. Searches of `Assets/**/*.cs` found no code that reads or writes `RenderSettings`, `Light`,
or the skybox.

| Setting | Value | Notes |
| --- | --- | --- |
| Ambient source (`m_AmbientMode`) | **Gradient** (was Flat) | Upward-facing surfaces take the sky colour and downward-facing surfaces take the ground colour. That helps read overhangs such as the solar panel. |
| Ambient sky / equator / ground | (0.24, 0.26, 0.29) / (0.20, 0.21, 0.22) / (0.12, 0.115, 0.11) | Was flat (0.5, 0.5, 0.5), which filled shadows too much. Intensity stays 1. |
| Skybox material | **None** | No existing sky asset is used. The background is a solid colour. |
| Background (Main Camera, Solid Colour) | (0.13, 0.15, 0.18) | Was (0.07, 0.08, 0.10). Dark enough that the pale structures stand out, and light enough that the violet landmark still has contrast against it. |
| Fog | Off | Kept off so distance does not dull the landmark or the route. |
| Environment reflections | Default (skybox mode, no skybox) | No reflection probes are used, so reflections are effectively off. This is intentional for cost. |
| Realtime / baked GI | None | The Prototype scene has no lightmaps or light probes. The ambient gradient is the only indirect term. |
| Directional light rotation | (50°, −30°, 0°), unchanged | Light direction (0.32, 0.77, −0.56), so the sun is south-east of the region and shadows fall north-west. |
| Directional light intensity / colour | 1 / white, unchanged | |
| Shadows | **Soft**, strength **0.8** (was 1.0) | Soft shadows are kept. Strength 0.8 keeps shadowed ground readable (about 0.47 in sRGB) rather than black. Shadow resolution and cascade settings come from the High URP asset: 2048 map, 2 cascades, 50 m shadow distance. |
| Post-processing | **None** | There is no Volume, and the camera has no `UniversalAdditionalCameraData` with post-processing enabled. No post-processing dependency was added. |

### Shadow casting

Ground-level meshes no longer cast shadows. They still receive them. That removes shadow passes for flat surfaces, which
would only add thin dark strips:

- `Ground_Lowland`, `Start_Pad`, `Main_Route_South`, `Main_Route_North`, `Main_Route_Plateau_A`, `Main_Route_Plateau_B`,
  `Optional_Gully_Path`, `Disturbed_Soil_Plot`.

Everything taller still casts shadows, including the ramps, the ledge, the boundary ridges, the rocks, the landmark,
the survey installation, and the player capsule.

## Palette and material changes

Two palette materials were darkened, and two objects were given different materials. Nothing else in the palette
changed.

| Object | Before | After | Reason |
| --- | --- | --- | --- |
| `NacrePearlGround` (ground) | (0.66, 0.64, 0.68) | (0.46, 0.46, 0.49) | The pale ground matched the plateau and was almost the same brightness as the route and the off-white survey structures. |
| `NacrePlateauSurface` (plateau, ramp, ledge, plinth) | (0.74, 0.72, 0.70) | (0.62, 0.60, 0.59) | Separates the plateau from the route strips and the survey structures. |
| Player capsule | `DevelopmentNeutral` (0.5 grey) | **`PlayerPlaceholder`** (0.05, 0.05, 0.06), new | Needed so the player stands out from a pale ground. `DevelopmentNeutral` is also used by the origin movement course, so it was left unchanged. |
| `INTERACTION_TEST_Target` | `DevelopmentNeutral` (0.5 grey) | **`NacreSurveyAccent`** (orange) | The grey cube would have blended into the darker ground. The orange is the region's existing marker colour. |

Unchanged, and by design: route strips (pale sand), start pad (mint), survey structure (off-white), landmark (pearl violet),
rock (slate teal), disturbed soil (dark brown-grey), and survey accents (orange).

## Readability reasoning

The palette is restrained, so most separation comes from hue, height, and shadow rather than from brightness alone.
Layers:

- **Route:** the brightest warm tone on the mid-grey ground. The route is the path cue.
- **Human-built:** the off-white survey structures and the orange markers.
- **Alien terrain:** the mid greys, the violet landmark, the teal rock, and the dark soil.
- **Player:** near-black, so it reads against every pale surface and against the background.
- **Interactive test target:** orange, so it is not confused with the grey terrain.

## Estimated contrast (model, not an editor render)

No Unity Editor is available in the sandbox, so these numbers come from a rough Lambert model. It uses the sun direction
above, the ambient gradient, shadow strength 0.8, and the palette values. Treat them as estimates. Confirm them with
screenshots.

| Pair | Contrast (luminance ratio) |
| --- | --- |
| Player vs ground (lit) | ~5.1 : 1 |
| Player vs route (lit) | ~8.5 : 1 |
| Player vs ground (shadowed) | ~2.4 : 1 |
| Route vs ground | ~1.7 : 1 |
| Plateau vs ground | ~1.3 : 1 |
| Survey structure vs plateau | ~1.4 : 1 (hue difference adds to this) |
| Landmark vs sky | ~2.5 : 1 (hue difference adds to this) |
| Interaction target vs ground | ~1.2 : 1 (hue difference adds to this) |

Lit ground is about 0.72 in sRGB, and shadowed ground is about 0.47, so nothing clips in the model. The ground-to-route
separation is the weakest pairing. It is the main thing to check on screen.

## Verification

### Done in the agent environment (no Unity Editor available)

1. The scene's YAML document count is unchanged (326 documents), every local `fileID` resolves, and every project GUID
   resolves. The new material and its `.meta` file are present with a unique GUID.
2. The only scene changes are the lighting and material references listed above. The diff was checked line by line.
3. The palette contrast estimate above was computed.

### To run in the Unity Editor (open `Prototype`)

1. Open `Prototype.unity` in Unity 6000.3.24f1. Check the Console for missing materials, missing shaders, or
   missing-reference warnings.
2. Window → Rendering → Lighting. Check Environment: Ambient Source is Gradient, the Skybox material is empty, and the
   fog is off. Check the Directional Light: Shadows is Soft, Strength 0.8, and the rotation is (50, −30, 0).
3. Enter Play Mode from the spawn. Take screenshots at the default third-person framing from:
   - the start pad, facing north (the gully) and facing east (the route);
   - the main ramp, looking up at the plateau;
   - the plateau, looking north at the landmark;
   - the gully, looking up the overlook ramp.
4. For each screenshot, confirm that the player capsule is visible, that the route strips can be followed, that the
   landmark is visible from the start, and that the orange interaction cube is visible from the spawn.
5. Check that no area is an unlit black hole and that no shadow hides a route. A shadow across the main route or the
   ramp counts as a problem.
6. Repeat the screenshots on Low and Medium to confirm that they still read clearly. Low has no main-light shadows, and
   Medium has one cascade with no soft shadows.

## Performance notes

- No new runtime work. Lighting is static scene data, with no scripts involved.
- The main light is the only real-time light. Its shadows are the main GPU cost. The High tier uses 2048 with 2 cascades
  and soft filtering. Shadow casting is off for eight flat meshes, which reduces the shadow pass draw calls.
- About 55 renderers still cast shadows: 36 scene mesh renderers (including the origin movement course, the player,
  and the taller Nacre objects) and 19 prefab instances (boundary ridges, rocks, and stakes). Count them in the Frame
  Debugger before deciding that the shadow pass needs trimming.
- No post-processing, reflection probes, lightmaps, light probes, or additional lights are used. That keeps the baseline
  stable for comparisons.
- Record the baseline with the Frame Debugger (draw calls and the shadow pass) and the Profiler (GPU time) on the same
  scene, the same camera path, and the same quality level, before and after any future lighting change.
