# Side and roof scrape grip

This extends the existing impact calibration without changing the authored Sandbox, collision geometry, mass, COM, inertia, tire curves, springs, downforce, steering or drivetrain.

## Cause and change

The lower convex hull and its small front contact face used the underbody glide material (static .04 / dynamic .025, Minimum combine) even when landing on their sides. This contact can retain substantial horizontal speed for several seconds. The upper body/roof used the shared LowFriction material (static/dynamic .3, Average combine).

`TumblerImpactPhysics` now creates/enables a runtime `TumblerChassisFriction` component after creating the front face. It assigns a private, non-bouncing scrape material (static .8 / dynamic .65, Average friction combine) to upper body contacts. The lower hull and front face retain their exact original glide material while upright, then use the scrape material when the vehicle tips beyond approximately 49.5° relative to gravity. They return to glide below approximately 41.4°; the hysteresis avoids repeated switching near the threshold. This is an orientation-based material approximation, not per-face material assignment: a very steep slope can also select scrape grip.

WheelColliders, triggers, and colliders on another Rigidbody are excluded. Materials are cached on enable, shared assets are never edited, and disabling the component restores original collider materials and destroys the private material. Disabling the parent impact calibration also disables scrape grip. There are no braking forces, velocity cancellation, extra torque, bounce boost, pose correction or automatic righting. Friction at off-center native contacts can convert translational motion into rotation; a flat roof scrape need not roll.

## Paired native experiment

Unity 6000.6.7f1, actual production Tumbler colliders and all six WheelColliders, fixed .02 s native physics, free body axes, flat .6/.6-friction test surface, identical injected poses and velocities. Existing solver/front-face calibration stays enabled for both variants. Only the scrape-grip component is toggled. Controller, motor/brake/boost and downforce loops are excluded to isolate contact response.

There are 21 pairs (42 trials): four settled roof slides, sixteen one-metre side/roof drops at 60/90/120/180° roll with downward speed 4 m/s and horizontal speed 10/20 m/s, and one upright landing control. Each observes five seconds after velocity injection. Roof slide trials settle first and are reanchored before injection. All tested pair starts match within .01 m / .5°. Distance is accumulated horizontal travel, not straight-line displacement; roll travel integrates absolute angular speed along the vehicle's longitudinal axis and can include rocking. It is not a count of completed revolutions.

| Scenario | Original travel | Scrape-grip travel | Original → adjusted roll travel |
| --- | ---: | ---: | ---: |
| Settled roof, forward, 36 km/h | 5.36 m | 3.99 m | 0.6° → 0.1° |
| Settled roof, forward, 72 km/h | 20.89 m | 15.16 m | 1.2° → 0.1° |
| 90° side drop, forward, 36 km/h | 37.71 m | 4.39 m | 0.5° → 90.1° |
| 90° side drop, lateral, 36 km/h | 37.67 m | 10.50 m | 1.0° → 151.6° |
| 90° side drop, lateral, 72 km/h | 81.88 m | 22.18 m | 1.0° → 676.3° |
| Upright landing control, 54 km/h | 61.27 m | 61.27 m | 0.2° → 0.2° |

These results support more grip and more natural rotation for side landings, without making every landing tumble. They do not establish universally shorter travel: the 60° lateral drop at 72 km/h travels 47.91 m instead of 40.20 m while undergoing greater rotation, and the 120° lateral drop at 36 km/h also travels farther. Landing orientation and regained wheel contact affect what follows the initial scrape. Both flat-roof configurations stop within the five-second window; the benefit there is a shorter stopping path.

Full measured data and checks are in `docs/rollover-friction/`. Run:

```sh
python tools/rollover/run.py RolloverFrictionVerify.Run
python tools/verify-chassis.py --unity /path/to/Unity
python tools/verify-tumbler-update.py --unity /path/to/Unity
```

The native sampler checks material selection/restoration, unchanged mass properties, paired starting poses, reduced roof/90°-side slide distances, extra native roll travel in a fast lateral fall, unchanged upright coast, free gravity and absence of airborne friction braking. Existing chassis regressions exercise rail sliding, curb/ledge recovery and fast-drop contact; drivetrain regressions cover all drive modes, gearing and steering. Physical controller driving, powered crashes, rough/sloped surface acceptance and a new standalone build remain manual checks. Five-second free-coast tests are not a full handling simulation.
