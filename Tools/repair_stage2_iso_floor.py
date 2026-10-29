"""Restore PS2 terrain texels in editable stages 2-4, 2-6 and 2-7.

The old backgrounds used a different floor. Walls, the exit, and their painted
edge art are retained. The ISO map UVs and previously extracted PS2 floor atlas
are the only sources for replacement pixels. Existing per-cell tile assets keep
their Unity GUIDs, so the painted layouts update without prefab serialization.
"""

from io import BytesIO
from pathlib import Path
import re
import struct

from PIL import Image, ImageDraw

from extract_ps2_hud import ISO, read_file
from inventory_ps2_stage_models import entries


ROOT = Path(__file__).resolve().parents[1]
CAMPAIGN = ROOT / "Assets/GainGround/Editable/OriginalCampaign"
MAPS = CAMPAIGN / "Complete/Maps"
STAGES = (4, 6, 7)


def rectangles(block):
    return [tuple(map(float, match)) for match in re.findall(
        r"    x: ([\d.-]+)\n    y: ([\d.-]+)\n    width: ([\d.-]+)\n    height: ([\d.-]+)", block)]


def terrain(data, atlas, pixels_per_unit):
    cell = pixels_per_unit // 4
    if pixels_per_unit % 4 or atlas.size != (256, 256):
        raise ValueError("Unexpected PS2 atlas or map resolution")
    result = Image.new("RGBA", (12 * pixels_per_unit, 14 * pixels_per_unit))
    for row in range(56):
        source_row = (row - 8) % 40
        for col in range(48):
            source_col = col + 2
            vertex = source_row * 208 + source_col * 2
            u0, v0 = struct.unpack_from("<2f", data, 0x12000 + vertex * 64 + 48)
            u1, _ = struct.unpack_from("<2f", data, 0x12000 + (vertex + 1) * 64 + 48)
            _, v1 = struct.unpack_from("<2f", data, 0x12000 + (vertex + 104) * 64 + 48)
            left = max(0, min(255, round(min(u0, u1) * 256)))
            right = max(left + 1, min(256, round(max(u0, u1) * 256)))
            top = max(0, min(255, round((1 - max(v0, v1)) * 256)))
            bottom = max(top + 1, min(256, round((1 - min(v0, v1)) * 256)))
            tile = atlas.crop((left, top, right, bottom)).resize(
                (cell, cell), Image.Resampling.NEAREST)
            result.paste(tile, (col * cell, row * cell))
    return result


def preserve_mask(stage, size, pixels_per_unit):
    text = stage.read_text(encoding="utf-8-sig")
    walls = rectangles(text.split("  walls:\n", 1)[1].split("  enemies:\n", 1)[0])
    exit_area = rectangles(text.split("  exit:\n", 1)[1].split("  floorColor:", 1)[0])
    mask = Image.new("L", size)
    draw = ImageDraw.Draw(mask)
    for x, y, width, height in walls + exit_area:
        margin = .12 if (x, y, width, height) in walls else 0
        left = round((x + 6 - margin) * pixels_per_unit)
        top = round((7 - y - height - margin) * pixels_per_unit)
        right = round((x + 6 + width + margin) * pixels_per_unit)
        bottom = round((7 - y + margin) * pixels_per_unit)
        draw.rectangle((left, top, right, bottom), fill=255)
    return mask


def png_bytes(image):
    stream = BytesIO()
    image.save(stream, format="PNG")
    return stream.getvalue()


def update_map(path, native, mask):
    original = Image.open(path).convert("RGBA")
    if original.size != native.size:
        raise ValueError(f"Map dimensions differ: {path}")
    original.paste(native, (0, 0), Image.eval(mask, lambda value: 255 - value))
    output = png_bytes(original)
    if path.read_bytes() != output:
        path.write_bytes(output)
    return original


def update_tiles(number, image):
    folder = CAMPAIGN / f"PaintedStages/Tiles/Stage_2_{number}"
    pixels = image.width // 12
    if image.height != 14 * pixels or len(list(folder.glob("Original_*.png"))) != 168:
        raise ValueError(f"Editable tile set incomplete: {folder}")
    for row in range(14):
        for col in range(12):
            path = folder / f"Original_{col:02}_{row:02}.png"
            # Tilemap rows use bottom-origin coordinates; PNG rows use top-origin.
            crop = image.crop((col * pixels, (13 - row) * pixels,
                               (col + 1) * pixels, (14 - row) * pixels))
            output = png_bytes(crop)
            if path.read_bytes() != output:
                path.write_bytes(output)


def main():
    with ISO.open("rb") as source:
        archive = read_file(source, "DATA/DATA2.ROM")
    for number in STAGES:
        stage = CAMPAIGN / f"Stage_2_{number}.asset"
        base = MAPS / f"2-{number:02}.png"
        reference = base
        width, height = Image.open(reference).size
        pixels_per_unit = width // 12
        if (width, height) != (12 * pixels_per_unit, 14 * pixels_per_unit):
            raise ValueError(f"Unexpected map dimensions: {reference}")
        data = dict(entries(archive, f"fr2s{number:02}.map"))[f"fr2s{number:02}.map"]
        atlas = Image.open(CAMPAIGN / f"StageTextures/2-{number:02}/Floor.png").convert("RGBA")
        native = terrain(data, atlas, pixels_per_unit)
        mask = preserve_mask(stage, native.size, pixels_per_unit)
        update_map(base, native, mask)
        image = update_map(reference, native, mask) if reference != base else Image.open(base).convert("RGBA")
        update_tiles(number, image)
        print(f"2-{number}: PS2 floor restored in source map and 168 editable tiles")


if __name__ == "__main__":
    main()
