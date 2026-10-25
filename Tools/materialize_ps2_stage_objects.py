"""Write the PS2 object prefabs and stage references when Unity batch mode is unavailable.

The editor command PS2StageObjectInstaller.Install remains the normal way to
regenerate them. This script preserves GUIDs on repeat runs and touches only
the five explicitly mapped stage assets.
"""
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets/GainGround/Editable/OriginalCampaign"
OBJECTS = ASSETS / "Objects"
SPRITES = OBJECTS / "Sprites"
PREFABS = OBJECTS / "Prefabs"


def guid_for(path, importer):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.M).group(1)
    guid = uuid.uuid4().hex
    meta.write_text("fileFormatVersion: 2\nguid: " + guid + "\n" + importer,
                    encoding="utf-8")
    return guid


def folder(path):
    path.mkdir(parents=True, exist_ok=True)
    guid_for(path, "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                   "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def sprite_meta(path):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        template = meta.read_text(encoding="utf-8")
        guid = guid_for(path, "")
    else:
        template = (ASSETS / "Complete/Sprites/archer.png.meta").read_text(encoding="utf-8")
        guid = uuid.uuid4().hex
        template = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + guid, template, count=1, flags=re.M)
    for field, value in (("enableMipMap", 0), ("nPOTScale", 0),
                         ("spriteMode", 1), ("spriteMeshType", 0),
                         ("spritePixelsToUnits", 64), ("alphaIsTransparency", 1),
                         ("textureType", 8)):
        template, count = re.subn(r"^([ \t]*)" + field + r": \d+$",
                                  lambda match: f"{match.group(1)}{field}: {value}",
                                  template, count=1, flags=re.M)
        if count != 1:
            raise ValueError(f"Missing {field} in {meta}")
    meta.write_text(template, encoding="utf-8", newline="")
    return guid


def prefab_yaml(name, sprite_guid, tiled, order):
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &100000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 400000}}
  - component: {{fileID: 21200000}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &400000
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!212 &21200000
SpriteRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: {order}
  m_Sprite: {{fileID: 21300000, guid: {sprite_guid}, type: 3}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: {2 if tiled else 0}
  m_Size: {{x: 1, y: 1}}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 1
  m_MaskInteraction: 0
  m_SpriteSortPoint: 0
"""


def stage_path(round_number, number):
    return ASSETS / f"Stage_{round_number}_{number}.asset"


def object_entry(identifier, prefab_guid, area, group=0):
    x, y, w, h = area
    return (f"  - id: {identifier}\n"
            f"    prefab: {{fileID: 100000, guid: {prefab_guid}, type: 3}}\n"
            f"    area:\n      serializedVersion: 2\n"
            f"      x: {x}\n      y: {y}\n      width: {w}\n      height: {h}\n"
            f"    teleportGroup: {group}\n")


def install_stage(round_number, number, entries):
    path = stage_path(round_number, number)
    text = path.read_text(encoding="utf-8")
    if "  objects:\n" in text:
        text = re.sub(r"  objects:\n.*?(?=  hazards:)", "", text, count=1, flags=re.S)
    if "  hazards:" not in text:
        raise ValueError("Hazard anchor absent: " + str(path))
    text = text.replace("  hazards:", "  objects:\n" + "".join(entries) + "  hazards:", 1)
    path.write_text(text, encoding="utf-8", newline="\n")


def main():
    folder(OBJECTS)
    folder(SPRITES)
    folder(PREFABS)
    prefab_guids = {}
    for name, tiled, order in (("WoodWallBoards", True, 0), ("WoodWallCap", True, 0),
                               ("TeleportRing", False, 1), ("LaserStrip", True, 3),
                               ("StoneArch", False, -1), ("IronDoor", False, -1)):
        sprite_guid = sprite_meta(SPRITES / (name + ".png"))
        path = PREFABS / (name + ".prefab")
        prefab_guids[name] = guid_for(path, "PrefabImporter:\n  externalObjects: {}\n"
                                     "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
        if not path.exists():
            path.write_text(prefab_yaml(name, sprite_guid, tiled, order), encoding="utf-8")

    installer = ROOT / "Assets/GainGround/Editor/PS2StageObjectInstaller.cs"
    guid_for(installer, "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n"
             "  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n"
             "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

    stage = stage_path(1, 6).read_text(encoding="utf-8")
    section = stage.split("  walls:\n", 1)[1].split("  enemies:\n", 1)[0]
    rects = [tuple(float(match.group(key)) for key in ("x", "y", "w", "h"))
             for match in re.finditer(r"    x: (?P<x>-?[\d.]+)\n    y: (?P<y>-?[\d.]+)\n"
                                      r"    width: (?P<w>-?[\d.]+)\n    height: (?P<h>-?[\d.]+)", section)]
    if len(rects) != 4:
        raise ValueError("Unexpected wooden wall geometry")
    wood_entries = [object_entry(f"PS2:WoodWall:{i}",
                    prefab_guids["WoodWallBoards" if i < 2 else "WoodWallCap"], rect)
                    for i, rect in enumerate(rects)]
    # Visual bridge only: a solid wall here would block the stage's exit.
    wood_entries.append(object_entry("PS2:WoodWall:4", prefab_guids["WoodWallCap"],
                                     (-1.5, -2.0, 3.0, 1.5)))
    install_stage(1, 6, wood_entries)
    install_stage(4, 2, [
        object_entry("PS2:Teleport:A", prefab_guids["TeleportRing"], (-5.5, -5.2, 1.4, 1.4), 1),
        object_entry("PS2:Teleport:B", prefab_guids["TeleportRing"], (4.1, -5.2, 1.4, 1.4), 1)])
    install_stage(2, 3, [object_entry("PS2:StoneArch", prefab_guids["StoneArch"],
                                  (-5.25, 4.3, 2.5, 2.1))])
    install_stage(2, 8, [object_entry("PS2:IronDoor", prefab_guids["IronDoor"],
                                  (-1.0, 4.5, 2.0, 2.0))])
    path = stage_path(5, 7)
    text = path.read_text(encoding="utf-8")
    before, hazards = text.split("  hazards:\n", 1)
    hazards = re.sub(r"    prefab: \{fileID: .*?\}\n", "", hazards)
    if hazards.count("    sprite: {fileID: 0}") != 4:
        raise ValueError("Expected four laser hazards")
    hazards = hazards.replace("    sprite: {fileID: 0}\n",
        f"    sprite: {{fileID: 0}}\n    prefab: {{fileID: 100000, guid: {prefab_guids['LaserStrip']}, type: 3}}\n")
    path.write_text(before + "  hazards:\n" + hazards, encoding="utf-8", newline="\n")
    print("Created six original PS2 object prefabs and updated stages 1-6, 2-3, 2-8, 4-2, 5-7")


if __name__ == "__main__":
    main()
