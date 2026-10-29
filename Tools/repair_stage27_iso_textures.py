"""Restore stage 2-7's editable tiles and wall faces from its PS2 map.

Run after updating Complete/Maps/2-07.png. Existing Unity tile GUIDs are kept.
"""

from io import BytesIO
from pathlib import Path
import re
import uuid

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
CAMPAIGN = ROOT / "Assets/GainGround/Editable/OriginalCampaign"
MAP = CAMPAIGN / "Complete/Maps/2-07.png"
STAGE = CAMPAIGN / "Stage_2_7.asset"
PREFAB = CAMPAIGN / "PaintedStages/Stage_2_7_Layout.prefab"
TILES = CAMPAIGN / "PaintedStages/Tiles/Stage_2_7"
ART = CAMPAIGN / "PaintedStages/Art"
META_TEMPLATE = ART / "Stage_2_2_CastleWall.png.meta"


def save_png(path: Path, image: Image.Image) -> None:
    output = BytesIO()
    image.save(output, format="PNG")
    if not path.exists() or path.read_bytes() != output.getvalue():
        path.write_bytes(output.getvalue())


def wall_rectangles(stage_text: str):
    block = stage_text.split("  walls:\n", 1)[1].split("  enemies:\n", 1)[0]
    return [tuple(map(float, match)) for match in re.findall(
        r"    x: ([\d.-]+)\n    y: ([\d.-]+)\n    width: ([\d.-]+)\n    height: ([\d.-]+)",
        block,
    )]


def main() -> None:
    image = Image.open(MAP).convert("RGBA")
    if image.size != (1536, 1792):
        raise ValueError(f"Unexpected 2-7 map dimensions: {image.size}")
    if len(list(TILES.glob("Original_*.png"))) != 168:
        raise ValueError("Stage 2-7 editable tile set is incomplete")

    stage_text = STAGE.read_text(encoding="utf-8-sig")
    walls = wall_rectangles(stage_text)
    if len(walls) != 11:
        raise ValueError(f"Unexpected 2-7 wall count: {len(walls)}")
    source_guid = re.search(r"^guid: ([0-9a-f]{32})$", MAP.with_suffix(".png.meta").read_text(), re.M).group(1)
    stage_text, count = re.subn(
        r"(?m)^  background: \{fileID: 21300000, guid: [0-9a-f]{32}, type: 3\}$",
        f"  background: {{fileID: 21300000, guid: {source_guid}, type: 3}}",
        stage_text,
    )
    if count != 1:
        raise ValueError("Stage background reference not found")

    # PNG rows have a top origin; Unity tile rows have a bottom origin.
    for row in range(14):
        for col in range(12):
            crop = image.crop((col * 128, (13 - row) * 128,
                               (col + 1) * 128, (14 - row) * 128))
            save_png(TILES / f"Original_{col:02}_{row:02}.png", crop)

    template = META_TEMPLATE.read_text(encoding="utf-8-sig")
    face_guids = []
    for index, (x, y, width, height) in enumerate(walls, 1):
        bounds = (round((x + 6) * 128), round((7 - y - height) * 128),
                  round((x + 6 + width) * 128), round((7 - y) * 128))
        if bounds[2] > image.width or bounds[3] > image.height:
            raise ValueError(f"Wall {index} exceeds map bounds: {bounds}")
        path = ART / f"Stage_2_7_ISOWall_{index:02}.png"
        save_png(path, image.crop(bounds))
        meta_path = path.with_suffix(".png.meta")
        if meta_path.exists():
            guid = re.search(r"^guid: ([0-9a-f]{32})$", meta_path.read_text(), re.M).group(1)
        else:
            guid = uuid.uuid5(uuid.NAMESPACE_URL, f"gain-ground/stage-2-7/wall/{index:02}").hex
            meta_path.write_text(re.sub(r"(?m)^guid: [0-9a-f]{32}$", f"guid: {guid}", template), encoding="utf-8")
        face_guids.append(guid)

    chunks = re.split(r"(?=--- !u!1 &)", PREFAB.read_text(encoding="utf-8-sig"))
    found = set()
    for chunk_index, chunk in enumerate(chunks):
        match = re.search(r"(?m)^  m_Name: Corridor elevation face (\d+)$", chunk)
        if not match:
            continue
        index = int(match.group(1))
        if index < 1 or index > len(walls) or index in found:
            raise ValueError(f"Unexpected corridor face {index}")
        found.add(index)
        chunk, scale_count = re.subn(r"(?m)^  m_LocalScale: \{[^\n]+\}$",
                                     "  m_LocalScale: {x: 1, y: 1, z: 1}", chunk, count=1)
        chunk, sprite_count = re.subn(r"(?m)^  m_Sprite: \{fileID: 21300000, guid: [0-9a-f]{32}, type: 3\}$",
                                      f"  m_Sprite: {{fileID: 21300000, guid: {face_guids[index - 1]}, type: 3}}", chunk, count=1)
        if scale_count != 1 or sprite_count != 1:
            raise ValueError(f"Could not update corridor face {index}")
        chunks[chunk_index] = chunk
    if len(found) != len(walls):
        raise ValueError(f"Only found {len(found)} of {len(walls)} corridor faces")
    STAGE.write_text(stage_text, encoding="utf-8", newline="\n")
    PREFAB.write_text("".join(chunks), encoding="utf-8", newline="\n")
    print("2-7: 168 editable tiles and 11 wall faces now use the PS2 map texture")


if __name__ == "__main__":
    main()
