"""Extract editable stage-object sprites from the local PS2 ISO texture packs.

Only the verified texture regions are exported. The PS2 stage geometry is not
decoded by this tool; placement remains in the Unity StageDefinition assets.
"""
from pathlib import Path
import sys
from collections import deque

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from extract_ps2_hud import ISO, read_file
from replace_complete_maps_with_ps2 import archive_textures

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/GainGround/Editable/OriginalCampaign/Objects/Sprites"


def portal_ring(source):
    # The atlas also contains a dark floor inside the ring. Keep only its
    # original metal pixels so the current stage floor remains visible.
    region = source.crop((0, 315, 200, 512)).convert("RGBA")
    pixels = region.load()
    for y in range(region.height):
        for x in range(region.width):
            radius = ((x - 98) ** 2 + (y - 98) ** 2) ** .5
            r, g, b, a = pixels[x, y]
            if radius > 97 or radius < 53:
                pixels[x, y] = (r, g, b, 0)
    return region


def laser_strip(source):
    # This red energy strip is present in r4s02_a.tm2. Preserve the source
    # colors and remove only the gray panel behind it.
    region = source.crop((320, 338, 462, 346)).convert("RGBA")
    pixels = region.load()
    for y in range(region.height):
        for x in range(region.width):
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (r, g, b, a if r > g * 1.35 and r > b * 1.25 else 0)
    return region


def clear_pale_border(source):
    """Remove empty atlas paper connected to the crop border, keeping the art."""
    image = source.convert("RGBA")
    pixels = image.load()
    seen = set()
    queue = deque()
    for x in range(image.width):
        queue.append((x, 0))
        queue.append((x, image.height - 1))
    for y in range(image.height):
        queue.append((0, y))
        queue.append((image.width - 1, y))
    while queue:
        x, y = queue.popleft()
        if not (0 <= x < image.width and 0 <= y < image.height) or (x, y) in seen:
            continue
        seen.add((x, y))
        r, g, b, a = pixels[x, y]
        if min(r, g, b) < 205 or max(r, g, b) - min(r, g, b) > 20:
            continue
        pixels[x, y] = (r, g, b, 0)
        queue.extend(((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))
    return image


def main():
    with ISO.open("rb") as source:
        archive = read_file(source, "DATA/DATA2.ROM")
    wood, _ = archive_textures(archive, 1, 6)
    future, _ = archive_textures(archive, 4, 2)
    ruins, _ = archive_textures(archive, 2, 3)
    prison, _ = archive_textures(archive, 2, 8)
    wood = wood["r1s06_a.tm2"]
    future = future["r4s02_a.tm2"]
    OUT.mkdir(parents=True, exist_ok=True)
    assets = {
        "WoodWallBoards.png": wood.crop((0, 148, 96, 512)),
        "WoodWallCap.png": wood.crop((0, 0, 512, 148)),
        "TeleportRing.png": portal_ring(future),
        "LaserStrip.png": laser_strip(future),
        "StoneArch.png": clear_pale_border(ruins["r2s03_a.tm2"].crop((21, 50, 231, 248))),
        "IronDoor.png": clear_pale_border(prison["r2s08_a.tm2"].crop((372, 0, 508, 252))),
    }
    for name, image in assets.items():
        image.save(OUT / name, optimize=True)
        print(name, image.size)


if __name__ == "__main__":
    main()
