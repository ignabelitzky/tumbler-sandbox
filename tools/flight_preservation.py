"""Exact approved flight/control changes layered over the earlier map preservation checks."""
import hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
MANIFEST=json.loads((ROOT/'docs/flight-camera-landing/manifest.json').read_text())
def blocks(text):return {m[1]:m[0] for m in re.finditer(r'^--- !u!\d+ &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\Z)',text,re.M|re.S)}
def before_flight_menu(text):
 current=blocks(text);baseline=subprocess.check_output(['git','show',MANIFEST['baseline']+':Assets/_Project/Scenes/Main Menu.unity'],cwd=ROOT,text=True);old=blocks(baseline)
 for key,digest in MANIFEST['menu_documents'].items():
  value=current.get(key,'');assert hashlib.sha256(value.encode()).hexdigest()==digest,'Flight controls menu delta changed: '+key
  text=text.replace(value,old.get(key,''),1)
 return text
