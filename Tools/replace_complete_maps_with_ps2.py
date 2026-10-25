"""Recompose all Complete/Maps images from the local PS2 ISO textures.

The PS2 map geometry is not decoded here. Original PS2 texture atlases are
placed on the campaign's existing collision rectangles at 64 pixels/unit.
Existing PNG names and Unity .meta GUIDs are retained.
"""
import hashlib
import json
import re
import struct

from PIL import Image, ImageDraw

from build_ps2_stage_backgrounds import PPU, ROOT, rectangles, tiled
from extract_ps2_characters import decode_texture
from extract_ps2_hud import ISO, read_file

MAPS = ROOT / "Assets/GainGround/Editable/OriginalCampaign/Complete/Maps"
STAGES = ROOT / "Assets/GainGround/Editable/OriginalCampaign"
PROVENANCE = ROOT / "Analysis/PS2Stages/complete_maps_provenance.json"

# A TIM2 often holds several unrelated surfaces. These bounds identify the
# repeatable part used for a background or a collision wall; props remain in
# the editable stage objects. Coordinates refer to the unscaled source atlas.
FLOOR_REGIONS = {
    (2, 1): (0, 0, 128, 128),
    (2, 5): (144, 16, 240, 112),
    (2, 8): (128, 0, 256, 128),
    (3, 6): (0, 0, 128, 128),
    (3, 8): (0, 0, 128, 128),
    **{(4, stage): (0, 0, 64, 64) for stage in range(1, 5)},
    **{(4, stage): (32, 32, 64, 64) for stage in range(5, 10)},
    (4, 10): (0, 128, 128, 256),
}
WALL_REGIONS = {
    (2, 1): ("b", (16, 128, 128, 240)),
    (2, 5): ("b", (0, 0, 128, 128)),
    (2, 8): ("b", (16, 160, 112, 256)),
    (3, 6): ("b", (0, 64, 72, 224)),
    (3, 8): ("a", (256, 384, 384, 512)),
    (3, 10): ("b", (8, 58, 128, 176)),
    **{(4, stage): ("b", (0, 0, 96, 96)) for stage in (3, 4, 5, 9)},
    (4, 7): ("b", (0, 0, 96, 96)),
}


def surface_region(atlas, bounds, label):
    left, top, right, bottom = bounds
    if not (0 <= left < right <= atlas.width and 0 <= top < bottom <= atlas.height):
        raise ValueError(f"Invalid {label} atlas region {bounds} for {atlas.size}")
    return atlas.crop(bounds)


def archive_textures(archive, round_number, stage_number):
    prefix = f"r{round_number}s{stage_number:02}"
    marker = f"f{prefix}.map".encode() + b"\0"
    base = archive.index(marker)
    while re.fullmatch(rb"[\w.-]+\.(tm2|bin|map|anm|lst)",
                       archive[base - 32:base - 16].split(b"\0")[0]):
        base -= 32
    textures = {}
    hashes = {}
    for index in range(256):
        entry = archive[base + index * 32:base + (index + 1) * 32]
        name = entry[:16].split(b"\0")[0]
        if not name:
            break
        name = name.decode("ascii")
        if not name.endswith(".tm2") or not name.startswith(prefix):
            continue
        offset, size = struct.unpack_from(">II", entry, 16)
        payload = archive[base + offset:base + offset + size]
        if payload[:4] != b"TIM2" or len(payload) != size:
            raise ValueError(f"Invalid texture {prefix}/{name}")
        textures[name] = decode_texture(payload).convert("RGBA")
        hashes[name] = hashlib.sha256(payload).hexdigest()
    return textures, hashes


def stage_geometry(round_number, stage_number):
    text = (STAGES / f"Stage_{round_number}_{stage_number}.asset").read_text(encoding="utf-8")
    width, height = map(float, re.search(r"^  size: \{x: ([\d.]+), y: ([\d.]+)\}", text, re.M).groups())
    walls = rectangles(text.split("  walls:", 1)[1].split("  enemies:", 1)[0])
    exits = [] if "  noExit: 1" in text else rectangles(text.split("  exit:", 1)[1].split("  floorColor:", 1)[0])
    if "  additionalExits:" in text and "  noExit: 1" not in text:
        exits += rectangles(text.split("  additionalExits:", 1)[1].split("  additionalCaptives:", 1)[0])
    return width, height, walls, exits


def pixel_rect(rect, width, height):
    x, y, w, h = rect
    return (round((x + width / 2) * PPU), round((height / 2 - y - h) * PPU),
            round(w * PPU), round(h * PPU))


def compose(round_number, stage_number, textures, banner):
    prefix = f"r{round_number}s{stage_number:02}"
    width, height, walls, exits = stage_geometry(round_number, stage_number)
    size = (round(width * PPU), round(height * PPU))
    ground = textures[f"{prefix}_c.tm2"]
    if (round_number, stage_number) in FLOOR_REGIONS:
        ground = surface_region(ground, FLOOR_REGIONS[(round_number, stage_number)], "floor")
    elif round_number == 1 and stage_number == 2:
        ground = ground.crop((0, 0, 256, 128))
    elif round_number == 2:
        ground = ground.crop((0, 0, 256, 128))
    elif round_number == 4:
        ground = ground.crop((0, 0, 128, 128))
    image = tiled(ground, size)
    stone = textures.get(f"{prefix}_b.tm2")
    if stone is None:
        stone = textures[f"{prefix}_a.tm2"].crop((0, 48, 192, 216))
    if (round_number, stage_number) in WALL_REGIONS:
        source, bounds = WALL_REGIONS[(round_number, stage_number)]
        stone = surface_region(textures[f"{prefix}_{source}.tm2"], bounds, "wall")

    shadow = Image.new("RGBA", size)
    draw = ImageDraw.Draw(shadow)
    for wall in walls:
        x, y, w, h = pixel_rect(wall, width, height)
        draw.rectangle((x + 6, y + 6, x + w + 5, y + h + 7), fill=(0, 0, 0, 95))
    image = Image.alpha_composite(image, shadow)
    for wall in walls:
        x, y, w, h = pixel_rect(wall, width, height)
        if w <= 0 or h <= 0:
            continue
        image.alpha_composite(tiled(stone, (w, h)), (x, y))
        rim = ImageDraw.Draw(image)
        rim.line((x, y, x + w - 1, y), fill=(180, 174, 134, 255), width=2)
        rim.line((x + w - 1, y, x + w - 1, y + h - 1, x, y + h - 1), fill=(43, 39, 29, 255), width=3)

    for exit_rect in exits:
        x, y, w, h = pixel_rect(exit_rect, width, height)
        if w <= 0 or h <= 0:
            continue
        sign = Image.new("RGBA", (w, 40))
        middle = min(w, 128)
        left = (w - middle) // 2
        right = w - middle - left
        if left:
            sign.paste(banner.crop((0, 0, 192, 40)).resize((left, 40)), (0, 0))
        sign.paste(banner.crop((192, 0, 320, 40)).resize((middle, 40)), (left, 0))
        if right:
            sign.paste(banner.crop((320, 0, 512, 40)).resize((right, 40)), (left + middle, 0))
        image.alpha_composite(sign, (x, y + (h - sign.height) // 2))
    return image.convert("RGB"), size, walls, exits


def update_import_settings(path):
    meta = path.with_suffix(path.suffix + ".meta")
    text = meta.read_text(encoding="utf-8")
    with Image.open(path) as image:
        width, height = image.size
    if width * 14 != height * 12 or width % 12:
        raise ValueError(f"Unexpected stage image dimensions: {path} ({width}x{height})")
    pixels_per_unit = width // 12
    expected = f"  spritePixelsToUnits: {pixels_per_unit}\n"
    if expected in text:
        return
    current = re.search(r"^  spritePixelsToUnits: \d+$", text, re.M)
    if current is None:
        raise ValueError(f"Unexpected importer settings: {meta}")
    text = text[:current.start()] + expected.rstrip("\n") + text[current.end():]
    text = text.replace("    filterMode: 0\n", "    filterMode: 1\n", 1)
    meta.write_text(text, encoding="utf-8", newline="")


def main():
    with ISO.open("rb") as stream:
        archive = read_file(stream, "DATA/DATA2.ROM")
    first_textures, _ = archive_textures(archive, 1, 1)
    banner = first_textures["r1s01_a.tm2"].crop((0, 0, 512, 40))
    manifest = []
    for round_number in range(1, 6):
        for stage_number in range(1, 11):
            target = MAPS / f"{round_number}-{stage_number:02}.png"
            if not target.is_file() or not target.with_suffix(".png.meta").is_file():
                raise FileNotFoundError(target)
            textures, hashes = archive_textures(archive, round_number, stage_number)
            image, pixels, walls, exits = compose(round_number, stage_number, textures, banner)
            image.save(target, optimize=True)
            update_import_settings(target)
            manifest.append(dict(stage=f"{round_number}-{stage_number:02}", pixels=pixels,
                                 source_pack=f"fr{round_number}s{stage_number:02}.rom",
                                 texture_sha256=hashes, walls=walls, exits=exits,
                                 output_sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
    PROVENANCE.write_text(json.dumps(dict(source=ISO.name, ppu=PPU,
        method="Original PS2 textures recomposed over existing campaign collision geometry",
        stages=manifest), indent=2), encoding="utf-8")
    print(f"Updated {len(manifest)} maps in {MAPS}")


if __name__ == "__main__":
    main()
