#!/usr/bin/env python3
"""Attach the generated facility without reserializing unrelated Sandbox objects."""
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
generated = json.loads((ROOT / "TestResults/SandboxExpansion/generated.json").read_text())
scene = ROOT / "Assets/_Project/Scenes/Sandbox.unity"
source = scene.read_text()
instance_id = "1900000001"
if "&" + instance_id in source:
    raise SystemExit("Facility already attached; inspect changes before regenerating.")
old_terrain = "c506a51442f19464d8c91fd8c8b31888"
assert source.count("m_TerrainData: {fileID: 15600000, guid: " + old_terrain) == 2
source = source.replace("m_TerrainData: {fileID: 15600000, guid: " + old_terrain,
                        "m_TerrainData: {fileID: 15600000, guid: " + generated["terrain_guid"])
block = """--- !u!1001 &1900000001
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {fileID: 0}
    m_Modifications:
    - target: {fileID: ROOT_ID, guid: PREFAB_GUID, type: 3}
      propertyPath: m_Name
      value: Underground Light Lab and Perimeter
      objectReference: {fileID: 0}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {fileID: 100100000, guid: PREFAB_GUID, type: 3}
"""
block = block.replace("ROOT_ID", str(generated["root_id"])).replace("PREFAB_GUID", generated["prefab_guid"])
marker = "--- !u!1660057539 &9223372036854775807"
source = source.replace(marker, block + marker)
source += "  - {fileID: " + instance_id + "}\n"
scene.write_text(source)
manifest = dict(generated, baseline="39c76a6bed7d7dc0749cfe066727becd7b7aaf6d",
    instance_id=instance_id, original_terrain_guid=old_terrain,
    terrain_path="Assets/_Project/Environment/UndergroundFacility/TunnelTerrain.asset",
    prefab_path="Assets/_Project/Environment/UndergroundFacility/UndergroundFacility.prefab")
(ROOT / "docs/sandbox-expansion-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
