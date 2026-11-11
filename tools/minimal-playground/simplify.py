#!/usr/bin/env python3
"""Explicit, repeatable prefab authoring; never runs during gameplay."""
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
from scene_documents import documents, validate_references

BASE = '41632d87b688402d64821be0b9575d15a2e0bc08'
PREFAB = 'Assets/_Project/Environment/ClassicSandbox/ClassicSandbox.prefab'
REMOVED = {
    'Open handling circuit', 'Open handling circuit blended shoulders',
    'West ramp approach', 'West ramp approach blended shoulders',
    'North ramp access', 'North ramp access blended shoulders',
    'Dirt meander', 'Dirt meander blended shoulders',
    'Sand handling curve', 'Sand handling curve blended shoulders',
    'Medium rolling tabletop', 'Medium rolling tabletop blended shoulders',
    'Original ramp_45', 'Original circular_bump', 'Original triangular_bump',
    'Low sandstone outcrops', 'Flush bump markings',
}


def simplify(source):
    blocks = documents(source)
    games = {i: b for i, b in blocks.items() if b.startswith('--- !u!1 ')}
    removed_games = {i for i, b in games.items()
                     if re.search(r'  m_Name: (.*)', b)[1] in REMOVED}
    if len(removed_games) != len(REMOVED):
        raise ValueError('Unexpected source layout')
    removed = removed_games | {
        i for g in removed_games for i in re.findall(r'component: \{fileID: (\d+)\}', games[g])}
    removed_transforms = {i for i in removed if blocks[i].startswith('--- !u!4 ')}
    root_transform = '2067189516479276970'
    output = source
    for i, block in blocks.items():
        if i in removed:
            output = output.replace(block, '', 1)
        elif block.startswith('--- !u!4 '):
            changed = block
            for t in removed_transforms:
                changed = changed.replace('  - {fileID: ' + t + '}\n', '')
            changed = changed.replace('  m_Children:\n  m_Father:', '  m_Children: []\n  m_Father:')
            if i == root_transform:
                changed = changed.replace('  m_Father:', ''.join(
                    f'  - {{fileID: {9001000002 + k * 10}}}\n' for k in range(4)) + '  m_Father:', 1)
            output = output.replace(block, changed, 1)
    # One visible, solid box per side. Corners overlap; bottoms are below the
    # existing terrain. No colliders remain from the removed trial objects.
    for k, (name, position, scale) in enumerate([
        ('West perimeter', (-475, 0, 0), (1.5, 4, 951.5)),
        ('East perimeter', (475, 0, 0), (1.5, 4, 951.5)),
        ('South perimeter', (0, 0, -475), (951.5, 4, 1.5)),
        ('North perimeter', (0, 0, 475), (951.5, 4, 1.5)),
    ]):
        # Reuse serialized Unity component schemas from the existing prefab.
        template_go = games['1387489008159567484']
        old_ids = ['1387489008159567484', '3910777648810011318',
                   '3588322316214861737', '2105117409442358827', '4256955360130331684']
        new_ids = [str(9001000001 + k * 10 + j) for j in range(5)]
        parts = [template_go] + [blocks[i] for i in old_ids[1:4]]
        wall = ''.join(parts)
        for old, new in zip(old_ids, new_ids):
            wall = wall.replace(old, new)
        wall = wall.replace('m_Name: Low sandstone outcrops', 'm_Name: ' + name)
        wall = re.sub(r'  m_LocalPosition: .*',
                      '  m_LocalPosition: {x: %s, y: %s, z: %s}' % position, wall)
        wall = wall.replace('  m_LocalScale: {x: 1, y: 1, z: 1}',
                            '  m_LocalScale: {x: %s, y: %s, z: %s}' % scale)
        wall = wall.replace('m_Father: {fileID: 3470502398245287908}',
                            'm_Father: {fileID: ' + root_transform + '}')
        wall = re.sub(r'  m_Mesh: .*',
                      '  m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}', wall)
        wall += f'''--- !u!65 &{new_ids[4]}
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {new_ids[0]}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {{x: 1, y: 1, z: 1}}
  m_Center: {{x: 0, y: 0, z: 0}}
'''
        output += wall
    validate_references(output)
    return output


if __name__ == '__main__':
    original = subprocess.check_output(['git', 'show', BASE + ':' + PREFAB], cwd=ROOT).decode()
    result = simplify(original)
    current = (ROOT / PREFAB).read_text()
    if current not in (original, result):
        raise SystemExit('Refusing to overwrite other prefab edits')
    (ROOT / PREFAB).write_text(result)
    print('Authored minimal playground: removed 17 objects; added 4 solid perimeter walls.')
