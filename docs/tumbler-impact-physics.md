# Tumbler impact physics

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

The committed baseline and final reports contain the same poses, obstacle geometries and injected velocities (10/30/60/100 km/h), 3 s settling, .02 s native physics, and no constraints. A 2 s window follows the near-obstacle approach reference; timeout is 10 s. Actual reference speeds differ from injected speeds because tire spin starts at zero and the car coasts before impact. Compare paired trials, not the nominal speed alone.

`preKph` is measured at the near-obstacle reference (3.5 m before target center), `postKph` 0.2 s later, and `finalKph` at the observation end. These are defined sampling windows, not exact first-contact telemetry; the bevel begins earlier than the vertical edge. `maxVertical`, COM altitude/rise, pitch/roll Euler maxima, angular speed, minimum/final grounded-wheel counts and native peak collision impulse cover the whole window. Euler pitch/roll are diagnostic, not a complete orientation decomposition during a rollover. Impulse excludes tire forces. Outcome labels are heuristic and do not constitute acceptance assertions.

The high-damping candidate (.85 critical ratio) is archived as a **rejected** experiment: small step vertical speed/angular peaks worsened even where maximum pitch decreased. This demonstrates why increasing damping or mass blindly is insufficient.

## Calibration and reversal

The dedicated `TumblerImpactPhysics` component applies local settings once when enabled; it does not run an Update/FixedUpdate loop. Disable it to restore its captured original settings. Disabling the independent circuit has no effect on the vehicle calibration. Remove the dedicated scene component and script to revert the vehicle change; all original serialized body and wheel values are retained.

Do not enable legacy EasySuspension in parallel. If mass or geometry changes later, remeasure sprung masses and COM before adjusting application height. Keep a positive application height below COM, retain free body axes and compare low-step, oblique, wall, jump and drop trials. No pose correction, velocity clamp, extra downward force, frozen axis or collision suppression is used in gameplay.

## Validation limits

Native asset/contact/impact tests are described below. Physical Xbox driving, subjective handling, full throttle/boost impacts, maximum-speed acceleration, audio/visual acceptance, performance on target hardware and player builds still require manual testing. Coasting-impact data is not a claim that all driving features passed.
