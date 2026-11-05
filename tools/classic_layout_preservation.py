"""Validate and normalize only the current classic-layout instance replacement."""
import hashlib,json,subprocess
from pathlib import Path
from scene_documents import documents
ROOT=Path(__file__).resolve().parents[1]
MANIFEST=json.loads((ROOT/'docs/classic-sandbox/manifest.json').read_text())

def before_classic(text):
    current=documents(text)
    old=documents(subprocess.check_output(['git','show',MANIFEST['baseline']+':Assets/_Project/Scenes/Sandbox.unity'],cwd=ROOT,text=True))
    for key,digest in MANIFEST['scene_documents'].items():
        block=current.get(key,'')
        if hashlib.sha256(block.encode()).hexdigest()!=digest:raise AssertionError('Classic map instance changed: '+key)
        text=text.replace(block,old.get(key,''),1)
    return text
