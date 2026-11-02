#!/usr/bin/env python3
"""Local Mono builds and validated release ZIPs; Python standard library only."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[2]
VERSION = "6000.6.7f1"
PLATFORMS = {"linux-x64": ("TumblerSandbox.x86_64", "UnityPlayer.so", b"\x7fELF"),
             "windows-x64": ("TumblerSandbox.exe", "UnityPlayer.dll", b"MZ")}


def run(*args):
    return subprocess.check_output(args, cwd=ROOT, text=True).strip()


def tag_ok(tag):
    if not re.fullmatch(r"v\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?", tag):
        raise ValueError("Expected v1.2.3 or v1.2.3-alpha.1")
    return tag


def clean():
    if run("git", "status", "--porcelain", "--untracked-files=all"):
        raise ValueError("Commit or remove local changes before building/packaging.")
    return run("git", "rev-parse", "HEAD")


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def build(platform, unity):
    sha = clean()
    output = ROOT / "Builds" / "Release" / platform
    if output.exists():
        raise ValueError(f"Remove old output explicitly first: {output}")
    temp = ROOT / "Assets" / "_LocalRelease"
    if temp.exists() or temp.with_suffix(".meta").exists():
        raise ValueError("Temporary Editor folder already exists; inspect it first.")
    output.mkdir(parents=True)
    try:
        (temp / "Editor").mkdir(parents=True)
        shutil.copy(ROOT / "tools/release/LocalReleaseBuild.cs", temp / "Editor")
        env = dict(os.environ, TUMBLER_PLATFORM=platform,
                   TUMBLER_OUTPUT=str(output / PLATFORMS[platform][0]))
        subprocess.run([unity, "-batchmode", "-projectPath", str(ROOT),
                        "-buildTarget", "Linux64" if platform == "linux-x64" else "Win64",
                        "-executeMethod", "LocalReleaseBuild.Run",
                        "-logFile", str(output / "build.log")], env=env, cwd=ROOT,
                       check=True, timeout=7200)
        if (output / "build-complete.txt").read_text() != VERSION:
            raise ValueError("Missing successful Unity build receipt.")
    finally:
        shutil.rmtree(temp)
        temp.with_suffix(".meta").unlink(missing_ok=True)
    if clean() != sha:
        raise ValueError("Source changed during build; do not package this output.")
    (output / "receipt.json").write_text(json.dumps(
        dict(commit=sha, unity=VERSION, platform=platform, development=False)))
    print(f"Built {output}. Run and test the Player before packaging.")


def check_zip(path, platform, sha):
    exe, player, magic = PLATFORMS[platform]
    if path.stat().st_size >= 2 * 1024 ** 3:
        raise ValueError("Release asset must be smaller than 2 GiB")
    with zipfile.ZipFile(path) as z:
        names = z.namelist()
        if len(names) != len(set(names)):
            raise ValueError("Duplicate archive entries")
        for info in z.infolist():
            p = PurePosixPath(info.filename)
            if (p.is_absolute() or ".." in p.parts or "\\" in info.filename
                    or ":" in info.filename or stat.S_ISLNK(info.external_attr >> 16)
                    or info.flag_bits & 1):
                raise ValueError("Unsafe archive entry")
            if p.name.lower().endswith((".ulf", ".xml")) or p.name.lower() in {".env", "build.log"}:
                raise ValueError("Private/configuration file in Player package")
        required = [exe, player, "TumblerSandbox_Data/globalgamemanagers",
                    "TumblerSandbox_Data/boot.config", "LICENSE", "NOTICE.md", "receipt.json"]
        if any(n not in names for n in required) or not any(n.startswith("MonoBleedingEdge/") for n in names):
            raise ValueError("Incomplete Mono Player package")
        if any(z.getinfo(n).file_size == 0 for n in required):
            raise ValueError("Empty required Player file")
        if z.getinfo("receipt.json").file_size > 4096:
            raise ValueError("Oversized build receipt")
        with z.open(exe) as executable:
            header = executable.read(len(magic))
        if header != magic:
            raise ValueError("Wrong executable format")
        if platform == "linux-x64" and not (z.getinfo(exe).external_attr >> 16) & 0o111:
            raise ValueError("Linux Player is not executable")
        if json.loads(z.read("receipt.json")) != dict(
                commit=sha, unity=VERSION, platform=platform, development=False):
            raise ValueError("Build receipt does not match release commit/platform")
        if z.testzip():
            raise ValueError("Corrupt archive")


def pack(tag, platforms):
    tag_ok(tag)
    sha = clean()
    dest = ROOT / "Builds" / "Packages" / tag
    dest.mkdir(parents=True, exist_ok=False)
    entries = []
    for platform in platforms:
        source = ROOT / "Builds" / "Release" / platform
        name = f"tumbler-sandbox-{tag}-{platform}.zip"
        archive = dest / name
        with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
            for path in sorted(source.rglob("*")):
                if path.is_symlink():
                    raise ValueError("Symlinks are not supported")
                if path.is_file() and path.name not in {"build.log", "build-complete.txt"}:
                    z.write(path, path.relative_to(source).as_posix())
            z.write(ROOT / "LICENSE", "LICENSE")
            z.write(ROOT / "docs/release-notice.md", "NOTICE.md")
        check_zip(archive, platform, sha)
        entries.append(dict(file=name, platform=platform, sha256=digest(archive)))
    manifest = dict(schema=1, tag=tag, commit=sha, unity=VERSION, packages=entries)
    (dest / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    validate(dest, tag, sha)
    print(dest)


def validate(folder, tag, sha):
    tag_ok(tag)
    m = json.loads((folder / "manifest.json").read_text())
    if (m["schema"], m["tag"], m["commit"], m["unity"]) != (1, tag, sha, VERSION):
        raise ValueError("Manifest does not match tag commit and Editor version")
    entries = m["packages"]
    if not 1 <= len(entries) <= 2 or len({e["platform"] for e in entries}) != len(entries):
        raise ValueError("Expected one or two distinct platforms")
    checksums = []
    for e in entries:
        platform = e["platform"]
        if platform not in PLATFORMS or e["file"] != f"tumbler-sandbox-{tag}-{platform}.zip":
            raise ValueError("Unexpected package filename/platform")
        path = folder / e["file"]
        if digest(path) != e["sha256"]:
            raise ValueError("Package checksum mismatch")
        check_zip(path, platform, sha)
        checksums.append(f'{e["sha256"]}  {e["file"]}\n')
    (folder / "SHA256SUMS").write_text("".join(checksums))
    return [e["file"] for e in entries]


def main():
    p = argparse.ArgumentParser(description=__doc__)
    sub = p.add_subparsers(dest="command", required=True)
    b = sub.add_parser("build")
    b.add_argument("platform", choices=PLATFORMS)
    b.add_argument("--unity", required=True, help="Absolute path to activated Unity executable")
    a = sub.add_parser("pack")
    a.add_argument("tag", type=tag_ok)
    a.add_argument("platforms", nargs="+", choices=PLATFORMS)
    v = sub.add_parser("validate")
    v.add_argument("folder", type=Path)
    v.add_argument("tag", type=tag_ok)
    v.add_argument("commit")
    args = p.parse_args()
    if args.command == "build":
        build(args.platform, args.unity)
    elif args.command == "pack":
        if len(set(args.platforms)) != len(args.platforms):
            p.error("Duplicate platforms")
        pack(args.tag, args.platforms)
    else:
        validate(args.folder, args.tag, args.commit)


if __name__ == "__main__":
    main()
