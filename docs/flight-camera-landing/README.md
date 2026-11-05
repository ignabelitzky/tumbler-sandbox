# Rear view and landing absorption

Airborne attitude follows the vehicle's natural physics. The left stick and keyboard do not add aerial pitch, roll, yaw or angular braking. The stick remains available for normal wheel steering.

Hold **LB** (keyboard **B**) to look behind. The camera sits in front of the car at 5.8 m horizontal distance and 2.9 m height, matching the close chase view. Release to restore the previous close/medium/far preset and orbit. The same collision probe keeps this view outside walls. Position and heading use the interpolated rendered vehicle pose, and the aim is resolved after the current frame's camera position. This avoids physics/render mismatch and previous-frame aim drift while accelerating. Tunnel proximity still takes precedence over returning to a distant preset.

The six suspension dampers now use their own supported mass to reach a 0.95 damping ratio. Spring stiffness, suspension travel, ride height and wheel geometry are unchanged. A heavy, upright touchdown on at least three wheels arms a short 0.8 second settling window. During supported contact it removes only excess velocity away from the ground normal; tangential momentum and airborne motion remain free. It does not glue the vehicle to a surface, add an automatic upright rotation or add a special loop force. Disabling the controller restores the original spring settings.

## Native validation

Unity 6000.6.7f1, HDRP 17.6, real six-wheel Rigidbody/WheelCollider simulation and virtual Input System devices were used in an isolated copy because the user's Editor was open. The committed Sandbox scene is unchanged by this feature. The only scene edits update the Controls overlay, which is checked at 1920×1080 and 1280×720.

`baseline-drops.json`, `after-drops.json` and their CSV traces record the before/after landing measurements on a solid, zero-bounce test floor. Cases include 5 m and 10 m drops at rest and at 72 km/h, plus a tilted 7.5 m drop at 54 km/h. Rebound excursion is measured from maximum suspension compression to the subsequent upward excursion; it is not an estimate of extra jump height above the standing vehicle.

The baseline was measured before the feature source was installed. A native comparison requires rebound speed below 60% of baseline, rebound excursion below 75%, an upright finish and settling within 1.3 seconds. At the project's normal 0.02 s physics step, four additional before/after pairs compare the services disabled/enabled on 5 m and 10 m drops at rest and at 72 km/h. Hard-body contact callbacks remove the solver's residual outward impulse within the same landing window; the existing depenetration limit stays unchanged. The original drop measurements below are retained as landing evidence. Current input tests verify that stick directions, X + stick and keyboard do not steer the car in flight or brake its natural rotation. They also cover LB hold/release, saved orbit/preset restoration, keyboard equivalents, pause and collision protection. A real-frame acceleration regression runs at 30, 60 and 120 simulated render frames per second with Rigidbody interpolation enabled. It measures follow error and aim against the rendered car after the Brain updates the output camera; see `rear-motion-before.json`, `rear-motion-after.json` and their CSV traces. The original transmission, HUD and navigation tests remain part of validation.

Run on the exact Editor, while that checkout is closed in Unity:

```sh
python3 tools/flight-controls/run.py FlightVerify.Verify
python3 tools/flight-controls/run.py FlightFeatures.Run --graphics
python3 tools/flight-controls/run.py FlightRearMotion.Verify --graphics
python3 tools/flight-controls/check.py
```

The drop comparison reads the recorded baseline from this guide when `TestResults/Flight/baseline-drops.json` is absent. Editor helpers are temporary and do not ship in the player. See `validation.json` for the original landing checks and `rear-view-validation.json` for the current control/camera revision; a standalone player build and a physical controller acceptance pass are separate.

The previous camera mixed physics-tick positions with interpolated rendering and reset aim before the current target was recorded. At the high-speed 120 Hz case, this produced more than 22 degrees of aim error and up to 1.28 m of follow error. The corrected view stays within 0.02 degrees and 0.001 m in the recorded checks. The 60 Hz case also includes a steering input.

![Held LB rear view](rear-view.png)

![Before/after landing rebound](landing-comparison.png)
