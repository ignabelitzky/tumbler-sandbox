#!/usr/bin/env python3
"""Apply the map layout without reserializing the vehicle or unrelated scene objects."""
from pathlib import Path
import re,json,subprocess,argparse
ROOT=Path(__file__).resolve().parents[2]
SCENE='Assets/_Project/Scenes/Sandbox.unity'
def blocks(s):return {m[1]:m[0] for m in re.finditer(r'^--- !u!\d+ &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\Z)',s,re.M|re.S)}
def patch(s):
 g=json.loads((ROOT/'TestResults/OrganicSandbox/generated.json').read_text());d=blocks(s)
 if '&1911000001' in s:
  old=re.search(r'm_SourcePrefab:.*guid: (\w+)',d['1911000001'])[1]
  s=s.replace(old,g['prefab_guid'])
  block=blocks(s)['1911000001']
  block=re.sub(r'fileID: \d+(, guid: '+g['prefab_guid']+r', type: 3\}\n      propertyPath: m_Name)',lambda m:'fileID: '+str(g['root_id'])+m[1],block)
  block=re.sub(r'fileID: \d+(, guid: '+g['prefab_guid']+r', type: 3\}\n      propertyPath: vehicleRoot)',lambda m:'fileID: '+str(g['grip_id'])+m[1],block)
  return s.replace(blocks(s)['1911000001'],block)
 # The old flat grid and isolated obstacle display are retained as disabled objects for easy rollback.
 t=d['560604267'];assert '  m_IsActive: 1\n' in t;s=s.replace(t,t.replace('  m_IsActive: 1\n','  m_IsActive: 0\n'))
 guidmap={}
 for meta in (ROOT/'Assets').rglob('*.prefab.meta'):
  text=meta.read_text();guidmap[re.search(r'^guid: (\w+)',text,re.M)[1]]=meta.with_suffix('')
 for id in ['819257298','834673486','1327134060','1373129390','1394258000','1554913287','1612590015','875400000000000001']:
  block=d[id];guid=re.search(r'm_SourcePrefab:.*guid: (\w+)',block)[1]
  root=re.search(r'- target: \{fileID: (\d+), guid: '+guid+r', type: 3\}\n      propertyPath: m_Name',block)[1]
  override=f'    - target: {{fileID: {root}, guid: {guid}, type: 3}}\n      propertyPath: m_IsActive\n      value: 0\n      objectReference: {{fileID: 0}}\n'
  s=s.replace(block,block.replace('    m_RemovedComponents:',override+'    m_RemovedComponents:'))
 block=d['1900000001'];guid=re.search(r'm_SourcePrefab:.*guid: (\w+)',block)[1];pf=blocks(guidmap[guid].read_text());extra=''
 for id,b in pf.items():
  if b.startswith('--- !u!1 ') and re.search(r'm_Name: (UNDERGROUND LIGHT LAB|LIGHT LAB / REVERSE ENTRY|TUNNEL TEST ROUTE|Portal posts )',b):
   extra+=f'    - target: {{fileID: {id}, guid: {guid}, type: 3}}\n      propertyPath: m_IsActive\n      value: 0\n      objectReference: {{fileID: 0}}\n'
 s=s.replace(block,block.replace('    m_RemovedComponents:',extra+'    m_RemovedComponents:'))
 block=f'''--- !u!1001 &1911000001
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
    - target: {{fileID: {g['root_id']}, guid: {g['prefab_guid']}, type: 3}}
      propertyPath: m_Name
      value: Organic proving grounds
      objectReference: {{fileID: 0}}
    - target: {{fileID: {g['grip_id']}, guid: {g['prefab_guid']}, type: 3}}
      propertyPath: vehicleRoot
      value:
      objectReference: {{fileID: 1665185318273656989}}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {g['prefab_guid']}, type: 3}}
'''
 s=s.replace('--- !u!1660057539 &9223372036854775807',block+'--- !u!1660057539 &9223372036854775807')
 s+='  - {fileID: 1911000001}\n';return s
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--index-copy');a=p.parse_args()
 if a.index_copy:Path(a.index_copy).write_text(patch(subprocess.check_output(['git','show','HEAD:'+SCENE],cwd=ROOT,text=True)))
 else:
  path=ROOT/SCENE;path.write_text(patch(path.read_text()))
