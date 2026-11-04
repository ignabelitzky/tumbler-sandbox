# Stealth night vision and camera transitions

The existing V / gamepad R3 stealth action now enables a blue HDRP surface sonar. See [the current sonar and headlight guide](blue-sonar-and-headlights.md) for the updated rendering and beam settings.
Returning to normal, including a boost-triggered stealth exit, disables the sensor
immediately. Vehicle lights, turbine effects and engine muting keep their existing
stealth behavior. No extra lamp is created and saved exposure profiles are untouched.

The runtime-only custom pass targets the gameplay camera after post-processing.
It combines visible luminance with depth and surface normals so completely unlit
walls and the roadway remain readable. A separate HDR source buffer avoids reading
and writing the same render target. Highlight compression, range attenuation, world-anchored surface samples,
cyan contours and expanding acoustic pulses create the sensor appearance. Activation
fades in over 0.18 seconds. The shader is in Resources for player-build inclusion.
The effect and its temporary render target/material are released on scene unload.

C / D-pad Down keeps cycling Close / Medium / Far on the existing ThreeRing rig.
`SandboxCameraModes` uses CinemachineBlendDefinition's EaseInOut curve to blend
all ring radii/heights and Radius over 0.9 seconds. Because these are presets of
one virtual camera, changing the Brain's camera-switch blend alone would not
interpolate them. Rapid input starts the new blend from the current rig values.
A normal tunnel entry uses a faster 0.35-second transition and preserves camera
history; exit blends back to the selected preset. Collision protection remains
active. Teleports and initial setup snap to a safe rig; pausing freezes movement.
The blend definition and tunnel time can be adjusted on SandboxCameraModes.

Validation on Unity 6000.6.7f1:

```
python3 tools/sandbox-expansion/run.py StealthCameraVerify.Run --graphics
python3 tools/sandbox-expansion/run.py FacilityVerify.Run
```

The feature checks cover intermediate/monotonic camera positions, all three exact
endpoints, interrupted transitions, paused time, tunnel entry/exit, teleports,
restoring the original rig, normal/stealth toggles, disabling systems, no vehicle
lights in stealth, camera-only rendering, and native HDRP image checks in darkness
and daylight. FacilityVerify explicitly advances transitions during its scripted
physics simulation and retains the complete vehicle traversal and camera checks.
Normal, night, restored-normal and daylight captures are produced in
TestResults/SandboxExpansion/Previews. Existing night-system regression tests also
cover keyboard/gamepad stealth, turbo exit, lights, audio, particles and brake heat.

The original feature was verified in a separate project copy while the editor was open; the current blue-sonar revision is also verified with native HDRP renders.
No scene/prefab or exposure profile needs to be reauthored: the vehicle systems
create the night sensor at runtime.
