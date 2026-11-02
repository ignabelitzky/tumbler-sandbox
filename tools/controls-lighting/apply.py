#!/usr/bin/env python3
"""Copy only the authored controls subtree and new fire-light objects into original scenes."""
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[2]
allowed=json.loads((ROOT/"TestResults/ControlsLighting/allowed.json").read_text())
def docs(text):
    return {m[1]:m[0] for m in re.finditer(r"^--- !u!\d+ &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\Z)",text,re.M|re.S)}
manifest=dict(baseline="403f4ff7684d40bc3c5dc7496575487c156a0591",scenes={})
for scene,temp,permitted in [
    ("Main Menu","ExpandedMenu",set(allowed["menuAllowedIds"])),
    ("Sandbox","ExpandedSandbox",{allowed["vehicleTransformId"]})
]:
    path=ROOT/("Assets/_Project/Scenes/"+scene+".unity")
    original=subprocess.check_output(["git","show",manifest["baseline"]+":"+str(path.relative_to(ROOT))],cwd=ROOT,text=True)
    authored=(ROOT/("TestResults/ControlsLighting/"+temp+".unity")).read_text()
    before,after=docs(original),docs(authored)
    missing=set(before)-set(after)
    assert not missing,(scene,"missing original object IDs",missing)
    added=set(after)-set(before)
    assert 0<len(added)<100,(scene,"unexpected new scene objects",len(added))
    clean=lambda block:"\n".join(line.rstrip() for line in block.splitlines())+"\n"
    changed={k for k in before if k in permitted and before[k]!=clean(after[k])}
    assert changed,(scene,"no intended edits found")
    if scene=="Sandbox":
        parent=allowed["vehicleTransformId"]
        def without_children(block):
            return re.sub(r"  m_Children:\n(?:  - .*\n)*","",block)
        assert without_children(before[parent])==without_children(after[parent]),"Vehicle transform changed beyond its child list"
    output=original
    # Normalize only approved edits; untouched serialized objects stay byte-identical.
    for key in changed:output=output.replace(before[key],clean(after[key]))
    insert="".join(clean(after[key]) for key in after if key in added)
    marker="--- !u!1660057539 &9223372036854775807"
    assert marker in output
    output=output.replace(marker,insert+marker)
    path.write_text(output)
    manifest["scenes"][str(path.relative_to(ROOT))]=dict(changed_ids=sorted(changed),added_ids=sorted(added))
(ROOT/"docs/controls-lighting-manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")
print({name:{kind:len(ids) for kind,ids in data.items()} for name,data in manifest["scenes"].items()})
