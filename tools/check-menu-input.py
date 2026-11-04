#!/usr/bin/env python3
"""Check the shipped UI action bindings, menu references and package-only input backend."""
import json
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]


def documents(text):
    return {m[1]: m[0] for m in re.finditer(
        r'^--- !u!\d+ &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\Z)', text, re.M | re.S)}


def main():
    checks = 0
    def check(value, label):
        nonlocal checks
        if not value:
            raise AssertionError(label)
        checks += 1

    settings = (ROOT / 'ProjectSettings/ProjectSettings.asset').read_text()
    check(re.search(r'^  activeInputHandler: 1$', settings, re.M), 'Use Input System Package only')
    packages = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']
    check('com.unity.inputsystem' in packages, 'Input System package dependency')
    asset = json.loads((ROOT / 'Assets/_Project/Resources/MenuInput.inputactions').read_text())
    check(len(asset['maps']) == 1 and asset['maps'][0]['name'] == 'UI', 'Dedicated UI action map')
    check(not asset['controlSchemes'], 'UI does not restrict devices to a control scheme')
    ui = asset['maps'][0]
    actions = {a['name']: a for a in ui['actions']}
    expected = {
        'Navigate': ('PassThrough', 'Vector2'), 'Submit': ('Button', 'Button'),
        'Cancel': ('Button', 'Button'), 'Point': ('PassThrough', 'Vector2'),
        'Click': ('PassThrough', 'Button'), 'RightClick': ('PassThrough', 'Button'),
        'MiddleClick': ('PassThrough', 'Button'), 'ScrollWheel': ('PassThrough', 'Vector2'),
    }
    check(set(actions) == set(expected), 'Complete UI action set')
    for name, (kind, control) in expected.items():
        check((actions[name]['type'], actions[name]['expectedControlType']) == (kind, control),
              'Action type/control: ' + name)
    ids = [ui['id']] + [a['id'] for a in ui['actions']] + [b['id'] for b in ui['bindings']]
    check(len(ids) == len(set(ids)), 'Unique action and binding IDs')
    for value in ids:
        uuid.UUID(value)
    bindings = ui['bindings']
    check(all(b['action'] in actions for b in bindings), 'Every binding resolves')
    check(all(not b['groups'] for b in bindings), 'UI bindings accept all connected devices')
    for name, paths in {
        'Navigate': ['<Gamepad>/leftStick', '<Gamepad>/dpad', *('<Keyboard>/' + k for k in
                     ['upArrow', 'downArrow', 'leftArrow', 'rightArrow', 'w', 's', 'a', 'd'])],
        'Submit': ['<Gamepad>/buttonSouth', '<Keyboard>/enter', '<Keyboard>/numpadEnter', '<Keyboard>/space'],
        'Cancel': ['<Gamepad>/buttonEast', '<Keyboard>/escape'],
        'Point': ['<Pointer>/position'], 'Click': ['<Pointer>/press'],
        'RightClick': ['<Mouse>/rightButton'], 'MiddleClick': ['<Mouse>/middleButton'],
        'ScrollWheel': ['<Mouse>/scroll'],
    }.items():
        actual = {b['path'] for b in bindings if b['action'] == name}
        check(set(paths) <= actual, 'Required bindings: ' + name)
    for i, binding in enumerate(bindings):
        if binding['isComposite']:
            parts = []
            for part in bindings[i + 1:]:
                if not part['isPartOfComposite']:
                    break
                parts.append(part)
            check({p['name'] for p in parts} == {'up', 'down', 'left', 'right'}, 'Complete keyboard composite')

    scene = documents((ROOT / 'Assets/_Project/Scenes/Main Menu.unity').read_text())
    check('  m_DeselectOnBackgroundClick: 0\n' in scene['1017201477'], 'Background clicks preserve selection')
    check('  m_FirstSelected: {fileID: 1355747817}' in scene['1017201478'], 'Menu starts on Enter Sandbox')
    controller = scene['1376158916']
    check('  enterSandboxButton: {fileID: 1355747818}' in controller, 'Recovery button reference')
    check('  m_IsActive: 0\n' in scene['194054416'], 'Standalone author label is disabled')
    check("  m_text: ''\n" in scene['194054418'], 'Standalone author text is removed')
    for field, file in {
        'developerLogo': 'Assets/_Project/Art/Branding/Phosphor/phosphor-primary_2048.png',
        'creditFont': 'Assets/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset',
    }.items():
        guid = re.search(r'^guid: (\w+)', (ROOT / (file + '.meta')).read_text(), re.M)[1]
        check(re.search(r'^  ' + field + r': .*guid: ' + guid, controller, re.M), 'Credit reference: ' + field)
    for key, up, down in [('1355747818', '970955195', '150339307'),
                           ('150339307', '1355747818', '970955195'),
                           ('970955195', '150339307', '1355747818')]:
        check('m_SelectOnUp: {fileID: ' + up + '}' in scene[key] and
              'm_SelectOnDown: {fileID: ' + down + '}' in scene[key], 'Menu navigation link: ' + key)

    # Vendor demo scripts may still contain old APIs, but none may enter the
    # build-scene/prefab dependency graph with the legacy backend disabled.
    guid_paths = {}
    for meta in (ROOT / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: (\w+)', meta.read_text(), re.M)
        if match:
            guid_paths[match[1]] = meta.with_suffix('')
    build = (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text()
    pending = [ROOT / p for p in re.findall(r'^    path: (.+)$', build, re.M)]
    visited = set()
    legacy = re.compile(r'\b(?:UnityEngine\.)?Input\.(?:Get\w+|anyKey\w*|mousePosition|mouseScrollDelta)\b')
    while pending:
        path = pending.pop()
        if path in visited:
            continue
        visited.add(path)
        source = path.read_text()
        if path.suffix == '.cs':
            check(not legacy.search(source), 'No legacy API in live script: ' + str(path.relative_to(ROOT)))
        else:
            for guid in re.findall(r'guid: (\w+)', source):
                target = guid_paths.get(guid)
                if target is not None and target.suffix in {'.prefab', '.cs'}:
                    pending.append(target)
    check(len(visited) > 3, 'Build input dependency graph inspected')
    print(json.dumps(dict(status='PASS', checks=checks, backend='Input System Package',
                          native_ui_events='NOT_RUN (use verify-navigation.py --unity)')))


if __name__ == '__main__':
    main()
