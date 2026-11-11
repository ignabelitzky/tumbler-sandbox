# Menu, takeoff calibration and minimal playground

Based on `41632d87b688402d64821be0b9575d15a2e0bc08`, Unity 6000.6.7f1.

## Developer credit

The bottom-left Phosphor group, icon, font, colors, dimensions and anchors are retained. Only the text's internal alignment changes from left to center; TMP line spacing gains 6 units. The exact two-line text remains `Developed by` / `Ignacio Belitzky`. Navigation verification checks both properties. Visual checks at 1920×1080, 1280×720, 1024×768 and 2560×1080 are pending.

## Takeoff calibration

Inspection found `forceAppPointDistance = 0` on all six wheels, while mass (3,000 kg), center of mass, inertia, wheel geometry and spring rates were already explicitly configured. The zero setting applies tire forces at the resting tire base, leaving a long moment arm below the center of mass when only the rear tires provide traction. Its contribution to the reported rear-only pivot is a hypothesis pending the native comparison, not a measured diagnosis.

The existing `TumblerFlightDynamics.Enable()` now places the suspension/tire force application plane 0.30 m below the center of mass, accounting for each wheel's center, radius and resting suspension travel. With the current geometry this gives approximately 0.5245 m for each front wheel and 0.5275 m for each of the four rear wheels. Separate contacts and Unity's supported mass per tire remain intact. The existing per-wheel 0.95 damping-ratio floor is unchanged.

Native `suspensionExpansionLimited` also prevents suspension support from following a receding surface faster than it can extend within a physics step. This targets false support at crest transitions. Both settings are restored on disable alongside the original springs. There are no new attitude torques, frozen rotations, angular-velocity clamps, jump-height limits, mass/inertia edits or aerial controls.

The square ramp still uses the same single source mesh and collider. Its transform, convex setting and top/face geometry are unchanged; no new collider seams are introduced.

References: [Unity force application](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WheelCollider-forceAppPointDistance.html), [suspension expansion](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WheelCollider-suspensionExpansionLimited.html).

## Playground

Git history from the original 2024 sandbox and the first Tumbler scene was reviewed for the open-terrain, small-obstacle-set design. The existing authored prefab is simplified directly; no generator runs in the player.

| Area | Current layout |
| --- | --- |
| Free driving | Existing broad, gently rolling terrain and spawn apron; handling circuit removed |
| Ramps | Original square/four-face ramp near (-102, -124), curved ramp near (-202, 82), low tabletop near (-110, 230) |
| Suspension | Progressive speed bumps, alternating gravel articulation, rolling gravel and shallow hollows |
| Tunnel | Original underground route, portals, lights, camera zones, terrain holes and both approach roads retained |
| Perimeter | Four visible sandstone boxes at X/Z ±475 m, 1.5 m thick; planted below terrain, with overlapping corners |

Seventeen prefab objects and their components are removed: the handling circuit and shoulders, north/west ramp access ribbons and shoulders, dirt/sand ribbons and shoulders, the medium tabletop and shoulders, the redundant 45-degree ramp, circular/triangular bumps, flush markings and collidable rock outcrops. The prefab has 26 GameObjects instead of 39 after adding the four walls. Distant visual ridges remain outside circulation.

Terrain data remains byte-identical, including its modest depressions and old route grading. Removed meshes leave no colliders behind. Source assets and historical route samples remain available but are not instantiated by the active prefab; the traversal helper selects only routes with a current scene object.

Scenes and all other game assets remain byte-identical. The tunnel and its navigation, keyboard/Xbox bindings, audio, cameras, branding assets, and Windows build configuration are preserved. The user's separate working copy and its uncommitted edits are untouched.

## Checks actually performed

- C# compilation against installed Unity 6000.6.7f1 assemblies: 41 runtime sources and 17 editor/diagnostic sources, zero errors. Windows player defines were also compiled separately, zero errors; this is not a Windows build.
- Seven existing Python scene-ownership tests: passed.
- `python3 tools/minimal-playground/check.py`: passed. Checks bidirectional component/hierarchy references, all scene-local references, removed-object absence, visible/solid perimeter components and byte preservation of 1,649 other game files.
- `git diff --check`: passed.

Unity was actually launched both with graphics and headless. The graphical run could not connect to the licensing service or X display. The headless run repeatedly failed to connect to `LicenseClient-ignacio` and was stopped before import/play mode. Consequently **import, runtime navigation, visual previews, takeoff/landing acceptance, console review and the Windows player build have not passed in this session**. No new in-engine screenshots or before/after physics measurements are claimed. Historical validation files elsewhere describe their own earlier revisions.

## Native checks to run when the Editor can start

Use a closed disposable checkout:

```sh
python3 tools/minimal-playground/run.py
python3 tools/classic-sandbox/run.py ClassicVerify.Run
python3 tools/classic-sandbox/run.py ClassicFixes.Run
python3 tools/minimal-playground/run.py --navigation
python3 tools/classic-sandbox/run.py ClassicPreview.Run --graphics
```

`RampTakeoffVerify` uses the real six-wheel Rigidbody on the preserved square ramp at 18, 43.2 and 79.2 km/h in both directions. It records paired before/after traces, rear-only pitch rate, release pitch rate, airborne duration and final attitude. Only the initial approach pose/speed is seeded; all subsequent motion uses real wheel forces and unconstrained rotation. A missing rear-only airborne event is inconclusive and fails acceptance. No results file is committed because this test has not run.

Also drive slowly up/down each ramp, accelerate/brake on slopes, repeat jumps at low/medium/high speed, land on flat and irregular terrain, cross consecutive humps, traverse both tunnel portals, and check the menu at the listed resolutions. Confirm that legitimate rolls remain possible and compare normal cornering after changing the force application plane. Run the existing drop/landing tests and a Windows build before claiming release readiness.

`tools/classic-sandbox/check.py` and older asset-hash manifests pin historical layouts. Use `tools/minimal-playground/check.py` for this revision. Re-running the historical `ClassicSandbox.Rebuild` restores its earlier map; apply `simplify.py` only against its explicitly pinned baseline or author a reviewed replacement.

Changes are split into UI, physics and scene commits so each can be reverted independently. No release or GitHub Actions workflow changes are included.
