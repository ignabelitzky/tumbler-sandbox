#!/usr/bin/env python3
"""Run the real-wheel takeoff comparison in a closed Unity checkout."""
import argparse
from pathlib import Path
import sys
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
from unity_editor_tools import DEFAULT_EDITOR, run_editor

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', default=str(ROOT))
    parser.add_argument('--unity', default=DEFAULT_EDITOR)
    parser.add_argument('--navigation', action='store_true', help='Run existing intro/menu/pause verification')
    args = parser.parse_args()
    run_editor('NavigationVerify.Run' if args.navigation else 'RampTakeoffVerify.Run', args.project,
               [ROOT / 'tools/navigation/NavigationVerify.cs' if args.navigation
                else Path(__file__).with_name('RampTakeoffVerify.cs')],
               'Navigation' if args.navigation else 'RampTakeoff',
               '_RampTakeoffWork', args.unity)
