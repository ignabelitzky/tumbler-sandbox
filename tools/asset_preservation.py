"""Exact approved deltas layered over the older feature-preservation baselines."""
import hashlib
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
APPROVED_BLOBS = {
    # Portal repair / camera transitions (5713507).
    'Assets/_Project/Environment/UndergroundFacility/Tunnel walls and closed roof.asset': '9ed0cc9da00dfca9a69d486c629a5b46ceba8f36',
    'Assets/_Project/Environment/UndergroundFacility/UndergroundFacility.prefab': '21b83f2c6d1370513a3d4601a557e8a3d0875385',
    'Assets/_Project/Scripts/Camera/SandboxCameraModes.cs': '9d28ed56f3d4bdeb321721ad48ae33fd64afe70e',
    # Grounded burnout / HDRP smoke (ed15404, 3b5828a).
    'Assets/_Project/Scripts/Vehicle/Tumbler/TumblerController.cs': '5764521bd7d49f496dd23e408cd0ae33dfe75a95',
    'Assets/_Project/Scripts/Vehicle/Tumbler/TumblerEffects.cs': 'd9b3098271147216829d01c8d871f8061691155e',
    'Assets/_Project/Scripts/Vehicle/Tumbler/TumblerSystems.cs': '316fd9190777ce22202e9efc7d59ffdc155f7978',
}
# Exact serialized additions from the impact-ground/calibration change (0a70a26).
IMPACT_DOCUMENTS = {
    '875400000000000001': '511b38da1b07514b22aafb3c5015cf80029430b1ec33d687470e6b06aae23f6e',
    '875400000000000002': '7b6f729bb05aad287a0fd6466aac3896f496a0ded8b4bb396d453e8519f37ec5',
    '875400000000000004': '5e9551840c3a14505aefec6b0373596369d8bea6960b991e8010044b7a9e2932',
}


def expected_blob(path, original):
    """Require an exact approved revision, rather than excluding the path."""
    return APPROVED_BLOBS.get(path, original)


def scene_before_impact_ground(path):
    """Validate/remove only the approved impact delta from today's scene in memory.

    Older navigation/lighting checks still examine all other current documents;
    no scene is loaded from history in place of the working copy. The actual
    scene also goes through verify-impact-ground.py without normalization.
    """
    text = (ROOT / path).read_text(encoding='utf-8')
    if path != 'Assets/_Project/Scenes/Sandbox.unity':
        return text
    blocks = {m[1]: m[0] for m in re.finditer(
        r'^--- !u!\d+ &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\Z)', text, re.M | re.S)}
    for key, digest in IMPACT_DOCUMENTS.items():
        block = blocks.get(key, '')
        if hashlib.sha256(block.encode()).hexdigest() != digest:
            raise AssertionError('Missing or changed impact scene document: ' + key)
        text = text.replace(block, '', 1)
    for key, line in (
        ('2040198345356866087', '  - component: {fileID: 875400000000000004}\n'),
        ('9223372036854775807', '  - {fileID: 875400000000000002}\n'),
    ):
        block = blocks.get(key, '')
        if block.count(line) != 1:
            raise AssertionError('Missing or duplicate impact scene reference: ' + key)
        text = text.replace(block, block.replace(line, '', 1), 1)
    return text
