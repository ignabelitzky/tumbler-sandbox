#!/usr/bin/env python3
"""Compare native paired coasting reports, with explicit impact and jump regression bounds."""
import argparse,json
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--reports',type=Path,default=Path(__file__).resolve().parents[1]/'docs/impact-physics')
a=p.parse_args()
before=json.loads((a.reports/'baseline.json').read_text())['trials']
after=json.loads((a.reports/'calibrated.json').read_text())['trials']
assert len(before)==len(after)==81
for x,y in zip(before,after):
 assert (x['scenario'],x['requestedKph'])==(y['scenario'],y['requestedKph'])
 assert all(abs(x['initialPosition'][k]-y['initialPosition'][k])<1e-5 for k in 'xyz')
 assert x['initialRotation']==y['initialRotation']
 assert y['comRise']<=x['comRise']+.15,(x['scenario'],'height regression')
 assert y['maxRoll']<=x['maxRoll']+5,(x['scenario'],'roll regression')
 if x['scenario']=='vertical-5' and x['requestedKph'] in (30,60):
  assert abs(x['preKph']-y['preKph'])<.01
  assert y['comRise']<x['comRise']*.3 and y['maxAngular']<x['maxAngular']*.3
  assert y['finalKph']<1 and y['minContacts']>0
 if x['scenario']=='jump' and x['requestedKph']==60:
  assert y['minContacts']==0 and y['finalContacts']>0
  assert abs(x['comRise']-y['comRise'])<.2
  assert .8*x['maxVertical']<y['maxVertical']<1.2*x['maxVertical']
 if x['scenario'].startswith('movable-'):
  assert y['movableMass']==x['movableMass'] and y['movableDisplacement']>.01
print(json.dumps({'status':'PASS','paired_trials':81,'matched_pose_tolerance_m':1e-5,'launch_cases_improved':2,'airborne_jump_and_movable_objects':'PASS'},indent=2))
