# Tumbler impact physics

Current extension: [side/roof scrape grip](rollover-friction.md) adds runtime friction selection for overturned landings. The measurements below describe the earlier contact-face calibration.

Inspected main: `5713507`, Unity **6000.6.7f1**. Work is isolated on `fix/tumbler-impact-physics`.

## Diagnosis from the actual scene

The car is 3000 kg, with explicit COM `(0.010406583,-0.11552851,-0.21503125)` and principal inertia `(7140.4707,9344.217,3083.037)` kg·m², principal-axis rotation approximately 3.64° about X. Local X is primarily pitch, Y yaw, Z roll. These values already describe a wide heavy vehicle; changing mass alone cannot correct a contact lever or a discontinuous tire contact.

COM is within the chassis, below its visual center. At settled ride height the lower hull clearance in the test fixture is approximately **0.236 m**, rather than the 0.30 m inferred from the scene's initial suspended pose. The existing 16-vertex beveled convex underbody and its minimum-combine .04/.025 friction material remain untouched, as do the upper collider and all visual dimensions.

**All six `EasySuspension` components are disabled**, attached individually to the wheel objects, not the vehicle root. Their serialized frequency 10, damping ratio 1.8 and shift .3 are inactive. Enabling them as written would call `GetComponent<Rigidbody>()` on a wheel without a Rigidbody. They do not recalculate the effective suspension in this scene. No blanket rewrite or enabling of this legacy component is appropriate.

Effective springs are **90000 N/m**, dampers **4500 N·s/m**, front travel **0.2 m**, rear **0.5 m**, target .5, force application distance **0**. Sprung masses are ~637/642 kg front and ~427…434 kg per rear wheel. Effective damping ratios are approximately .296 front / .36 rear, with angular natural frequency ~11.9 / 14.5 rad/s (1.9 / 2.3 Hz). See the committed setup report for all six values.

The wheel-base application points are well below COM: horizontal tire forces therefore have long pitch/roll levers. The short-travel front suspension also encounters a sudden ray-contact height increase at a vertical edge. Native trials confirm contact-dependent upward/pitch transients, particularly the 0.50 m edge at the nominal 30 km/h injection. This is a measured response in the new geometry, not proof that every user-reported violent launch has the same cause.

Global solver remains 6/1, timestep .02 s, default contact offset .01 m. Original body uses Continuous detection, drag/angular drag .05 and depenetration limit 10 m/s. No unexpected penetration is proven by these experiments; lowering depenetration is a local safety limit, not a claimed root-cause diagnosis.

Motor torque is limited by the seven-speed drivetrain and maximum drive force. Boost delivers a forward impulse equivalent to ~6000 N at .02 s. Both can continue to load a contact when held; existing downforce is 10×speed N in the scene and also acts airborne. None of those systems is altered. Measurements isolate **coasting impacts** without motor, brakes, turbo, or downforce, so they do not establish powered-impact behavior.

## Experiment

The committed baseline and final reports contain the same poses, obstacle geometries and injected velocities (10/30/60/100 km/h), 3 s settling followed by reanchoring to the measured flat-platform pose (Y .9708383), .02 s native physics, and no constraints. This removes settling drift; initial positions match within 1e-5 m and orientations match in all 81 pairs. A 2 s window follows the near-obstacle approach reference; jump windows are 5 s at nominal 30 / 3 s at nominal 60 to include takeoff/landing; timeout is 10 s. Actual reference speeds differ from injected speeds because tire spin starts at zero and the car coasts before impact. Compare paired trials, not the nominal speed alone.

`preKph` is measured at the approach reference (9.5 m after the canonical start), `postKph` 0.2 s later, and **`finalKph` is the posterior velocity at the observation end**. The reference is 3.5 m before low-obstacle centers; the frontal-wall run starts 15 m before its center, and the jump reference is on the ramp. The +0.2 s sample can precede actual contact at low speed, so use finalKph for the after-impact comparison. If an obstacle stops the vehicle before the approach reference, preKph retains the injected-speed fallback and postKph remains zero (no sample); these cases must not be read as measured reference speeds. These are defined sampling windows, not exact first-contact telemetry; the bevel begins earlier than the vertical edge. `maxVertical`, COM altitude/rise, pitch/roll Euler maxima, angular speed, minimum/final grounded-wheel counts and native peak collision impulse cover the whole window. Euler pitch/roll are diagnostic, not a complete orientation decomposition during a rollover. Impulse excludes tire forces. Outcome labels are heuristic and do not constitute acceptance assertions.

The high-damping candidate (.85 critical ratio) is archived as a **rejected** experiment: small step vertical speed/angular peaks worsened even where maximum pitch decreased. This demonstrates why increasing damping or mass blindly is insufficient.

## Calibration and reversal

The dedicated `TumblerImpactPhysics` component applies local settings once when enabled; it does not run an Update/FixedUpdate loop. Disable it to restore its captured original settings. Disabling the independent circuit has no effect on the vehicle calibration. Remove the dedicated scene component and script to revert the vehicle change; all original serialized body and wheel values are retained.

Do not enable legacy EasySuspension in parallel. If mass or geometry changes later, remeasure sprung masses and COM before adjusting application height. Retain the original force-application heights and free body axes and compare low-step, oblique, wall, jump and drop trials. No pose correction, velocity clamp, extra downward force, frozen axis or collision suppression is used in gameplay.

## Validation limits

Native asset/contact/impact tests are described below. Physical Xbox driving, subjective handling, full throttle/boost impacts, maximum-speed acceleration, audio/visual acceptance, performance on target hardware and player builds still require manual testing. Coasting-impact data is not a claim that all driving features passed.

## Selected correction

Changing force-application height and changing COM/inertia were rejected after they produced a larger launch at the 0.50 m / nominal 60 km/h edge. Solver-only and depenetration-only experiments did not resolve the launch. The original mass, COM, inertia, damping, wheel travel and force points are therefore retained.

The confirmed geometric influence is the lower hull's front bevel: its sloped contact can redirect a frontal approach upward. The selected change adds a **2.8 × 0.20 × 0.20 m vertical contact face** across the front lower edge, entirely within the old lower collision envelope. Its local center is `(hull.center.x, hull.min.y + .20, hull.max.z - .10)`. A 0.10 m gap above the hull bottom preserves the low-bevel rail-climbing path. This fills only the forward central portion of the lower bevel, rather than replacing the hull with the old full box. Side/rear bevels, low clearance, approved mesh, glide material and upper collider are unchanged. It intentionally reduces the frontal approach angle: obstacles above chassis clearance can stop the vehicle sooner. This tradeoff must be included in ledge/approach acceptance.

The face is a simple BoxCollider on a runtime child of Body with the same glide material and no renderer. Explicit mass properties prevent the added collider from moving COM or recalculating inertia. The component disables the child on disable and restores the original local solver values. It has no continuous work, impulses, downforce, pose changes or speed cancellation. Runtime construction also keeps every original serialized collider unchanged.

| Setting | Original | Selected |
| --- | --- | --- |
| Mass / COM / inertia / damping | See diagnosis | Unchanged |
| Wheel springs / dampers / travel / force points | See diagnosis | Unchanged |
| Local solver position / velocity iterations | 6 / 1 | 12 / 4 |
| Local maximum depenetration velocity | 10 m/s | 5 m/s |
| Continuous collision detection | Continuous | Unchanged |
| Lower front contact | Sloping bevel only | Bevel plus bounded vertical contact face |
| Global physics / timestep | Existing | Unchanged |

Unity's [WheelCollider documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WheelCollider.html) describes its ray-based contact and separate tire-friction model. The native suspension expansion limiter was also tested and rejected; no limiter is used in the selected change.

## Final native comparison

Full [baseline](impact-physics/baseline.json), [selected calibration](impact-physics/calibrated.json), [paired CSV](impact-physics/comparison.csv), setup and rejected-candidate reports are committed. There are **81 paired trials**: 56 low-edge/bevel, 13 wall, 10 movable-object and 2 ramp trials. No scenario had a COM-rise increase above 0.15 m or roll increase above 5° in this set; this is a retrospective observation, not a universal guarantee or a claim of statistical repeatability.

Representative **0.50 m rigid vertical edge**, with matched initial pose and velocity:

| Metric | Nominal 30 original | Nominal 30 selected | Nominal 60 original | Nominal 60 selected |
| --- | ---: | ---: | ---: | ---: |
| Speed at approach reference, km/h | 25.565 | 25.565 | 53.116 | 53.116 |
| Posterior speed (finalKph), km/h | 17.877 | 0 | 28.696 | 0 |
| Peak upward velocity, m/s | 2.501 | 0.827 | 5.804 | 0.033 |
| COM rise, m | 0.525 | 0.042 | 2.321 | 0.002 |
| Maximum pitch, degrees | 16.791 | 0.384 | 39.563 | 0.462 |
| Maximum angular speed, rad/s | 3.857 | 0.270 | 2.928 | 0.124 |
| Minimum grounded wheels | 0 | 6 | 0 | 6 |
| Peak chassis impulse, N·s | 5019.8 | 18632.0 | 12917.5 | 44220.3 |

The selected vehicle **stops against this above-clearance obstacle**. Total contact impulse is larger because forward momentum is arrested instead of being carried over the obstacle; the improvement is in upward redirection and rotation, not cancellation of total impulse. The face deliberately reduces frontal obstacle-climbing ability above ~0.34 m at static ride height. It does not soften a severe collision or simulate deformation/damage.

The nominal 60 km/h ramp trial retains complete loss of six-wheel contact (`minContacts=0`) followed by regained ground contact (`finalContacts=4`), COM rise ~1.10 m and peak vertical speed ~1.12 m/s in both configurations. The nominal 30 run has `minContacts=2`, so it is a ramp articulation/transition trial rather than a full six-wheel airborne jump. Ground gravity remains enabled, without extra stabilizing force.

All five movable fixtures move under native physics. At nominal 30 km/h, measured displacements are approximately 13.59 m (25 kg crate), 11.21 m (250 kg crate), 13.20 m (80 kg barrel), 5.68 m (900 kg block), and 9.65 m (400 kg barricade). These are fixture-specific results, not mass-only predictions.

The additional enabled-calibration recovery experiment retains the **6 cm underbody rail climb (8.80 m travel)** and forward escape from the 1.5 m ledge in AWD, FWD and RWD. These isolated regressions temporarily constrain selected test axes, as did the historical chassis verifier; they are separate from the unconstrained impact comparison. Reverse escape and arbitrary narrow-ledge geometry remain manual acceptance cases.

## Regression and integrity results

- `tools/verify-chassis.py`: **39 edit / 24 runtime checks passed**. Its historical comparison disables vehicle scripts; the separate recovery experiment above explicitly enables the new calibration.
- `tools/verify-driving-ground.py`: **89 edit / 199 runtime checks passed**.
- `tools/verify-tumbler-update.py`: **82 edit / 119 runtime checks passed**.
- `ImpactGroundVerify.Run`: **561 native fixture, height, winding/contact, sampled occupancy and access checks passed**. Sampled occupancy is not an exhaustive spatial proof.
- Local reset: both baseline and calibrated runs restore all five movable poses/velocities without moving the Tumbler. Gravity smoke checks pass.
- `tools/verify-impact-ground.py`: all **311 original non-root/non-Tumbler object blocks** remain byte-for-byte equal to inspected main. Original roots retain ordering and only gain the new root. The Tumbler GameObject only gains the calibration component reference. Original colliders, materials, transforms, physics properties, asset GUIDs, cameras, controls and environments remain unchanged on disk.
- `tools/validate-project.py`: **fails on clean main and this branch with the same historical HDRP assertion**, `Assets/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset`. Both blobs are `5363ad69f605e594aa63b85e0dc90b45bf9f9149`. The verifier compares against old input-cleanup commit `b624533`; no historical assertion was deleted or relaxed.

Unity logs also contain the existing Editor Search indexing exception. Native methods complete with exit status zero. Dynamic TMP atlas and editor workspace settings produced during verification are restored before committing.

Reproduce additional native checks with:

```sh
/path/to/Unity -batchmode -nographics -projectPath "$PWD" -executeMethod ImpactGroundVerify.Run -logFile TestResults/fixtures.log
IMPACT_CONFIG=recovery /path/to/Unity -batchmode -nographics -projectPath "$PWD" -executeMethod ImpactPhysicsVerify.Run -logFile TestResults/recovery.log
IMPACT_CONFIG=baseline /path/to/Unity -batchmode -nographics -projectPath "$PWD" -executeMethod ImpactPhysicsVerify.Run -logFile TestResults/baseline.log
IMPACT_CONFIG=calibrated /path/to/Unity -batchmode -nographics -projectPath "$PWD" -executeMethod ImpactPhysicsVerify.Run -logFile TestResults/calibrated.log
```

Close this checkout in Unity before batch verification. Baseline mode disables the selected component and restores its captured original settings; calibrated mode reenables it. Never save a verifier's Play Mode fixtures into the scene. Reports are under `TestResults/ImpactPhysics/`.

`python3 tools/compare-impact-physics.py` verifies all 81 paired starts (1e-5 m tolerance), bounded COM-rise/roll regressions, the two documented launch improvements, retained airborne ramp behavior and native displacement of the five movable objects. Pass `--reports TestResults/ImpactPhysics` after a fresh run. Those tolerances cover these fixtures, not arbitrary future environments.
