#!/usr/bin/env python3
"""Run temporary Editor authoring/verification helpers with the project's exact Editor."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
p = argparse.ArgumentParser()
p.add_argument("method")
p.add_argument("--graphics", action="store_true")
p.add_argument("--unity", default="/home/ignacio/Unity/Hub/Editor/6000.6.7f1/Editor/Unity")
args = p.parse_args()
temp = ROOT / "Assets/_SandboxExpansionWork"
if temp.exists():
    raise SystemExit("Temporary helper already exists; inspect it before retrying.")
font = ROOT / "Assets/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset"
original_font = font.read_bytes()
try:
    (temp / "Editor").mkdir(parents=True)
    for source in Path(__file__).parent.glob("*.cs"):
        shutil.copy2(source, temp / "Editor")
    flags = ["-force-vulkan"] if args.graphics else ["-nographics"]
    report = ROOT / "TestResults/SandboxExpansion"
    report.mkdir(parents=True, exist_ok=True)
    subprocess.run([args.unity, "-batchmode", *flags, "-projectPath", str(ROOT),
        "-executeMethod", args.method, "-logFile", str(report / (args.method + ".log"))],
        cwd=ROOT, check=True, timeout=900)
finally:
    font.write_bytes(original_font)
    shutil.rmtree(temp)
    temp.with_suffix(".meta").unlink(missing_ok=True)
