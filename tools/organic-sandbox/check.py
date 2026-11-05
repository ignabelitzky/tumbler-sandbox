#!/usr/bin/env python3
"""Pin the authored course and prove that all unrelated vehicle/scene source remains intact."""
import hashlib,json,re,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools'))
from organic_layout_preservation import MANIFEST,before_organic
scene=(ROOT/'Assets/_Project/Scenes/Sandbox.unity').read_text()
baseline=subprocess.check_output(['git','show',MANIFEST['baseline']+':Assets/_Project/Scenes/Sandbox.unity'],cwd=ROOT,text=True)
assert before_organic(scene)==baseline,'Change outside approved organic scene delta'
for path,digest in MANIFEST['asset_sha256'].items():
 assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==digest,'Authored asset changed: '+path
seen={}
for meta in (ROOT/'Assets').rglob('*.meta'):
 match=re.search(r'^guid: (\w+)',meta.read_text(),re.M)
 if match:
  assert match[1] not in seen,'Duplicate GUID: '+str(meta)
  seen[match[1]]=meta
for path in MANIFEST['asset_sha256']:
 if path.startswith('Assets/') and not path.endswith('.meta'):assert (ROOT/(path+'.meta')).exists(),'Missing metadata '+path
for protected in ('Assets/_Project/Input','Assets/_Project/Scripts/Vehicle','Assets/_Project/Environment/UndergroundFacility','Packages','ProjectSettings'):
 for row in subprocess.check_output(['git','ls-tree','-r','-z',MANIFEST['baseline'],'--',protected],cwd=ROOT).split(b'\0'):
  if not row:continue
  info,path=row.split(b'\t',1);name=path.decode();data=(ROOT/name).read_bytes()
  assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==info.split()[2].decode(),'Protected source changed: '+name
print(json.dumps(dict(status='PASS',authored_assets=len(MANIFEST['asset_sha256']),scene_documents=len(MANIFEST['scene_documents']),unique_guids=len(seen))))
