"""Shared installation, invocation and cleanup for temporary Unity Editor helpers."""
from pathlib import Path
import shutil
import subprocess
import sys

DEFAULT_EDITOR='/home/ignacio/Unity/Hub/Editor/6000.6.7f1/Editor/Unity'

def run_editor(method, project, helpers, results, temporary, unity=DEFAULT_EDITOR, graphics=False, scene_source=None):
    project=Path(project).resolve();temporary=project/'Assets'/temporary
    if (project/'Temp/UnityLockfile').exists():
        raise RuntimeError('This checkout is open in Unity. Use a closed disposable copy.')
    if temporary.exists() or temporary.with_suffix('.meta').exists():
        raise RuntimeError('Temporary helper path already exists: '+str(temporary))
    sources=list(map(Path,helpers));names=[p.name for p in sources]
    if len(names)!=len(set(names)):raise ValueError('Duplicate temporary helper filenames')
    preserve=list((project/'Assets/HDRPDefaultResources').glob('*.asset'))+list((project/'Assets/TextMeshPro').rglob('*.asset'))
    backup={p:p.read_bytes() for p in preserve}
    scene=project/'Assets/_Project/Scenes/Sandbox.unity'
    if scene_source is not None:backup[scene]=scene.read_bytes()
    output=project/'TestResults'/results;output.mkdir(parents=True,exist_ok=True)
    try:
        if scene_source is not None:scene.write_bytes(Path(scene_source).read_bytes())
        editor=temporary/'Editor';editor.mkdir(parents=True)
        for source in sources:shutil.copy2(source,editor/source.name)
        flags=(['-force-vulkan'] if sys.platform.startswith('linux') else []) if graphics else ['-nographics']
        subprocess.run([str(unity),'-batchmode',*flags,'-projectPath',str(project),'-executeMethod',method,
                        '-logFile',str(output/(method+'.log'))],cwd=project,check=True,timeout=1500)
    finally:
        for path,data in backup.items():
            if not path.exists() or path.read_bytes()!=data:path.write_bytes(data)
        if temporary.exists():shutil.rmtree(temporary)
        temporary.with_suffix('.meta').unlink(missing_ok=True)
