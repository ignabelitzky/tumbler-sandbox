#!/usr/bin/env python3
"""Check authored layout, collider ownership and preservation; no runtime claims."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
from scene_documents import documents, validate_references
from simplify import BASE, PREFAB, REMOVED, simplify


def verify():
    original = subprocess.check_output(['git', 'show', BASE + ':' + PREFAB], cwd=ROOT).decode()
    current = (ROOT / PREFAB).read_text()
    assert current == simplify(original), 'Prefab differs from reviewed minimal authoring'
    blocks = documents(current)
    validate_references(current)
    games = {i: b for i, b in blocks.items() if b.startswith('--- !u!1 ')}
    transforms = {i: b for i, b in blocks.items() if b.startswith('--- !u!4 ')}
    names = {re.search(r'  m_Name: (.*)', b)[1] for b in games.values()}
    assert not names & REMOVED
    assert {'Original square_ramp', 'Original curved_ramp', 'Low rolling tabletop',
            'Progressive speed bumps', 'Alternating wheel articulation',
            'Rolling gravel and shallow hollows', 'Gentle classic terrain',
            'Tunnel west approach', 'Tunnel east return'} <= names
    # Parent/child and GameObject/component links are checked in BOTH directions.
    for gid, game in games.items():
        for cid in re.findall(r'component: \{fileID: (\d+)\}', game):
            assert f'  m_GameObject: {{fileID: {gid}}}\n' in blocks[cid], cid
    for tid, transform in transforms.items():
        parent = re.search(r'  m_Father: \{fileID: (\d+)\}', transform)[1]
        if parent != '0':
            assert f'  - {{fileID: {tid}}}\n' in transforms[parent], tid
        for child in re.findall(r'^  - \{fileID: (\d+)\}', transform, re.M):
            assert f'  m_Father: {{fileID: {tid}}}\n' in transforms[child], child
    for name in ['West perimeter', 'East perimeter', 'South perimeter', 'North perimeter']:
        game = next(g for g in games.values() if f'  m_Name: {name}\n' in g)
        components = [blocks[i] for i in re.findall(r'component: \{fileID: (\d+)\}', game)]
        assert len(components) == 4
        assert any(b.startswith('--- !u!23 ') and 'm_Enabled: 1' in b for b in components)
        collider = next(b for b in components if b.startswith('--- !u!65 '))
        assert 'm_IsTrigger: 0' in collider and 'm_Enabled: 1' in collider
        assert 'm_Size: {x: 1, y: 1, z: 1}' in collider
    # No editor reserialization, tunnel, camera, audio, input, navigation, scene,
    # mass, inertia or Windows build configuration changes are permitted here.
    allowed = {PREFAB, 'Assets/_Project/Scripts/UI/MenuController.cs',
               'Assets/_Project/Scripts/Vehicle/Tumbler/TumblerFlightDynamics.cs'}
    unchanged = 0
    for entry in subprocess.check_output(['git', 'ls-tree', '-r', '-z', BASE,
                                          '--', 'Assets', 'Packages', 'ProjectSettings'], cwd=ROOT).split(b'\0'):
        if not entry:
            continue
        info, name = entry.split(b'\t', 1)
        path = name.decode()
        if path in allowed:
            continue
        data = (ROOT / path).read_bytes()
        sha = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
        assert sha == info.split()[2].decode(), 'Unexpected asset change: ' + path
        unchanged += 1
    for scene in (ROOT / 'Assets/_Project/Scenes').glob('*.unity'):
        validate_references(scene.read_text())
    report = dict(status='PASS',scope='static only',removed_objects=len(REMOVED),
                  solid_perimeter_walls=4,retained_ramps=3,unchanged_game_files=unchanged,
                  before_objects=len([b for b in documents(original).values() if b.startswith('--- !u!1 ')]),
                  after_objects=len(games),unity_play_mode='not evaluated by static checker')
    target = ROOT / 'TestResults/MinimalPlayground/static.json'
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report))


if __name__ == '__main__':
    verify()
