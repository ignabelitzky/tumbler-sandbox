#!/usr/bin/env python3
"""Validate exact approved scene edits and keep README controls in sync."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[2]
manifest=json.loads((ROOT/"docs/controls-lighting-manifest.json").read_text())
def git(*args):
    return subprocess.check_output(["git",*args],cwd=ROOT)
def docs(text):
    return {m[1]:m[0] for m in re.finditer(r"^--- !u!\d+ &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\Z)",text,re.M|re.S)}
for path,delta in manifest["scenes"].items():
    old=docs(git("show",manifest["baseline"]+":"+path).decode())
    new=docs((ROOT/path).read_text())
    assert set(old)<=set(new),"Original scene object removed: "+path
    assert set(new)-set(old)==set(delta["added_ids"]),"Unexpected scene object addition: "+path
    changed={k for k in old if old[k]!=new[k]}
    assert changed==set(delta["changed_ids"]),"Unexpected existing scene object edit: "+path
    for key in delta["added_ids"]:
        owner=re.search(r"^  m_GameObject: \{fileID: (\d+)\}",new[key],re.M)
        if owner and owner[1]!="0" and "m_Component:" in new.get(owner[1],""):
            assert "component: {fileID: "+key+"}" in new[owner[1]],"Unregistered scene component "+key+" in "+path
    if path.endswith("Sandbox.unity"):
        assert len(changed)==1
        for key in changed:
            strip=lambda s:re.sub(r"  m_Children:\n(?:  - .*\n)*","",s)
            assert strip(old[key])==strip(new[key]),"Vehicle changed beyond its new lighting child"
count=0
for row in git("ls-tree","-r","-z",manifest["baseline"]).split(b"\0"):
    if not row:continue
    info,name=row.split(b"\t",1);path=name.decode()
    if not path.startswith(("Assets/","Packages/","ProjectSettings/")) or path in manifest["scenes"]:continue
    data=(ROOT/path).read_bytes()
    digest=hashlib.sha1(b"blob "+str(len(data)).encode()+b"\0"+data).hexdigest()
    assert digest==info.split()[2].decode(),"Unexpected existing asset/settings change: "+path
    count+=1
catalog=json.loads((ROOT/"docs/controls-catalog.json").read_text())
expected="| Action | Xbox controller | Keyboard / mouse |\n| --- | --- | --- |\n"+"\n".join("| "+r["action"]+" | "+r["xbox"]+" | "+r["keyboard"]+" |" for r in catalog["rows"])
assert expected in (ROOT/"README.md").read_text(),"README control table is out of sync"
actions=json.loads((ROOT/"Assets/_Project/Input/TumblerInput.inputactions").read_text())["maps"][0]
assert {r["inputAction"] for r in catalog["rows"]}=={a["name"] for a in actions["actions"]}|{"CycleCamera","OrbitCamera"},"Catalog misses a gameplay action"
scene=(ROOT/"Assets/_Project/Scenes/Main Menu.unity").read_text()
# UI text and visual fit are tested natively; static CI verifies all catalog labels
# appear in the saved scene and original input code remains byte-identical.
for row in catalog["rows"]:
    for value in (row["action"],row["keyboard"].upper(),row["xbox"].upper()):
        assert value in scene,"Missing in-game control label: "+value
print(json.dumps(dict(status="PASS",controls=len(catalog["rows"]),unchanged_project_files=count)))
