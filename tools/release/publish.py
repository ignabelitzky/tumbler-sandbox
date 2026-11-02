"""Idempotent upload; refuse to replace differing release assets."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
from release import digest, tag_ok, validate


def publish(folder, tag, commit):
    tag_ok(tag)
    files = validate(folder, tag, commit) + ["manifest.json", "SHA256SUMS"]
    release = json.loads(subprocess.check_output(
        ["gh", "release", "view", tag, "--json", "assets,isDraft"], text=True))
    existing = {a["name"] for a in release["assets"]}
    with tempfile.TemporaryDirectory() as temp:
        for name in files:
            if name in existing:
                subprocess.run(["gh", "release", "download", tag, "--pattern", name, "--dir", temp], check=True)
                if digest(Path(temp) / name) != digest(folder / name):
                    raise ValueError("Existing release asset differs: " + name)
    for name in files:
        if name not in existing:
            subprocess.run(["gh", "release", "upload", tag, str(folder / name)], check=True)
    if release["isDraft"]:
        subprocess.run(["gh", "release", "edit", tag, "--draft=false",
                        "--prerelease=" + ("true" if "-" in tag else "false")], check=True)


if __name__ == "__main__":
    publish(Path(sys.argv[1]), sys.argv[2], sys.argv[3])
