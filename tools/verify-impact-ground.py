#!/usr/bin/env python3
"""Prove additive scene preservation against the inspected main, plus GUID uniqueness."""
from pathlib import Path
import re, subprocess, json
ROOT=Path(__file__).resolve().parents[1]
BASE='5713507'
scene='Assets/_Project/Scenes/Sandbox.unity'
old=subprocess.check_output(['git','show',BASE+':'+scene],cwd=ROOT).decode()
from organic_layout_preservation import before_organic
new=before_organic((ROOT/scene).read_text())
def blocks(s):
 return {re.search(r'&(-?\d+)',b)[1]:b for b in re.split(r'(?=--- !u!)',s) if b.startswith('--- !u!')}
a,b=blocks(old),blocks(new)
assert set(a)<=set(b),'Original object deleted'
allowed={'9223372036854775807','2040198345356866087'}
for key in a:
 if key not in allowed: assert a[key]==b[key],f'Original serialized object changed: {key}'
rootline='  - {fileID: 875400000000000002}\n'
assert b['9223372036854775807'].replace(rootline,'')==a['9223372036854775807']
# Tumbler may only acquire the dedicated physical setup component.
car=b['2040198345356866087']
assert car.replace('  - component: {fileID: 875400000000000004}\n','')==a['2040198345356866087']
metas={}
for p in (ROOT/'Assets').rglob('*.meta'):
 m=re.search(r'^guid: (\w+)',p.read_text(),re.M)
 if m:
  assert m[1] not in metas,f'Duplicate GUID: {p}'
  metas[m[1]]=str(p)
prefab=(ROOT/'Assets/_Project/Prefabs/ImpactPhysicsTestGround.prefab').read_text()
for name in ['01 Low obstacle impacts','02 Rigid wall collisions','03 Movable objects','04 Suspension and uneven ground','05 Weight transfer and stability','06 Jumps and landings']:
 assert 'm_Name: '+name in prefab,name
assert prefab.count('Rigidbody:')==5
assert 'm_Name: Impact Physics Test Ground' in prefab
print(json.dumps({'status':'PASS','preserved_original_blocks':len(a)-2,'added_blocks':len(b)-len(a),'unique_asset_guids':len(metas),'movable_fixtures':5},indent=2))
