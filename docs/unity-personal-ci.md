# Unity Personal: GitHub Actions setup

This repository uses Unity **6000.6.7f1** and a free **Unity Personal** account. No Pro serial is required. Follow the current [GameCI Personal activation instructions](https://game.ci/docs/github/activation/).

## One-time private setup

1. Sign into Unity Hub with your Unity account. Open **Preferences → Licenses → Add → Get a free personal license** and complete activation.
2. Locate `Unity_lic.ulf`. On Linux, the documented path is `~/.local/share/unity3d/Unity/Unity_lic.ulf`. On Windows it is `C:\ProgramData\Unity\Unity_lic.ulf`.
3. Open [this repository's Actions secrets](https://github.com/ignabelitzky/tumbler-sandbox/settings/secrets/actions) and create these **repository secrets**:

| Secret | Private value |
| --- | --- |
| `UNITY_LICENSE` | Complete contents of the activated `.ulf` file |
| `UNITY_EMAIL` | Email used for the Unity account |
| `UNITY_PASSWORD` | Password used for the Unity account |

Enter these values directly in GitHub. Do not put them in issues, commits, workflow YAML, screenshots or chat. The workflow checks only whether each secret exists and never deliberately prints its contents. A successful presence check does not prove activation works; the following Unity job does that.

If Hub does not generate the `.ulf` file, follow GameCI's current activation guidance. Do not use a fabricated license or an old manual activation workaround.

## Running validation

The [Unity validation workflow](../.github/workflows/unity-validation.yml) starts on pushes to `feat/navigation-branded-intro-release` and `main`, on pull requests to `main`, and through `workflow_dispatch` after the workflow exists on the default branch. Before merge, open the latest [Actions run](https://github.com/ignabelitzky/tumbler-sandbox/actions) for the feature branch and use **Re-run all jobs** after adding the secrets. A new commit also starts a run.

The workflow performs these dependent stages:

1. Static preservation, GUID, reference, scene-order and branding checks.
2. A private check for all three Personal secret names. Missing configuration fails visibly; it does not silently count as a passing native test.
3. Real Unity navigation Play Mode sessions and the existing vehicle regression verifier, sequentially in the same runner.
4. Development Player compilation for Windows x64 and Linux x64, sequentially, using the same checked-out commit.

GameCI is pinned to a reviewed commit. `manualExit: true` suppresses `-quit` so the asynchronous Play Mode sessions can finish and return their own exit code. Each test must also produce its expected completion files. The temporary verifier files are placed under Editor directories and are excluded from Players. Build target selection comes from GameCI's command-line platform argument, since ordinary workflow environment variables are not automatically passed into its container.

Reports are uploaded as Actions artifacts and identified by the workflow commit SHA. Unity/container logs are available in the job steps. Workflow runs and build targets are serialized to avoid simultaneous activations of the same Personal account. Fork pull requests run static checks only and receive no Unity secrets. Unity jobs require successful prior stages; a missing activation, unavailable exact Editor image/module, compilation error or failed assertion leaves validation incomplete. The workflow does not change the Unity version or project packages to work around an unavailable image.

## Remaining release gate

These jobs do not establish native HDRP visual quality, real audio output, physical Xbox support or Windows/Linux Player execution. Keep those checks in the [navigation guide](navigation-and-intro.md#required-manualnative-checks-before-merge). The workflow uploads diagnostic reports only; it does not distribute Players, merge the PR, create tags or publish a Release.

After phase 1 passes and is integrated into `main`, the release phase must build both distribution packages from that same `main` commit, retain notices and licenses, record SHA256 checksums, and publish the requested `v0.1.0-alpha.1` prerelease. The existing external-asset provenance/redistribution review remains open. Unity Personal activation does not resolve that separate requirement.
