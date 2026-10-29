"""Audit every campaign enemy reference against locally extracted PS2 models.

This checks asset provenance and Unity links. The PS2 stage .map actor list also
contains scenery, so it must not be treated as an enemy spawn table.
"""

from collections import Counter
from hashlib import sha256
from pathlib import Path
import json
import re
import struct

from extract_ps2_hud import ISO, read_file


ROOT = Path(__file__).resolve().parents[1]
EDITABLE = ROOT / "Assets/GainGround/Editable"
CAMPAIGN = EDITABLE / "OriginalCampaign"
ACTORS = EDITABLE / "Characters/Models3D/EnemyActors"
SOURCES = ROOT / "Analysis/PS2Enemies"
INSTALLER = ROOT / "Assets/GainGround/Editor/PS2EnemyCampaignInstaller.cs"
REPORT = ROOT / "Validation/PS2EnemyAssetAudit.json"


def guid_map(folder):
    result = {}
    for meta in folder.glob("*.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})", meta.read_text(errors="ignore"), re.M)
        if match:
            result[match.group(1)] = meta.stem.replace(".png", "").replace(".prefab", "")
    return result


def source_map():
    text = INSTALLER.read_text(encoding="utf-8")
    entries = re.findall(r'\{"([^\"]+)",new\[\]\{([^}]+)\}\}', text)
    return {kind: re.findall(r'"([^"]+)"', models) for kind, models in entries}


def iso_models(archive):
    result = {}
    for match in re.finditer(rb"enemy\d\d[a-z]?\.lst\x00", archive):
        base = match.start()
        while base >= 32 and re.fullmatch(rb"[A-Za-z0-9_.-]+\.(lst|anm|bin|tm2)",
                                           archive[base - 32:base - 16].split(b"\0")[0]):
            base -= 32
        group = match.group()[:-5].decode("ascii")
        for index in range(512):
            entry = archive[base + index * 32:base + (index + 1) * 32]
            name = entry[:16].split(b"\0")[0]
            if not name:
                break
            if not name.endswith(b".bin"):
                continue
            offset, size = struct.unpack_from(">II", entry, 16)
            payload = archive[base + offset:base + offset + size]
            if len(payload) != size:
                raise ValueError("Truncated PS2 enemy asset " + name.decode("ascii"))
            result[(group, name[:-4].decode("ascii"))] = sha256(payload).hexdigest()
    return result


def source_record(name, manifest, iso_hashes):
    group, model = name.split("_", 1)
    original = SOURCES / group / (model + ".bin")
    converted = SOURCES / group / (model + ".json")
    record = manifest.get((group, model))
    if record is None or not original.exists() or not converted.exists():
        return None, "missing extracted ISO model " + name
    if sha256(original.read_bytes()).hexdigest() != record["sha256"]:
        return None, "ISO source hash mismatch " + name
    if iso_hashes.get((group, model)) != record["sha256"]:
        return None, "Current ISO does not match extracted model " + name
    return json.loads(converted.read_text(encoding="utf-8")), None


def stage_49_actor_positions(archive):
    """Read the 22 indexed actor positions decoded for the PS2 4-9 map."""
    base = archive.index(b"fr4s09.map\0")
    entry = archive[base:base + 32]
    offset, size = struct.unpack_from(">II", entry, 16)
    data = archive[base + offset:base + offset + size]
    if len(data) != size or data[:4] != b"00AG":
        raise ValueError("Invalid PS2 4-9 map")
    positions = {}
    for record in range(0x90000, min(len(data) - 80, 0xa0000), 16):
        actor = struct.unpack_from("<I", data, record + 16)[0]
        if actor not in range(26, 48):
            continue
        x, y, z, w = struct.unpack_from("<4f", data, record + 32)
        if -208 <= x <= 208 and -48 <= z <= 272 and -100 <= y <= 100 and w == 1:
            positions[actor] = (round(x / 32, 5), round((112 - z) / 32, 5))
    return [positions[index] for index in range(26, 48)]


def main():
    sources = source_map()
    manifest = {(entry["group"], entry["model"]): entry for entry in
                json.loads((SOURCES / "manifest.json").read_text(encoding="utf-8"))}
    with ISO.open("rb") as source:
        archive = read_file(source, "DATA/DATA2.ROM")
    iso_hashes = iso_models(archive)
    sprites = guid_map(CAMPAIGN / "Complete/Sprites")
    prefabs = guid_map(ACTORS)
    counts = Counter()
    stages = {}
    errors = []
    footprints = {}
    for stage in sorted(CAMPAIGN.glob("Stage_*_*.asset")):
        body = stage.read_text(encoding="utf-8").split("  enemies:\n", 1)
        if len(body) < 2:
            errors.append(stage.name + ": missing enemies list")
            continue
        blocks = body[1].split("  rescue:", 1)[0].split("  - position:")[1:]
        stages[stage.stem] = len(blocks)
        for index, block in enumerate(blocks):
            sprite = re.search(r"    sprite: .*guid: ([0-9a-f]{32})", block)
            model = re.search(r"    modelPrefab: .*guid: ([0-9a-f]{32})", block)
            size = re.search(r"    visualSize: \{x: [^,]+, y: ([^}]+)", block)
            if not sprite or not model or not size:
                errors.append(f"{stage.stem} enemy {index}: incomplete visual reference")
                continue
            kind = sprites.get(sprite.group(1))
            prefab = prefabs.get(model.group(1))
            # RunningKnight is a campaign behavior variant that intentionally
            # reuses the knight sprite and the same ISO-derived model.
            running_knight_alias = prefab == "RunningKnight" and kind == "knight"
            if kind is None or (prefab != kind and not running_knight_alias):
                errors.append(f"{stage.stem} enemy {index}: sprite={kind} model={prefab}")
                continue
            counts[kind] += 1
            footprints[kind] = max(footprints.get(kind, 0), float(size.group(1)))

    native_49 = stage_49_actor_positions(archive)
    stage_49 = (CAMPAIGN / "Stage_4_9.asset").read_text(encoding="utf-8")
    enemy_49 = stage_49.split("  enemies:\n", 1)[1].split("  rescue:", 1)[0]
    unity_49 = [(float(x), float(y)) for x, y in re.findall(
        r"  - position: \{x: ([^,]+), y: ([^}]+)\}", enemy_49)]
    if unity_49 != native_49:
        errors.append("Stage_4_9: actor positions differ from PS2 map")

    sizes = {}
    for kind, models in sorted(sources.items()):
        prefab_file = ACTORS / (kind + ".prefab")
        if not prefab_file.exists():
            errors.append("missing actor prefab " + kind)
            continue
        prefab_text = prefab_file.read_text(encoding="utf-8")
        for name in models:
            if name not in prefab_text:
                errors.append(f"{kind}: prefab does not contain PS2 model {name}")
        geometry = re.search(r"m_Name: Original assembled geometry.*?m_LocalScale: \{x: ([0-9.e-]+)",
                             prefab_text, re.S)
        rig = re.search(r"m_Name: Original PS2 rig.*?m_LocalScale: \{x: ([0-9.e-]+)",
                        prefab_text, re.S)
        if not geometry or not rig:
            errors.append("missing model transform " + kind)
            continue
        spans = []
        for name in models:
            data, error = source_record(name, manifest, iso_hashes)
            if error:
                errors.append(error)
                continue
            vertices = [vertex for part in data["parts"] for vertex in part["vertices"]]
            spans.append(max(max(vertex[axis] for vertex in vertices) -
                             min(vertex[axis] for vertex in vertices) for axis in ("x", "z")))
        if spans and kind in footprints:
            size = max(spans) * float(geometry.group(1)) * abs(float(rig.group(1))) * footprints[kind]
            sizes[kind] = round(size, 3)
            if kind not in {"dragon", "demon", "giant", "sorcererBlue", "sorcererRed", "tank", "computer"} and size > 3:
                errors.append(f"{kind}: projected footprint {size:.2f} world units")

    result = {
        "scope": "50 Unity campaign stages and PS2 model sources; not PS2 spawn-table equivalence",
        "isoFile": ISO.name,
        "isoVerifiedModelCount": sum(len(names) for names in sources.values()),
        "stageCount": len(stages),
        "enemyCount": sum(stages.values()),
        "nativeActorPositionMatches4_9": len(native_49) if unity_49 == native_49 else 0,
        "stages": stages,
        "kinds": dict(sorted(counts.items())),
        "largestProjectedFootprint": sizes,
        "errors": errors,
    }
    REPORT.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f'{result["stageCount"]} stages; {result["enemyCount"]} enemies; '
          f'{len(sources)} ISO model mappings; {len(errors)} errors. Report: {REPORT}')
    for error in errors:
        print("ERROR:", error)
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
