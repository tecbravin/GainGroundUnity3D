"""Rebuild one stage PNG and its existing painted-layout tiles.

Placed constructions are independent prefabs; edit their SpriteRenderer sprite
in Unity. Existing tile GUIDs are kept so stage prefabs retain their sprites.
"""
import argparse
import hashlib
from pathlib import Path

from PIL import Image

from replace_complete_maps_with_ps2 import MAPS, ROOT, compose, update_import_settings

TILES = ROOT / "Assets/GainGround/Editable/OriginalCampaign/PaintedStages/Tiles"


def source(path):
    path = Path(path)
    if not path.is_absolute():
        path = ROOT / path
    if not path.is_file():
        raise FileNotFoundError(path)
    return Image.open(path).convert("RGBA")


def sync_tiles(round_number, stage_number, image):
    folder = TILES / f"Stage_{round_number}_{stage_number}"
    if not folder.is_dir():
        raise FileNotFoundError(folder)
    rgba = image.convert("RGBA")
    if rgba.width % 12 or rgba.height % 14 or rgba.width // 12 != rgba.height // 14:
        raise ValueError(f"Stage map is not a 12 x 14 square-tile grid: {rgba.size}")
    pixels = rgba.width // 12
    changed = 0
    for row in range(14):
        for col in range(12):
            path = folder / f"Original_{col:02}_{row:02}.png"
            if not path.is_file() or not path.with_suffix(".png.meta").is_file():
                raise FileNotFoundError(path)
            box = (col * pixels, rgba.height - (row + 1) * pixels,
                   (col + 1) * pixels, rgba.height - row * pixels)
            tile = rgba.crop(box)
            with Image.open(path) as previous:
                if previous.convert("RGBA").tobytes() == tile.tobytes():
                    continue
            tile.save(path, optimize=True)
            changed += 1
    return changed


def rebuild(round_number, stage_number, floor, elevations, constructions, exit_banner,
            output=None):
    prefix = f"r{round_number}s{stage_number:02}"
    textures = {
        f"{prefix}_c.tm2": source(floor),
        f"{prefix}_b.tm2": source(elevations),
        f"{prefix}_a.tm2": source(constructions),
    }
    banner = source(exit_banner)
    if banner.size == (1024, 80):
        banner = banner.resize((512, 40), Image.Resampling.LANCZOS)
    elif banner.size != (512, 40):
        raise ValueError("The EXIT texture must be 512 x 40 or 1024 x 80 pixels")
    image, _, _, _ = compose(round_number, stage_number, textures, banner)
    target = Path(output) if output else MAPS / f"{round_number}-{stage_number:02}.png"
    if not target.is_absolute():
        target = ROOT / target
    if not target.is_file():
        raise FileNotFoundError(target)
    with Image.open(target) as previous:
        target_size = previous.size
    if image.size != target_size:
        image = image.resize(target_size, Image.Resampling.LANCZOS)
    image.quantize(colors=256, method=Image.Quantize.MEDIANCUT).save(target, optimize=True)
    if output is None:
        update_import_settings(target)
        with Image.open(target) as rebuilt:
            changed = sync_tiles(round_number, stage_number, rebuilt)
        print(f"Updated {changed} painted-layout tiles without changing their GUIDs")
    print(f"Rebuilt {target} ({target_size[0]} x {target_size[1]}) "
          f"SHA-256 {hashlib.sha256(target.read_bytes()).hexdigest()}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--round", type=int, required=True)
    parser.add_argument("--stage", type=int, required=True)
    parser.add_argument("--floor", required=True)
    parser.add_argument("--elevations", required=True)
    parser.add_argument("--constructions", required=True)
    parser.add_argument("--exit", dest="exit_banner", required=True)
    parser.add_argument("--output", help="Diagnostic output PNG; leaves the stage map untouched")
    args = parser.parse_args()
    rebuild(args.round, args.stage, args.floor, args.elevations,
            args.constructions, args.exit_banner, args.output)


if __name__ == "__main__":
    main()
