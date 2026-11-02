# Local builds and GitHub Releases

Unity **6000.6.7f1** (revision b22767f04f04) compiles locally with Ignacio's activated
Unity Personal installation. GitHub only validates and publishes prepared ZIPs.
No GameCI, Unity license secret, XML/ULF transfer, account password, self-hosted
runner or paid Unity plan is part of this workflow.

## Branch and validation status

Reviewed main: 32da32f8cecab6ba03755865ae0a5c828a743de8.
Main had no workflows. The GameCI workflow belonged to the unmerged navigation/
intro PR #2, branch feat/navigation-branded-intro-release at 309dc63dea8bb548aee0f47b550c3ec29d38e947.
This change targets that branch to replace its workflow without changing the game.
Merge this tooling PR into that branch first, perform its native checklist, then
integrate PR #2 into main before publishing. Do not merge the old GameCI workflow
back afterwards. No runtime script, scene, asset, package or project setting is
changed by this tooling PR.

Automated repository checks and synthetic package tests are separate from Unity
compilation and playing a real build. The implementation environment has no Unity
Editor, so actual build/Play Mode/graphics/controller/Windows execution is pending.
No version was published as part of implementing this tooling.

## Install and check locally

1. Install the exact Editor in Unity Hub and keep the existing Personal activation.
   Open the project once to complete package resolution/import, then close it.
2. In Hub, install the target's Linux/Windows Build Support modules offered for this
   Editor. Use Standalone Mono, x86_64, not Dedicated Server.
3. Install Python 3, Git and GitHub CLI. Use normal private browser login with
   gh auth login if needed; never paste credentials in scripts or chat.
4. Start from a clean checkout of the intended main commit. Close the Editor
   before command-line builds. Run one Editor process at a time.

Example from the repository root (adjust only the Editor path):

    UNITY_EDITOR="$HOME/Unity/Hub/Editor/6000.6.7f1/Editor/Unity"
    python3 tools/verify-navigation.py
    python3 tools/verify-navigation.py --unity "$UNITY_EDITOR"
    python3 tools/verify-tumbler-update.py --unity "$UNITY_EDITOR" --graphics
    python3 -m unittest discover -s tools/release -p 'test_*.py' -v

Existing verifier reports go under TestResults. Review all failures and complete
the manual checklist in navigation-and-intro.md: intro/menu, pause/resume, return
to menu, exit, keyboard, real Xbox controller, driving, turbo, lights, stealth,
audio, HUD and HDRP. Existing verifiers may update font caches or project state;
inspect git status and restore only known generated changes after review. Never
commit those changes incidentally. Passing static CI does not imply native passes.

## Build and test

    python3 tools/release/release.py build linux-x64 --unity "$UNITY_EDITOR"
    python3 tools/release/release.py build windows-x64 --unity "$UNITY_EDITOR"

Each build uses all enabled EditorBuildSettings scenes in their existing order
(Intro, Main Menu, Sandbox), BuildOptions.None and the current Mono backend.
No scene or Player setting is rewritten to make the build pass. The script checks
a clean source tree before and after compilation, requires the exact Editor and
successful BuildReport, and writes a receipt tied to HEAD only after success.
It temporarily installs an Editor helper, then removes it even on failure.
Unity logs stay local under Builds/Release/<platform>/build.log.

Outputs are Builds/Release/linux-x64/TumblerSandbox.x86_64 and
Builds/Release/windows-x64/TumblerSandbox.exe, with their complete runtime folders.
Run each Player on its actual target OS and repeat the manual checklist before
packaging. Exit Unity Players before continuing. An executable header or build
receipt is not proof that graphics, input or gameplay work.

Builds must start in an absent output directory to prevent stale files. After
a failed build, inspect the local log and source diff, then explicitly remove
only that platform's generated Builds/Release directory before retrying.
If the process is forcibly killed and leaves Assets/_LocalRelease or its .meta,
inspect and remove that temporary folder before the next attempt.

## Package

Use a new version, for example v0.1.0-alpha.1. Keep the same clean source commit
for both platforms. Do not rebuild from a modified checkout or relabel old outputs.

    python3 tools/release/release.py pack v0.1.0-alpha.1 linux-x64 windows-x64

If Windows is unavailable/unverified, release Linux only:

    python3 tools/release/release.py pack v0.1.0-alpha.1 linux-x64

The command creates Builds/Packages/v0.1.0-alpha.1 with versioned ZIPs,
manifest.json and SHA256SUMS. Existing output directories are refused; inspect and
remove failed packaging output explicitly before retrying. ZIPs include the
complete build directory except local build logs/markers, plus LICENSE and NOTICE.
Retain any additional third-party notices in the build directory. Resolve the
existing asset provenance/redistribution review before public binary distribution;
this tooling does not grant rights. See README credits and repository-audit.md.

Validation checks package hashes, receipt commit/version/platform, executable
format, Linux execute permission, Mono runtime and required Data/UnityPlayer files.
It rejects duplicate/unsafe paths, symlinks and common license/credential filenames.
This is structural validation, not a malware scan, signature or reproducible build
attestation. Receipts and hashes record locally supplied provenance; a repository
writer can forge them. Never package source, Library, your home folder or licenses.

## Upload first, then create the version tag

After the tooling and game changes are integrated, update main, compile and package
again from that final commit. With GitHub CLI logged into this repository:

    git switch main
    git pull --ff-only
    # Run native checks, build, test and pack here, from this clean commit.
    RELEASE_VERSION=v0.1.0-alpha.1
    BUILD_COMMIT=$(git rev-parse HEAD)
    gh release create "local-builds-$RELEASE_VERSION" --repo ignabelitzky/tumbler-sandbox --draft --target "$BUILD_COMMIT" --title "Staged $RELEASE_VERSION" --notes "Local Players awaiting validation."
    gh release upload "local-builds-$RELEASE_VERSION" --repo ignabelitzky/tumbler-sandbox Builds/Packages/"$RELEASE_VERSION"/*.zip Builds/Packages/"$RELEASE_VERSION"/manifest.json
    git tag -a "$RELEASE_VERSION" "$BUILD_COMMIT" -m "$RELEASE_VERSION"
    git push origin "$RELEASE_VERSION"

The local-builds-* release is a staging draft; keep it unpublished. Its prefix
does not match the version-tag trigger. Uploading assets alone does not trigger
Actions, so finish all uploads before pushing the version tag. The staging draft
must be in this same repository, not a fork. Builds are never committed to Git.

The v* tag push starts Publish local Players. It resolves annotated/lightweight
tags to a commit on main, downloads staged ZIPs, validates everything, creates a
target draft release, uploads validated files and finally publishes. Tags with a
hyphen become prereleases (including alpha/beta/rc); plain vX.Y.Z are stable.
The target Release notes can be edited normally after publication.

Alternative: create the version tag first, stage its matching packages, then run:

    gh workflow run publish-release.yml --repo ignabelitzky/tumbler-sandbox --ref main -f tag=v0.1.0-alpha.1

You can also use Actions > Publish local Players > Run workflow > tag.
workflow_dispatch needs the workflow integrated into the default branch.
Publishing a GitHub Release for an existing tag also triggers validation and
asset upload, but the release page is public before that job finishes; prefer
tag/manual publication so assets are validated before the draft becomes public.
An empty GitHub Release or source ZIP is not a compiled game.

## Failures, retries and integrity

Watch Actions and require the publication job to succeed. Missing staging assets,
wrong commit, wrong Editor, partial files, corrupt ZIP or a tag outside main fails
the job. A tag pushed too early can be retried after staging via the manual trigger.
The workflow serializes jobs per version and refuses to overwrite differing
existing target assets. Matching assets are skipped on reruns; an interrupted
draft upload can be completed. A release published by GITHUB_TOKEN does not
normally trigger a second release workflow; publication already finishes in the
same job. Human publication may start another idempotent validation run.

Do not move released tags or use upload --clobber to replace published binaries.
For a corrected build, choose a new version. Keep staging until publication and
download verification succeed, then remove its draft manually if desired.
The job needs contents: write through its automatic GITHUB_TOKEN; no custom token
secret is required. Repository policy must allow Actions and that permission.
Each release asset must remain below GitHub's 2 GiB limit.

Users download and extract the complete platform ZIP, then run the Player without
Unity. From the download folder: sha256sum -c SHA256SUMS (download all listed ZIPs,
or verify just the line for the selected platform). Preserve the source tag and
notices alongside any redistribution.

## Windows and Linux limits

- This path supports Mono x64 only. Windows Mono can be cross-built from Linux
  when the exact Editor's Windows Build Support module is installed and available.
  Missing modules fail explicitly. If Hub does not offer a compatible module,
  compile on a Windows machine with the same source commit/Editor using this
  Python helper and its Windows Unity executable path.
- Windows IL2CPP requires a Windows toolchain (Visual Studio C++ and Windows SDK).
  It is not enabled by this Fedora Mono workflow. Linux IL2CPP also requires its
  own module/toolchain and different runtime validation; it is outside this path.
- A Windows cross-build must be tested on actual Windows; Wine is not acceptance
  evidence. Linux must be tested with a compatible HDRP/Vulkan GPU and driver.
  Fedora is the maintainer's local environment, not a claim of official Unity
  support or compatibility with every Linux distribution.
- Linux libraries/glibc, native plugins and GPU drivers limit portability. Test on
  each supported target distribution; do not promise universal Linux binaries.
  No macOS, ARM, server build, installer, code signing or automatic updater is
  supplied. Building succeeds separately from proving the game works.

References: [Unity Mono](https://docs.unity.com/en-us/engine/6000.6/manual/programming-environment/scripting-backends/mono),
[Unity Windows requirements](https://docs.unity3d.com/6000.0/Documentation/Manual/windows-requirements-and-compatibility.html),
[GitHub CLI asset upload](https://cli.github.com/manual/gh_release_upload),
[Actions triggers](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows).
