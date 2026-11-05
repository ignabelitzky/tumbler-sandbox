# Project review fixes

The six actionable defects reviewed at `9010321` are addressed with targeted behavior changes and shared cleanup/authoring paths.

| Finding | Corrected behavior | Regression |
| --- | --- | --- |
| Scene-authoring rollback | Controls authoring captures its own source snapshots, merges only owned documents into current scenes, and preflights both scenes before writing. It produces a reviewable patch and leaves historical manifests intact. | Seven ownership/conflict/reference tests, plus applying actual Unity-authored snapshots to a later classic-map scene while preserving 305 unrelated documents, scene roots and impact calibration |
| Reverse input cuts forward speed | A reverse request can supply opposing motor torque while the vehicle is still moving forward. The speed governor chooses its limit from actual travel direction. | LT at 216 km/h leaves velocity unchanged before the physics step; opposing torque remains available; actual backward travel respects the reverse governor |
| Incomplete upright recovery | Recovery projects heading onto the horizontal plane, with a stable fallback for vertical headings, and clears angular velocity. | Actual Y input on a 40-degree pitched / 110-degree rolled car, plus a vertical-heading case |
| Active torque after controller disable | Shutdown clears cached driving state, motor torque, braking and front steering outputs. | Active throttle followed by disabling the controller leaves zero drive outputs |
| Latched wheel audio | Every tire source evaluates its own current support on each audio update. Disabling audio stops screech sources. | Changing contact from four rear tires to two front tires stops airborne sources and starts supported ones; releasing handbrake stops all |
| Lost turbine effects after stealth disable | Normal exit and component shutdown share the same restoration path. Inactive particles defer restoration until reactivation; turbo remains owned by boost. | Component toggle and whole-vehicle reactivation restore captured idle emitters without igniting turbo |

The refactor concentrates on the failing responsibilities: explicit drive-output cleanup, separate opposing-torque eligibility, per-wheel audio, shared stealth capture/restore, owned scene-document merging, and shared Unity test-runner setup/cleanup. Serialized vehicle fields, input action IDs, mass, inertia, wheel geometry and existing camera behavior are retained.

`ClassicFixes.Run` verifies the gameplay corrections in Unity with virtual Xbox input and real WheelColliders. Existing drivetrain/HUD, navigation, held rear-camera, no-air-control and drop tests remain regression gates. No broad retuning of the powertrain or suspension is included.
