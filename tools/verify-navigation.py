#!/usr/bin/env python3
"""Check asset preservation; optionally run real Unity navigation checks and a compilation smoke build."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = '32da32f8cecab6ba03755865ae0a5c828a743de8'
SCENES = 'Assets/_Project/Scenes/'


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)


def documents(data):
    return {m[2]: m[0] for m in re.finditer(r'^--- !u!(\d+) &(\d+)[^\n]*\n.*?(?=^--- !u!|\Z)', data, re.M | re.S)}


def static_checks():
    checks = []
    def check(condition, label):
        if not condition:
            raise AssertionError(label)
        checks.append(label)

    tracked = git('ls-tree', '-r', '--name-only', BASE).decode().splitlines()
    mutable = {
        SCENES + 'Main Menu.unity', SCENES + 'Sandbox.unity',
        'Assets/_Project/Scripts/Core/GameManager.cs', 'Assets/_Project/Scripts/UI/MenuController.cs',
        *('Assets/_Project/Scripts/Vehicle/Tumbler/' + f + '.cs' for f in
          ['TumblerController', 'Boost', 'TumblerSystems', 'EngineAudio']),
        'ProjectSettings/EditorBuildSettings.asset', 'ProjectSettings/ProjectSettings.asset',
    }
    preserved = 0
    # Compare Git object hashes, not timestamps; every original meta, package and physics/art source stays exact.
    for row in git('ls-tree', '-r', '-z', BASE).split(b'\0'):
        if not row:
            continue
        info, raw_path = row.split(b'\t', 1)
        name = raw_path.decode()
        if name.startswith(('Assets/', 'Packages/', 'ProjectSettings/')) and name not in mutable:
            data = (ROOT / name).read_bytes()
            digest = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
            if digest != info.split()[2].decode():
                raise AssertionError('Unexpected asset change: ' + name)
            preserved += 1
    check(True, f'{preserved} existing assets/settings preserved byte-for-byte, including every original meta and both input sources')

    before = documents(git('show', BASE + ':' + SCENES + 'Sandbox.unity').decode())
    after = documents((ROOT / SCENES / 'Sandbox.unity').read_text())
    check(set(after) - set(before) == {'1824967747'}, 'Sandbox adds exactly one pause component')
    check(not set(before) - set(after), 'No Sandbox object removed')
    check(all(before[k] == after[k] for k in before if k not in {'1824967743', '1824967746'}),
          'Every vehicle, camera, collider, HUD, lighting and environment document preserved')
    check(after['1824967746'] == before['1824967746'].replace('m_MethodName: QuitGame', 'm_MethodName: TogglePause'),
          'PlayerInput changes only the pause callback; IDs and other callbacks preserved')
    menu_before = git('show', BASE + ':' + SCENES + 'Main Menu.unity').decode()
    check((ROOT / SCENES / 'Main Menu.unity').read_text() == menu_before.replace('m_text: Quit sandbox', 'm_text: Pause / resume'),
          'Main Menu layout and Controls/Back callbacks preserved; only obsolete help label changed')

    seen = {}
    for file in (ROOT / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([a-f0-9]{32})$', file.read_text(), re.M)
        if match:
            check(match[1] not in seen, 'Unique GUID: ' + str(file.relative_to(ROOT)))
            seen[match[1]] = file
    new = [p for p in (ROOT / 'Assets').rglob('*') if str(p.relative_to(ROOT)) not in tracked and not p.name.endswith('.meta')]
    check(all(p.with_name(p.name + '.meta').exists() for p in new), 'All new Unity files and folders have metadata')
    intro = (ROOT / SCENES / 'Intro.unity').read_text()
    ids = documents(intro)
    for guid in re.findall(r'guid: ([a-f0-9]{32})', intro):
        check(guid in seen or guid.startswith('000000'), 'Intro external reference resolves: ' + guid)
    for match in re.finditer(r'\{fileID: (-?\d+)([^}]*)\}', intro):
        if 'guid:' not in match[2] and match[1] != '0':
            check(match[1] in ids, 'Intro local reference resolves: ' + match[1])
    check(re.findall(r'path: (.+)', (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text()) ==
          [SCENES + x + '.unity' for x in ['Intro', 'Main Menu', 'Sandbox']], 'Build order: Intro, Main Menu, Sandbox')

    manifest = json.loads((ROOT / 'docs/navigation/brand-sources.json').read_text())
    for name, digest in manifest['sha256'].items():
        check(hashlib.sha256((ROOT / manifest['asset_directory'] / name).read_bytes()).hexdigest() == digest,
              'Original kit bytes: ' + name)
    for file in (ROOT / 'Assets/_Project/Scripts').rglob('*.cs'):
        source = re.sub(r'#if UNITY_EDITOR\b.*?#endif', '', file.read_text(), flags=re.S)
        check('UnityEditor' not in source, 'No unguarded runtime Editor reference: ' + file.name)
    return {'status': 'PASS', 'baseline': BASE, 'preserved_assets': preserved, 'checks': checks,
            'unity_compilation': 'NOT_RUN', 'play_mode': 'NOT_RUN', 'standalone': 'NOT_RUN'}


def native_checks(unity, target):
    if not unity.is_file():
        raise RuntimeError('Unity Editor executable not found: ' + str(unity))
    destination = ROOT / 'Assets/_NavigationVerification'
    if destination.exists() or destination.with_suffix('.meta').exists():
        raise RuntimeError('Temporary verification path already exists; refusing to overwrite it')
    if (ROOT / 'Temp/UnityLockfile').exists():
        raise RuntimeError('Close this project in Unity before running the batch verifier')
    report = ROOT / 'TestResults/Navigation'
    report.mkdir(parents=True, exist_ok=True)
    (report / 'complete.txt').unlink(missing_ok=True)
    scenes = {p: p.read_bytes() for p in (ROOT / SCENES).glob('*.unity')}
    font = ROOT / 'Assets/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset'
    font_bytes = font.read_bytes()
    try:
        editor = destination / 'Editor'
        editor.mkdir(parents=True)
        shutil.copy2(ROOT / 'tools/navigation/NavigationVerify.cs', editor)
        env = dict(os.environ, TUMBLER_NAVIGATION_RESULTS=str(report))
        result = subprocess.run([str(unity.resolve()), '-batchmode', '-nographics', '-projectPath', str(ROOT),
            '-executeMethod', 'NavigationVerify.Run', '-logFile', str(report / 'unity.log')],
            cwd=ROOT, env=env, timeout=900)
        if result.returncode or not (report / 'complete.txt').is_file():
            raise RuntimeError('Unity verification incomplete; inspect TestResults/Navigation/unity.log')
        if target:
            env['TUMBLER_NAVIGATION_BUILD_TARGET'] = target
            result = subprocess.run([str(unity.resolve()), '-batchmode', '-nographics', '-projectPath', str(ROOT),
                '-executeMethod', 'NavigationVerify.Build', '-logFile', str(report / 'build.log')],
                cwd=ROOT, env=env, timeout=1800)
            if result.returncode:
                raise RuntimeError('Standalone compilation failed; inspect TestResults/Navigation/build.log')
        if any(p.read_bytes() != data for p, data in scenes.items()):
            raise RuntimeError('Verifier unexpectedly modified a scene')
        return {'play_mode': 'PASS', 'standalone_compilation': 'PASS' if target else 'NOT_RUN',
                'standalone_execution': 'NOT_RUN', 'build_target': target}
    finally:
        font.write_bytes(font_bytes)
        shutil.rmtree(destination)
        destination.with_suffix('.meta').unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', type=Path)
    parser.add_argument('--build-target', choices=['Windows', 'Linux'])
    args = parser.parse_args()
    if args.build_target and not args.unity:
        parser.error('--build-target requires --unity')
    try:
        result = static_checks()
        folder = ROOT / 'TestResults/Navigation'
        folder.mkdir(parents=True, exist_ok=True)
        (folder / 'static.json').write_text(json.dumps(result, indent=2) + '\n')
        if args.unity:
            result['native'] = native_checks(args.unity, args.build_target)
        print(json.dumps({k: v for k, v in result.items() if k != 'checks'}, indent=2))
    except (AssertionError, RuntimeError, subprocess.TimeoutExpired, OSError) as error:
        print(str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
