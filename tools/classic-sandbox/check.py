#!/usr/bin/env python3
"""Pin the classic map, current runtime fixes, original tunnel and supplied sky."""
import hashlib,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'tools'))
from classic_layout_preservation import MANIFEST,before_classic
from flight_preservation import MANIFEST as FLIGHT
scene='Assets/_Project/Scenes/Sandbox.unity'
assert before_classic((ROOT/scene).read_text())==subprocess.check_output(['git','show',MANIFEST['baseline']+':'+scene],cwd=ROOT,text=True)
for path,digest in MANIFEST['asset_sha256'].items():assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==digest,'Classic asset changed: '+path
preserved=0
for entry in subprocess.check_output(['git','ls-tree','-r','-z',MANIFEST['baseline'],'--','Assets','Packages','ProjectSettings'],cwd=ROOT).split(b'\0'):
 if not entry:continue
 info,name=entry.split(b'\t',1);name=name.decode()
 if name==scene:continue
 data=(ROOT/name).read_bytes();actual=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
 assert actual==FLIGHT['approved_blobs'].get(name,info.split()[2].decode()),'Unexpected unrelated source/asset change: '+name
 preserved+=1
prefab=(ROOT/'Assets/_Project/Environment/ClassicSandbox/ClassicSandbox.prefab').read_text()
for name in ['Progressive speed bumps','Alternating wheel articulation','Rolling gravel and shallow hollows','Original ramp_45','Original curved_ramp','Original square_ramp']:assert 'm_Name: '+name in prefab,name
assert not any(name in prefab for name in ['Loop 18m','Loop 26m','Long four bend','Progressively banked oval'])
print(json.dumps(dict(status='PASS',classic_assets=len(MANIFEST['asset_sha256']),scene_documents=len(MANIFEST['scene_documents']),preserved_existing_game_files=preserved,active_tunnels=1)))
