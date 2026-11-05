#!/usr/bin/env python3
"""Apply only owned controls/fire edits; preflight both current scenes before writing."""
import argparse
import difflib
import hashlib
import json
from pathlib import Path
import os
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
from scene_documents import SceneConflict,merge_owned


def atomic_write(path, text):
    descriptor, name = tempfile.mkstemp(prefix='.scene-', suffix='.tmp', dir=path.parent)
    try:
        with os.fdopen(descriptor, 'w') as stream:
            stream.write(text)
        os.chmod(name, path.stat().st_mode)
        os.replace(name, path)
    finally:
        Path(name).unlink(missing_ok=True)


def apply(root, dry_run=False):
    folder = root / 'TestResults/ControlsLighting'
    allowed = json.loads((folder / 'allowed.json').read_text())
    plans = {}
    for scene, label, ownership in [('Main Menu', 'Menu', 'menuAllowedIds'),
                                     ('Sandbox', 'Sandbox', 'sandboxAllowedIds')]:
        path = root / ('Assets/_Project/Scenes/' + scene + '.unity')
        # Missing snapshots fail instead of falling back to a historical revision.
        current = path.read_text()
        baseline = (folder / ('Original' + label + '.unity')).read_text()
        authored = (folder / ('Expanded' + label + '.unity')).read_text()
        output = merge_owned(current, baseline, authored, allowed[ownership])
        plans[path] = (current, output)

    patch = ''.join(''.join(difflib.unified_diff(before.splitlines(True), after.splitlines(True),
                    fromfile=str(path.relative_to(root)), tofile=str(path.relative_to(root))))
                    for path, (before, after) in plans.items())
    (folder / 'proposed.patch').write_text(patch)
    if dry_run:
        return plans
    if any(path.read_text()!=before for path,(before,after) in plans.items()):
        raise SceneConflict("A scene changed after preflight; no files were written")
    written = []
    try:
        for path, (before, after) in plans.items():
            if before != after:
                atomic_write(path, after)
                written.append(path)
    except Exception:
        for path in written:
            atomic_write(path, plans[path][0])
        raise
    report = {str(path.relative_to(root)): {
        'before_sha256': hashlib.sha256(before.encode()).hexdigest(),
        'after_sha256': hashlib.sha256(after.encode()).hexdigest()
    } for path, (before, after) in plans.items()}
    (folder / 'applied.json').write_text(json.dumps(report, indent=2) + '\n')
    return plans


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dry-run', action='store_true')
    args = parser.parse_args()
    apply(ROOT, args.dry_run)
    print(('Prepared' if args.dry_run else 'Applied') + ' owned edits; historical preservation manifests retained.')
