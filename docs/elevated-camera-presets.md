# Elevated Tumbler camera presets

Close, Medium and Far now share a neutral downward chase angle of approximately 26.6 degrees toward the existing vehicle aim target. The old medium/far neutral views used a height-to-distance ratio of .1 (about 5.7 degrees), which looked almost horizontal. The close view previously had a separate 2.2 m center height.

Cinemachine is configured as a ThreeRing orbit: its VerticalAxis value is normalized into the orbit spline, rather than directly used as a pitch angle. The correction therefore changes all three ring heights in `SandboxCameraModes.ApplyMode`, retaining the existing horizontal radii and gameplay aim/composition.

| Preset | Neutral horizontal distance | Neutral height above vehicle pivot | Low / high orbit height |
| --- | ---: | ---: | ---: |
| Close | 5.8 m | 2.9 m | 1.624 / 4.64 m |
| Medium | 10 m | 5 m | 2.8 / 8 m |
| Far | 16 m | 8 m | 4.48 / 12.8 m |

All heights are relative to the followed vehicle pivot, not to terrain elevation. Manual horizontal/vertical orbit remains available; the lower ring is also elevated to avoid the floor-level appearance when looking around. Cycling modes preserves the player's orbit axes and the existing smooth ring interpolation. Initial activation/teleports retain the existing snap behavior, and disabling restores the authored rig. Tunnel zones still temporarily select Close; the existing collision extension can shorten the camera boom near obstacles.

## Native verification

```sh
python tools/camera-elevation/run.py CameraElevationVerify.Run --graphics
python tools/sandbox-expansion/run.py FacilityVerify.Run
python tools/verify-navigation.py
python tools/sandbox-expansion/check.py
```

The new verifier runs the actual production Cinemachine pipeline and measures the final eye position/aim for all three presets. It checks elevated neutral viewpoints, downward pitch, centered vehicle aim, horizontal orbit from both sides/front, low/high vertical orbit, continuous switching, paused transitions, tunnel proximity and restoration on disable. It captures the three normal gameplay views at 1280×720. A temporary unsaved volume disables motion blur only during inspection to avoid stale teleport motion vectors; gameplay exposure, lens settings and motion-blur assets are not changed.

The existing facility verifier also checks camera cycling/input, all tunnel zones, camera contact exclusion and physical vehicle traversal through the tunnel. Its old exact 2.2 m close-height assertion is replaced by an elevated chase-angle requirement. Serialized Sandbox/camera assets, controls, vehicle physics and all unrelated assets remain unchanged. Measurements/checks are in `docs/camera-elevation/`. Physical controller/camera feel and a new standalone executable remain manual checks.
