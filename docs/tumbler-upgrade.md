# Digital instruments, seven-speed drivetrain and extended underbody

Available on `main`. Baseline: `e36e977`.

## Balanced steering

The previous steering correction approached a one-degree limit at high speed, making the vehicle reluctant to turn. The existing wheelbase/speed model now includes a smooth four-degree high-speed allowance:

```text
limit = atan(wheelbase × acceleration allowance / horizontal speed²
             + tan(high-speed steering allowance))
```

The result is still capped by the original low-speed steering envelope and retains its smoothing. Sandbox uses a 4.513 m wheelbase, 9 m/s² acceleration allowance and 4° high-speed allowance. Parking steering remains 40°. This is game handling calibration; tire grip still limits the actual path and full steering at speed can create slip or reduce speed.

Paired, two-second full-steering tests with the new drivetrain compare the previous zero-allowance model with the balanced model:

| Initial speed | Previous heading change | Balanced heading change |
| --- | --- | --- |
| 60 km/h | 39° | 67° |
| 120 km/h | 16° | 42° |
| 180 km/h | 8° | 29° |
| 240 km/h | 5° | 27° |

These are body-heading changes under controlled conditions, not guaranteed corner radii or fixed-speed handling measurements. The vehicle remains free to move and lose speed during each turn.

## Seven forward gears and speed governor

The previous five-entry, speed-scaled RPM model is replaced by seven ratios: **5.8, 4.0, 2.9, 2.25, 1.8, 1.4, 1.08**, with a 5.7 final drive. Automatic upshifts use 6600 road-coupled RPM, downshifts use 3200 RPM, and a 0.45 s cooldown prevents rapid hunting. A short 0.18 s torque reduction represents the change. Stopping allows the gearbox to return to first. Reset also clears wheel spin and returns the engine/gearbox to idle and first gear.

The simulated engine idles at **900 RPM** and has a **7200 RPM** instrument limit. While grounded, RPM follows the same road-speed coupling and selected gear used by the automatic shift thresholds. Axle wheel spin still affects traction and wheel motion, but does not independently inflate the grounded tachometer or engine pitch. While airborne, driven-wheel spin can raise RPM and automatic shifts remain suspended. During a shift, the road coupling is used while the clutch delivery is reduced. Upshifts immediately reduce engine RPM according to the new ratio, then the shared engine state continues smoothing toward its coupled value. This is a simplified game drivetrain, not measured Tumbler engine data.

The forward governor is **260 km/h**; reverse is limited to **80 km/h**. Speed and the digital speedometer use horizontal velocity so falling speed does not inflate the reading. The governor preserves vertical movement for jumping/falling and also applies while using boost. Native straight-line tests reach approximately 260.04 km/h in all three drivetrain modes without boost, with seventh engaged. The small numerical overshoot occurs within a physics step.

Available engine torque is distributed over the selected driven wheels and compensated for the different front/rear tire radii. It is no longer multiplied by selecting more driven wheels. Peak forward engine torque is configured at 1400, with an 18000 N drive-force delivery ceiling. Tire grip, suspension, body mass, center of mass, inertia, colliders, braking, handbrake forces and existing input bindings are retained. Surface profiles continue to scale tire friction.

## RPM-synchronous audio

The original engine/startup/idle/tire clips remain. Running pitch now follows `GetNormalizedEngineRPM()` rather than absolute vehicle speed, ranging from 0.65 at idle to 1.8 at the RPM limit. Idle/running levels crossfade with engine revs. The HUD reads the same engine RPM state. Native measurements confirm that every upshift reduces both RPM and pitch. Listening on the target PC remains part of personal acceptance.

## Compact digital HUD

`Assets/_Project/Prefabs/DigitalVehicleHUD.prefab` replaces the analog dial and needle. The 300 × 176 panel sits at the bottom right with 24 reference pixels of margin and safe-area inset handling. The Canvas scales from a 1920 × 1080 reference resolution.

The graphite panel contains a large speed value, KM/H units, the current forward gear or **R**, and an **AWD/FWD/RWD** chip. A numeric RPM value and twenty tachometer segments progress through green, yellow, orange and red. Only the engine instruments change RPM color; speed stays neutral for readability. The HUD images do not intercept pointer input. Existing gear/drivetrain Unity events still target `UIController`; serialized references connect that adapter to the new view.

The old analog GameObjects, needle-update code, and the exclusively referenced `dial 1.png` / `needle.png` assets and metas are removed after a project-wide reference check. Other HUD/menu imagery is retained.

These native HDRP layout previews use representative instrument values on a stationary scene; the speed-governor results above come from separate physics runs.

![Digital instruments at the RPM limit](tumbler-upgrade/hud-redline.png)

![Digital instruments at idle](tumbler-upgrade/hud-idle.png)

![HUD at 1280 × 720](tumbler-upgrade/hud-720.png)

## Extended mechanical underside

The approved five original detail groups remain. One additional visual-only prefab, `TumblerUnderbodySystems.prefab`, fills the outer floor and front-center areas:

- Side floor stampings with formed beads, sill rails, removable panels and fasteners.
- Front subframe braces, ribbed sump cover, steering-rack housing, bellows, tie rods and leading skid trim.
- Brake/service lines, retaining clips, insulated mounts and metal hardware.

Three combined meshes add approximately 3300 triangles using the existing graphite/alloy/zinc palette and one matte rubber material. The original FBX, wheel/exterior geometry, proportions, original detail meshes and materials remain unchanged. New pieces remain inside the original vehicle render envelope. No additional physics colliders are added, preserving the underbody glide fix.

Studio images use temporary inspection lighting and cameras; gameplay exposure and lighting are retained.

Before the extension:

![Original central relief](tumbler-upgrade/underbody-before.png)

After:

![Extended floor and front systems](tumbler-upgrade/underbody-after.png)

![Front mechanical detail](tumbler-upgrade/underbody-front.png)

## Verification

Close the project in Unity, then use the matching Editor:

```sh
python3 tools/verify-tumbler-update.py --unity /path/to/Unity --graphics
python3 tools/verify-driving-ground.py --unity /path/to/Unity
python3 tools/verify-chassis.py --unity /path/to/Unity
```

The current vehicle helper covers scene assets, HUD sizing/text/colors, seven-speed acceleration in FWD/RWD/AWD, RPM/pitch drops, shift cooldown, braking to first/idle, reset, keyboard/Xbox virtual inputs, proportional/parking/reverse steering, both-direction sustained turns, boost/governor behavior and vertical-velocity preservation. Optional GPU checks render eight HUD/underbody inspection images. Reports go to ignored `TestResults/TumblerUpdate/`. The helper removes temporary scripts and preserves the original scene and dynamic font asset. `verify-handling.py` delegates to this current suite; the superseded steering-only verifier is removed, while historical validation reports remain unchanged.

The [validation report](tumbler-upgrade-validation.json) records measured runs and preservation checks. The existing contact/grip and chassis suites are also rerun. Physical-controller driving, exhaustive obstacle coverage, performance profiling, player builds and subjective audio evaluation remain manual checks. An existing Unity Editor Search indexing exception can appear independently of the native assertions.

## Drive-mode RPM correction

Switching AWD → FWD under throttle could concentrate torque in the front wheels,
raise their slip speed and pin the tachometer near 7200 RPM while the gearbox was
still waiting for 6600 road-coupled RPM. The engine display/audio and the gearbox
were using different RPM references. In Unity 6000.6.7f1, a switch starting from
30 km/h reproduced 2.14 seconds above 7100 indicated RPM while the road coupling
was below 6000, with a peak mismatch of about 2599 RPM.

The grounded engine display and audio now use the gearbox's road-speed reference.
This is a correction to the simplified game drivetrain's feedback, not faster
gear ratios or a torque/grip retune: shift thresholds, cooldown, torque delivery,
wheel slip, steering, suspension, speed limits and bindings retain their settings.
The first 200 physics samples of the reproduced case have identical speed, gear
and driven-wheel RPM before and after the correction. The false redline interval
falls to zero. The airborne wheel-spin feedback is retained.

The existing native vehicle helper now exercises twelve under-throttle transitions,
cycling AWD → FWD → RWD → AWD from initial runs at 30, 60, 100 and 160 km/h.
It records per-step speed, gear, indicated/road/normalized driven-wheel RPM and
contact state in TestResults/TumblerUpdate/drive-mode-transitions.csv. It checks
false redline duration, RPM coupling, RPM/audio drops on upshift, speed limits,
and airborne RPM with shifts suspended. These controlled physics tests select
the drive mode and clear wheel torques as the production callback does; they do
not simulate physical controller presses.

[Measured correction and regression results](tumbler-rpm-validation.json) record
82 Edit Mode and 119 Play Mode checks passing in the actual installed Editor.
Real driving feel and subjective audio remain manual acceptance checks.
