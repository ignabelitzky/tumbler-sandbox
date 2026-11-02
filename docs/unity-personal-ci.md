# Local Unity validation

The project uses Unity **6000.6.7f1** with the maintainer's existing local Unity
Personal activation. GitHub Actions runs static asset, scene and reference checks.
No GameCI, license upload or Unity account secrets are needed.

Run native navigation checks locally from the repository root with the project
closed in the Editor:

    python3 tools/verify-navigation.py --unity /absolute/path/to/6000.6.7f1/Editor/Unity

For the existing vehicle regression:

    python3 tools/verify-tumbler-update.py --unity /absolute/path/to/6000.6.7f1/Editor/Unity --graphics

See [navigation and intro](navigation-and-intro.md) for the manual checklist.
Do not upload or share license XML/ULF files or Unity account credentials.
This repository does not configure automatic releases or binary publication.
