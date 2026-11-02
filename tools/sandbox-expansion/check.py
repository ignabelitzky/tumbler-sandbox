#!/usr/bin/env python3
"""Protect the last approved game's assets/settings while adding the underground facility."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
manifest = json.loads((ROOT / "docs/sandbox-expansion-manifest.json").read_text())
rows = subprocess.check_output(["git", "ls-tree", "-r", "-z", manifest["baseline"]], cwd=ROOT)
preserved = 0
for row in rows.split(b"\0"):
    if not row:
        continue
    info, name = row.split(b"\t", 1)
    path = name.decode()
    if not path.startswith(("Assets/", "Packages/", "ProjectSettings/")):
        continue
    if path in {"Assets/_Project/Scenes/Sandbox.unity", "Assets/_Project/Scenes/Main Menu.unity"}:
        continue  # verify-navigation.py checks scene preservation, including the later controls/fire-light delta.
    data = (ROOT / path).read_bytes()
    sha = hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()
    if sha != info.split()[2].decode():
        raise SystemExit("Unexpected change to existing game asset/configuration: " + path)
    preserved += 1
for key in ("terrain_path", "prefab_path"):
    asset = ROOT / manifest[key]
    meta = asset.with_name(asset.name + ".meta").read_text()
    expected = manifest["terrain_guid" if key == "terrain_path" else "prefab_guid"]
    if "guid: " + expected not in meta:
        raise SystemExit("Generated asset reference does not match: " + str(asset))
print(json.dumps(dict(status="PASS", baseline=manifest["baseline"], unchanged_project_files=preserved)))
