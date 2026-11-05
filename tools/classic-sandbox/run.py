#!/usr/bin/env python3
"""Run temporary classic-sandbox authoring or verification in a closed Unity checkout."""
import argparse
from pathlib import Path
import sys
ROOT=Path(__file__).resolve().parents[2]
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'tools'))
from unity_editor_tools import DEFAULT_EDITOR,run_editor

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('method')
    parser.add_argument('--project',default=str(ROOT))
    parser.add_argument('--unity',default=DEFAULT_EDITOR)
    parser.add_argument('--graphics',action='store_true')
    parser.add_argument('--scene-source')
    args=parser.parse_args()
    run_editor(args.method,args.project,list(HERE.glob('Classic*.cs'))+[ROOT/'tools/organic-sandbox/OrganicGeometry.cs'],'ClassicSandbox','_ClassicSandboxWork',args.unity,args.graphics,args.scene_source)
