#!/usr/bin/env python3
"""Check rear view and landing source and preserve every unrelated scene/asset setting."""
import hashlib,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'tools'))
from flight_preservation import MANIFEST,before_flight_menu
base=MANIFEST['baseline']
scene='Assets/_Project/Scenes/Main Menu.unity'
assert before_flight_menu((ROOT/scene).read_text())==subprocess.check_output(['git','show',base+':'+scene],cwd=ROOT,text=True)
for name,digest in MANIFEST['asset_sha256'].items():assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==digest,'Authored source changed: '+name
changed={scene,*MANIFEST['approved_blobs']}
preserved=0
for row in subprocess.check_output(['git','ls-tree','-r','-z',base,'--','Assets','Packages','ProjectSettings'],cwd=ROOT).split(b'\0'):
 if not row:continue
 info,raw=row.split(b'\t',1);name=raw.decode()
 if name in changed:continue
 data=(ROOT/name).read_bytes();assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==info.split()[2].decode(),'Unrelated game source/settings changed: '+name
 preserved+=1
source=(ROOT/'Assets/_Project/Scripts/Camera/SandboxCameraModes.cs').read_text();assert '<Gamepad>/leftShoulder' in source and '<Keyboard>/b' in source
source=(ROOT/'Assets/_Project/Scripts/Vehicle/Tumbler/TumblerController.cs').read_text();assert 'airAttitude' not in source and 'flight?.Step(Time.fixedDeltaTime)' in source
source=(ROOT/'Assets/_Project/Scripts/Vehicle/Tumbler/TumblerFlightDynamics.cs').read_text();assert 'AddRelativeTorque' not in source and 'ControlAttitude' not in source
print(json.dumps(dict(status='PASS',preserved_game_files=preserved,menu_documents=len(MANIFEST['menu_documents']),authored_source=len(MANIFEST['asset_sha256']))))
