"""Validate the organic map's exact scene delta before older feature checks normalize it."""
import hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
MANIFEST=json.loads((ROOT/'docs/organic-sandbox/manifest.json').read_text())
def documents(text):
 return {m[1]:m[0] for m in re.finditer(r'^--- !u!\d+ &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\Z)',text,re.M|re.S)}
def before_organic(text):
 current=documents(text)
 baseline=subprocess.check_output(['git','show',MANIFEST['baseline']+':Assets/_Project/Scenes/Sandbox.unity'],cwd=ROOT,text=True)
 original=documents(baseline)
 for key,digest in MANIFEST['scene_documents'].items():
  block=current.get(key,'')
  if hashlib.sha256(block.encode()).hexdigest()!=digest:raise AssertionError('Organic layout scene delta changed: '+key)
  text=text.replace(block,original.get(key,''),1)
 return text
