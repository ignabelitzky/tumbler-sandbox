# Blue surface sonar and clear headlights

Stealth (V / right-stick click) now enables a blue sonar inspired by the visual language of The Dark Knight. It reconstructs the visible scene from HDRP depth and normals, independently of lighting, rather than recoloring the camera image green. No scene exposure or world light is added.

## Sensor

- Dark navy background, blue surface relief and brighter cyan silhouettes and corners.
- Perspective-correct surface points anchored to the world, with horizontal contours on walls. Pixel-footprint filtering fades undersampled points/contours to reduce distant shimmer.
- Acoustic fronts expand from the vehicle at 60 scene units/second, repeating every three seconds, with a short echo decay. A continuous base response keeps obstacles readable between fronts.
- Range falloff preserves depth. Tangent-plane discontinuities distinguish silhouettes from a flat road receding into the distance; raw depth slope would incorrectly brighten the horizon.
- A small compressed contribution from the original image preserves material markings without allowing daylight or bright lamps to dominate the sensor.
- Pulses freeze with game time when paused. Activation retains the existing 0.18-second fade; deactivation clears the pass immediately. The pass is restricted to the current main gameplay camera, including replacement cameras. No through-wall reconstruction is attempted: visible opaque geometry supplies depth and normals.

## Headlights

| Preset | Active lamps | Range limit | Outer / inner full cone angle | Downward aim |
| --- | --- | --- | --- | --- |
| Low | Four upper | 85 (previously 38) | 88° / 58° (previously 56° / 38°) | 4° |
| High, upper | Four upper | 180 (previously 85) | 88° / 58° | 4° |
| High, lower | Four lower | 180 (previously 85) | 62° / 36° (previously 34° / 22°) | 1.2° |

Ranges are Unity light attenuation limits, not a promise of equal brightness at every distance. The upper lamps preserve foreground and side coverage when high beam is selected; the lower lamps provide additional throw. A configurable output multiplier of six retains the existing relative intensity presets while making direct illumination readable under the Sandbox’s fixed EV100 12.5 exposure (180,000 low / 540,000 high per active lamp). Shadows and optical emission remain in use. The local headlight haze is always disabled and headlight `HDAdditionalLightData.affectsVolumetric` is false, so lights illuminate surfaces without generating an artificial fog cone. Scene weather is otherwise retained.

Light cycling (L / D-pad Up), stealth's saved light preset and turbo exit behavior remain unchanged. The Sandbox scene, prefab, drivetrain, colliders, inputs and exposure assets are unchanged on disk.

## Verification

Run the existing native regression and capture helper with Unity 6000.6.7f1 and a graphics-capable Vulkan session:

```sh
python tools/sandbox-expansion/run.py StealthCameraVerify.Run --graphics
```

The helper stages the actual Sandbox tunnel and outdoor area without saving scenes. It checks the sensor lifecycle, camera transitions, zero vehicle lights during stealth, restored high-beam preset, low/high lamp counts, range, beam width, disabled volumetric scattering and disabled local haze. It renders the sonar at 720p, 1080p and ultrawide, checks blue-channel dominance and visible surfaces, verifies shader compilation, and compares two acoustic phases to ensure the pulse changes the rendered image. Headlight captures use the same tunnel camera and authored scene exposure for both presets.

Reports: `TestResults/SandboxExpansion/stealth-camera-checks.txt`; captures: `TestResults/SandboxExpansion/Previews/`. Temporary verification scripts are removed from Assets by the runner.

Static preservation checks:

```sh
python tools/verify-navigation.py
python tools/sandbox-expansion/check.py
```

The exact allowed asset hashes are updated for the three changed runtime files; unrelated assets and serialized scene objects remain protected by the existing checks. Physical controller testing, subjective driving evaluation, GPU profiling and a standalone executable remain manual checks.
