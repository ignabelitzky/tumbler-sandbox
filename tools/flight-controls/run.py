#!/usr/bin/env python3
"""Install temporary Editor tools, run Unity, then restore pre-existing editor caches."""
import argparse,subprocess,shutil
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('method');p.add_argument('--project',default=str(Path(__file__).resolve().parents[2]));p.add_argument('--unity',default='/home/ignacio/Unity/Hub/Editor/6000.6.7f1/Editor/Unity');p.add_argument('--graphics',action='store_true');p.add_argument('--scene-source');a=p.parse_args()
r=Path(a.project);here=Path(__file__).resolve().parent;temp=r/'Assets/_FlightWork';assert not temp.exists(),temp
preserve=list((r/'Assets/HDRPDefaultResources').glob('*.asset'))+list((r/'Assets/TextMeshPro').rglob('*.asset'))
backup={p:p.read_bytes() for p in preserve}
scene=r/'Assets/_Project/Scenes/Sandbox.unity'
if a.scene_source:backup[scene]=scene.read_bytes()
log=r/'TestResults/Flight';log.mkdir(parents=True,exist_ok=True)
try:
 if a.scene_source:
  scene.write_bytes(Path(a.scene_source).read_bytes())
  for f in preserve:
   baseline=subprocess.run(['git','show','HEAD:'+str(f.relative_to(r))],cwd=r,capture_output=True)
   if baseline.returncode==0:f.write_bytes(baseline.stdout)
 (temp/'Editor').mkdir(parents=True)
 for f in here.glob('Flight*.cs'):shutil.copy2(f,temp/'Editor')
 subprocess.run([a.unity,'-batchmode',*(['-force-vulkan'] if a.graphics else ['-nographics']),'-projectPath',str(r),'-executeMethod',a.method,'-logFile',str(log/(a.method+'.log'))],cwd=r,check=True,timeout=1500)
finally:
 for f,data in backup.items():
  if f.read_bytes()!=data:f.write_bytes(data)
 shutil.rmtree(temp);temp.with_suffix('.meta').unlink(missing_ok=True)
