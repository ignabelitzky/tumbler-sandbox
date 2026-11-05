#!/usr/bin/env python3
"""Replace only the existing proving-ground instance; preserve every other scene byte."""
import argparse,json,re,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'tools'))
from scene_documents import documents,validate_references
SCENE='Assets/_Project/Scenes/Sandbox.unity'
def patch(source,generated):
 block=documents(source)['1911000001'];guid=re.search(r'm_SourcePrefab:.*guid: (\w+)',block)[1]
 value=block.replace(guid,generated['prefab_guid'])
 for property_name,key in [('m_Name','root_id'),('vehicleRoot','grip_id')]:
  pattern=r'fileID: \d+(, guid: '+generated['prefab_guid']+r', type: 3\}\n      propertyPath: '+property_name+r'\n)'
  value,count=re.subn(pattern,lambda m:'fileID: '+str(generated[key])+m[1],value)
  assert count==1,'Missing proving-ground target '+property_name
 value=value.replace('value: Organic proving grounds','value: Classic flowing proving grounds')
 output=source.replace(block,value,1);validate_references(output);return output
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,default=ROOT);a=p.parse_args()
 generated=json.loads((a.project/'TestResults/ClassicSandbox/generated.json').read_text());scene=a.project/SCENE;scene.write_text(patch(scene.read_text(),generated))
