# Impact & Physics Test Ground

Additive installation in `Sandbox.unity`, prefab `Assets/_Project/Prefabs/ImpactPhysicsTestGround.prefab`, root **Impact Physics Test Ground**.

## Location and access

Root Unity position **(-280, 0.03, -250)**, unrotated. Platform X **-460…-100**, Z **-430…-70**, surface Y **0.03 m**. It is southwest of the original playground and south of the Driving Test Ground. Existing terrain remains unchanged beneath it. The facility has its own flush 3 cm access lip, no excavation.

From spawn `(0,1,40)`, drive west to `(-80,0,40)`, south to `(-80,0,-60)`, then west along the added access turn to `(-280,0.03,-60)` and south onto the platform. Existing underground portals are at positive X; perimeter containment remains outside the facility. Do not route through those structures.

All added geometry, signs, fixtures, and local reset are children of this root. Disable the root to remove every new physical interaction. The prefab has no reference to the Tumbler, navigation, HUD, or original scene objects.

## Layout (local coordinates relative to root)

| Group | Fixtures | Dimensions / masses |
| --- | --- | --- |
| 01 Low impacts | Seven parallel lanes X -155…-35, 20 m apart. Bevel at Z -110, vertical at Z -50 | Heights 0.05/0.10/0.15/0.20/0.30/0.50/0.70 m. Width 8 m. Vertical depth 1 m; bevel run 2 m. All static. |
| 02 Rigid walls | Six approach lanes X 35…155, walls near Z -45 | 3 m high, frontal/30°/45° widths 8 m, wide concrete 18 m, lateral 1×15 m. Corner is approached with lateral offset. Static. |
| 03 Movable objects | X -150…-38, Z 70 | Crates 1.5 m cube: 25/250 kg; barrel Ø1.1×1.5 m: 80 kg; concrete 1.5 m cube: 900 kg; barricade 5×1.5×1 m: 400 kg. Native Rigidbody response only. |
| 04 Suspension | Progressive lane X40, articulation X72, potholes X110, cross slope X150 | 0.05…0.30 m steps; alternate left/right 0.24 m blocks. Raised 0.4 m pothole deck with 3 m depressions down to 0.05 m. 8° asymmetric slope. |
| 05 Weight transfer | West/east acceleration/braking straight at Z110, curve markers centered (-110,135), alternating direction markers | 120 m straight with 10 m references, 30 m radius curve. 8° lateral slope 14×50 m at X -25. |
| 06 Jumps | Launch X45/Z135, landing Z158; braking incline X95; small drop X145 | Launch 14×18 m, 1.2 m rise. Landing 20×24 m, 0.2 m high. Braking incline 14×28 m, 2 m rise with horizontal crest. Drop 0.30 m. |

Concrete/steel/road/amber/paint materials are reused from the Sandbox's underground facility. Closed wedges use simple static six-vertex meshes; other obstacles use primitive colliders. Painted references have disabled colliders. Barrel uses Unity's primitive cylinder collider. No external artwork.

## Repeatable trials

Begin at 10 km/h for low impacts and walls; progress to 30/60 km/h when appropriate. 100 km/h trials are severe impacts, not a no-rollover acceptance test. Each low lane has a beveled and a vertical obstacle: for isolation, start **between** them for the vertical obstacle; the automated verifier temporarily disables the other low obstacles, without saving the scene. High-speed runout can exit the platform, so manual high-speed comparisons should stop and reposition after the target event.

The digital HUD remains the speed reference. Lane marks assist approach positioning, not speed measurement. Six-wheel static ride height and actual underbody clearance are recorded by the verifier; the 0.30/0.50/0.70 m lanes bracket chassis contact. The selected front face starts 0.10 m above the hull bottom, around 0.34 m above the flat platform.

In Play Mode, select the root's **Impact Ground Reset** component and use its context menu **Restore movable test objects**. This restores only its five cached rigidbodies, clears velocities and sleeps them. There are no new key bindings, polling loops, or automatic reset. The vehicle's own reset and spawn are unchanged.

Run the matching Unity Editor closed on this checkout:

```sh
python3 tools/verify-impact-ground.py
IMPACT_CONFIG=calibrated /path/to/Unity -batchmode -nographics -projectPath "$PWD" -executeMethod ImpactPhysicsVerify.Run -logFile TestResults/impact.log
```

`ImpactPhysicsVerify` is Editor-only; it loads Sandbox, enters Play Mode, disables normal script forces, releases all body constraints and advances native physics at 0.02 s. It records 81 trials on the actual fixture geometries. Reports go to ignored `TestResults/ImpactPhysics/`. It never saves the scene. Restore the dynamic TMP font from Git if the Editor changes its atlas.

For full driving acceptance, repeat acceleration/braking, all drive modes, seven gears, boost, steering, jump/landing, reset, keyboard and Xbox on the user's machine. The coasting impact experiment does not establish those subjective/player-control results.

The selected vehicle calibration passes the 6 cm rail and AWD/FWD/RWD ledge-recovery checks. The complete numeric comparison and known limitations are in [tumbler-impact-physics.md](tumbler-impact-physics.md). For the lateral slopes, approach from the low long edge rather than striking the elevated end face.
