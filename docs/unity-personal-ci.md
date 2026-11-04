# Local Unity validation

The project uses Unity **6000.6.7f1** with the maintainer's existing local Unity
Personal activation. GitHub Actions runs static asset, scene and reference checks.
No GameCI, license upload or Unity account secrets are needed.

The historical navigation, controls/lighting and tunnel baselines remain in place.
`tools/asset_preservation.py` records exact Git blob hashes for the later portal,
camera, burnout and HDRP smoke revisions. These paths are still checked for
unexpected edits; they are not excluded from preservation checks.

For Sandbox, only the three approved impact-ground/calibration documents and
their two attachment lines are removed **in memory** before the older scene
checks. Their exact document hashes are checked first, and all other current
scene data still goes through the original comparisons. Navigation validation
also runs `tools/verify-impact-ground.py` against the actual, unmodified scene.
When deliberately changing one of these assets, review its diff and update only
the corresponding approved hash. Do not reset a baseline to HEAD to hide errors.

Run native navigation checks locally from the repository root with the project
closed in the Editor:

    python3 tools/verify-navigation.py --unity /absolute/path/to/6000.6.7f1/Editor/Unity

For the existing vehicle regression:

    python3 tools/verify-tumbler-update.py --unity /absolute/path/to/6000.6.7f1/Editor/Unity --graphics

See [navigation and intro](navigation-and-intro.md) for the manual checklist.
Do not upload or share license XML/ULF files or Unity account credentials.
This repository does not configure automatic releases or binary publication.
