"""Expose the PS2 stage atlases and placed prefabs as Unity editor-only palettes.

Existing PNGs, metas and palettes are kept so edits made in Unity are not lost.
The palettes live under Editor and are never dependencies of runtime stages.
"""
import re
import uuid
from pathlib import Path

from replace_complete_maps_with_ps2 import ROOT, STAGES, archive_textures
from extract_ps2_hud import ISO, read_file


TEXTURES = STAGES / "StageTextures"
PALETTES = ROOT / "Assets/GainGround/Editor/StageTexturePalettes"
SCRIPT = ROOT / "Assets/GainGround/Editor/StageTexturePalette.cs"
MAP_META = STAGES / "Complete/Maps/1-06.png.meta"


def guid(path):
    match = re.search(r"^guid: ([a-f0-9]{32})$", path.read_text(encoding="utf-8"), re.M)
    if not match:
        raise ValueError(f"Missing Unity GUID: {path}")
    return match.group(1)


def ensure_folder(path):
    path.mkdir(parents=True, exist_ok=True)
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():
        meta.write_text(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")


def ensure_image(path, image):
    if not path.exists():
        image.save(path, optimize=True)
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():
        template = MAP_META.read_text(encoding="utf-8")
        template = re.sub(r"^guid: [a-f0-9]{32}$", f"guid: {uuid.uuid4().hex}", template, count=1, flags=re.M)
        meta.write_text(template, encoding="utf-8")
    return guid(meta)


def object_links(stage_path):
    text = stage_path.read_text(encoding="utf-8")
    results = []
    seen = set()
    for section in ("objects", "hazards"):
        match = re.search(rf"^  {section}:\n(.*?)(?=^  [a-zA-Z]\w*:|\Z)", text, re.M | re.S)
        if not match:
            continue
        pairs = re.findall(r"^  - id: ([^\n]+)\n    prefab: \{fileID: (\d+), guid: ([a-f0-9]{32}), type: 3\}", match.group(1), re.M)
        for name, file_id, prefab_guid in pairs:
            if prefab_guid in seen:
                continue
            seen.add(prefab_guid)
            results.append((name, file_id, prefab_guid))
    return results


def unity_text(value):
    return '"' + value.replace('\\', '\\\\').replace('"', '\\"') + '"'


def palette_yaml(name, stage_guid, texture_guids, links, script_guid):
    lines = [
        "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000",
        "MonoBehaviour:", "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}",
        f"  m_Name: {name}", "  m_EditorClassIdentifier: ",
        f"  stage: {{fileID: 11400000, guid: {stage_guid}, type: 2}}",
    ]
    for field in ("floor", "elevations", "constructions", "exitBanner"):
        lines.append(f"  {field}: {{fileID: 2800000, guid: {texture_guids[field]}, type: 3}}")
    if links:
        lines.append("  objects:")
        for label, file_id, prefab_guid in links:
            lines.append(f"  - id: {unity_text(label)}")
            lines.append(f"    prefab: {{fileID: {file_id}, guid: {prefab_guid}, type: 3}}")
    else:
        lines.append("  objects: []")
    return "\n".join(lines) + "\n"


def main():
    ensure_folder(TEXTURES)
    ensure_folder(PALETTES)
    script_meta = SCRIPT.with_name(SCRIPT.name + ".meta")
    if not script_meta.exists():
        template = (ROOT / "Assets/GainGround/Editor/PS2StageObjectInstaller.cs.meta").read_text(encoding="utf-8")
        template = re.sub(r"^guid: [a-f0-9]{32}$", f"guid: {uuid.uuid4().hex}", template, count=1, flags=re.M)
        script_meta.write_text(template, encoding="utf-8")
    script_guid = guid(script_meta)

    with ISO.open("rb") as stream:
        archive = read_file(stream, "DATA/DATA2.ROM")
    banner = archive_textures(archive, 1, 1)[0]["r1s01_a.tm2"].crop((0, 0, 512, 40))
    banner_guid = ensure_image(TEXTURES / "ExitBanner.png", banner)
    created = 0
    for round_number in range(1, 6):
        for stage_number in range(1, 11):
            prefix = f"r{round_number}s{stage_number:02}"
            stage_name = f"Stage_{round_number}_{stage_number}"
            stage_path = STAGES / f"{stage_name}.asset"
            atlas, _ = archive_textures(archive, round_number, stage_number)
            folder = TEXTURES / f"{round_number}-{stage_number:02}"
            ensure_folder(folder)
            construction = atlas[f"{prefix}_a.tm2"]
            elevations = atlas.get(f"{prefix}_b.tm2")
            if elevations is None:
                elevations = construction.crop((0, 48, 192, 216))
            texture_guids = {
                "floor": ensure_image(folder / "Floor.png", atlas[f"{prefix}_c.tm2"]),
                "elevations": ensure_image(folder / "Elevations.png", elevations),
                "constructions": ensure_image(folder / "Constructions.png", construction),
                "exitBanner": banner_guid,
            }
            palette = PALETTES / f"{stage_name}.asset"
            if not palette.exists():
                palette.write_text(palette_yaml(stage_name, guid(stage_path.with_name(stage_path.name + ".meta")),
                                               texture_guids, object_links(stage_path), script_guid), encoding="utf-8")
                created += 1
            meta = palette.with_name(palette.name + ".meta")
            if not meta.exists():
                meta.write_text(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    print(f"Exposed 50 stage palettes ({created} newly created) in {PALETTES}")


if __name__ == "__main__":
    main()
