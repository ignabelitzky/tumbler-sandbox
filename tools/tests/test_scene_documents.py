import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

TOOLS=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(TOOLS))
from scene_documents import SceneConflict,merge_owned
spec=importlib.util.spec_from_file_location('controls_apply',TOOLS/'controls-lighting/apply.py')
controls=importlib.util.module_from_spec(spec);spec.loader.exec_module(controls)

def scene(label='before',terrain='current terrain'):
 return f'''%YAML 1.1
--- !u!1 &1
GameObject:
  m_Name: {label}
--- !u!4 &2
Transform:
  m_GameObject: {{fileID: 1}}
--- !u!1 &3
GameObject:
  m_Name: {terrain}
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_Roots:
  - {{fileID: 2}}
'''

class SceneOwnershipTests(unittest.TestCase):
 def test_preserves_newer_terrain_while_updating_controls(self):
  result=merge_owned(scene(terrain='new classic terrain'),scene(),scene('after','old terrain'),{'1'})
  self.assertEqual(result,scene('after','new classic terrain'))
 def test_conflicting_owned_edit_is_rejected(self):
  with self.assertRaises(SceneConflict):merge_owned(scene('concurrent'),scene(),scene('after'),{'1'})
 def test_stale_or_unowned_added_object_is_not_imported(self):
  authored=scene('after').replace('--- !u!1660057539','--- !u!1 &99\nGameObject:\n  m_Name: unwanted\n--- !u!1660057539')
  self.assertNotIn('unwanted',merge_owned(scene(),scene(),authored,{'1'}))
 def test_dangling_local_reference_is_rejected(self):
  authored=scene('after').replace('m_Name: after','m_Name: after\n  target: {fileID: 1234}')
  with self.assertRaises(SceneConflict):merge_owned(scene(),scene(),authored,{'1'})
 def fixture(self,root):
  folder=root/'TestResults/ControlsLighting';folder.mkdir(parents=True)
  (folder/'allowed.json').write_text(json.dumps({'menuAllowedIds':['1'],'sandboxAllowedIds':['1']}))
  for name,label in [('Main Menu','Menu'),('Sandbox','Sandbox')]:
   path=root/f'Assets/_Project/Scenes/{name}.unity';path.parent.mkdir(parents=True,exist_ok=True);path.write_text(scene(terrain='classic ground'))
   (folder/f'Original{label}.unity').write_text(scene())
   (folder/f'Expanded{label}.unity').write_text(scene('after','old ground'))
  return folder
 def test_both_scenes_preflight_before_either_is_written(self):
  with tempfile.TemporaryDirectory() as name:
   root=Path(name);self.fixture(root)
   second=root/'Assets/_Project/Scenes/Sandbox.unity';second.write_text(scene('concurrent','classic ground'))
   first=root/'Assets/_Project/Scenes/Main Menu.unity';before=first.read_bytes()
   with self.assertRaises(SceneConflict):controls.apply(root)
   self.assertEqual(first.read_bytes(),before)
 def test_dry_run_writes_reviewable_patch_without_scene_changes(self):
  with tempfile.TemporaryDirectory() as name:
   root=Path(name);folder=self.fixture(root);first=root/'Assets/_Project/Scenes/Main Menu.unity';before=first.read_bytes()
   controls.apply(root,dry_run=True)
   self.assertEqual(first.read_bytes(),before);self.assertIn('+  m_Name: after',(folder/'proposed.patch').read_text())
 def test_apply_retains_current_map_and_does_not_replace_historical_manifest(self):
  with tempfile.TemporaryDirectory() as name:
   root=Path(name);self.fixture(root);manifest=root/'docs/controls-lighting-manifest.json';manifest.parent.mkdir();manifest.write_text('historical evidence')
   controls.apply(root)
   self.assertEqual((root/'Assets/_Project/Scenes/Sandbox.unity').read_text(),scene('after','classic ground'))
   self.assertEqual(manifest.read_text(),'historical evidence')

if __name__=='__main__':unittest.main()
