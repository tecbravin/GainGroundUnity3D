"""Extract projectile and effect sprites from weapon.rom/effect.rom in the PS2 ISO.

The crop table keeps original TIM2 pixels and alpha. Japanese prompt artwork in
weapon/shot04.tm2 is intentionally excluded because it is UI, not a shot effect.
"""
import hashlib
import json
import re
import struct
from pathlib import Path
from PIL import Image

from extract_ps2_characters import decode_texture
from extract_ps2_hud import ISO, ROOT, read_file

OUT = ROOT / "Assets/GainGround/Editable/PS2Effects/Sprites"

# name: (pack, TIM2 member, pixel rectangle or None for the whole image)
CROPS = {
    "Arrow": ("effect", "shot01.tm2", (112, 48, 128, 96)),
    "Spear": ("effect", "shot01.tm2", (96, 32, 112, 96)),
    "Boomerang": ("effect", "shot01.tm2", (96, 96, 128, 128)),
    "Tornado": ("weapon", "shot01.tm2", (0, 32, 48, 96)),
    "WhiteBolt": ("weapon", "shot01.tm2", (32, 0, 48, 32)),
    "RedBolt": ("weapon", "shot01.tm2", (96, 0, 112, 32)),
    "BlueBolt": ("weapon", "shot01.tm2", (112, 0, 128, 32)),
    "BlueBlade": ("weapon", "shot01.tm2", (64, 32, 96, 48)),
    "BlueBladeVertical": ("weapon", "shot01.tm2", (64, 48, 80, 96)),
    "Bomb": ("weapon", "shot01.tm2", (64, 96, 96, 128)),
    "EnergyChain": ("weapon", "shot01.tm2", (0, 96, 64, 128)),
    "Fireball_00": ("weapon", "shot02.tm2", (0, 0, 32, 32)),
    "Fireball_01": ("weapon", "shot02.tm2", (32, 0, 64, 32)),
    "Flame_00": ("weapon", "shot06.tm2", (0, 16, 32, 64)),
    "Flame_01": ("weapon", "shot06.tm2", (32, 16, 64, 64)),
    "FlameCap_00": ("weapon", "shot06.tm2", (0, 0, 32, 16)),
    "FlameCap_01": ("weapon", "shot06.tm2", (32, 0, 64, 16)),
    "PurpleOrb": ("weapon", "shot05.tm2", (0, 0, 16, 16)),
    "GreenOrb": ("weapon", "shot05.tm2", (16, 0, 32, 16)),
    "RedOrb": ("weapon", "shot05.tm2", (0, 16, 16, 32)),
    "BlueOrb": ("weapon", "shot05.tm2", (16, 16, 32, 32)),
    "StarShot": ("weapon", "hositama.tm2", None),
    "Smoke": ("weapon", "moku.tm2", None),
    "ImpactSpark": ("weapon", "shot07.tm2", (0, 0, 32, 32)),
    "ImpactRing": ("weapon", "shot07.tm2", (0, 32, 32, 64)),
    "WhiteBlast": ("effect", "shot01.tm2", (0, 32, 48, 80)),
    "FireRing": ("effect", "shot02.tm2", (96, 32, 128, 64)),
    "PurpleBeam": ("effect", "shot07.tm2", (48, 0, 64, 32)),
    "PurpleBurst": ("effect", "shot07.tm2", (0, 32, 32, 64)),
    "OrangeBurst": ("effect", "shot07.tm2", (32, 32, 64, 64)),
    "EnergyGlowDim": ("weapon", "en01.tm2", None),
    "EnergyGlowBright": ("weapon", "en02.tm2", None),
    "EnergyCrescent": ("weapon", "en03.tm2", None),
    "EnergyRing": ("weapon", "en04.tm2", None),
}
for frame in range(12):
    CROPS[f"Explosion_{frame:02}"] = ("weapon", "shot03.tm2",
                                    ((frame % 4) * 32, (frame // 4) * 32,
                                     (frame % 4 + 1) * 32, (frame // 4 + 1) * 32))
for frame in range(4):
    CROPS[f"Lightning_{frame:02}"] = ("weapon", "thunder.tm2",
                                    (frame * 32, 0, (frame + 1) * 32, 64))
    CROPS[f"WhiteRing_{frame:02}"] = ("weapon", "shot01.tm2",
                                     ((frame % 2) * 16, (frame // 2) * 16,
                                      (frame % 2 + 1) * 16, (frame // 2 + 1) * 16))
    CROPS[f"FlameBolt_{frame:02}"] = ("weapon", "shot02.tm2",
                                     (64 + frame * 16, 0, 80 + frame * 16, 32))
    CROPS[f"MagicStar_{frame:02}"] = ("weapon", "shot05.tm2",
                                     (32 + frame % 2 * 16, frame // 2 * 16,
                                      48 + frame % 2 * 16, 16 + frame // 2 * 16))
    CROPS[f"FireSpark_{frame:02}"] = ("effect", "shot07.tm2",
                                     (16 + frame % 2 * 16, frame // 2 * 16,
                                      32 + frame % 2 * 16, (frame // 2 + 1) * 16))
    CROPS[f"EffectFlame_{frame:02}"] = ("effect", "shot05.tm2",
                                      (frame % 2 * 32, 0 if frame < 2 else 16,
                                       (frame % 2 + 1) * 32, 16 if frame < 2 else 64))
    CROPS[f"EffectFlameAlt_{frame:02}"] = ("effect", "shot06.tm2",
                                         (frame % 2 * 32, 0 if frame < 2 else 16,
                                          (frame % 2 + 1) * 32, 16 if frame < 2 else 64))
for frame in range(2):
    CROPS[f"LightningBurst_{frame:02}"] = ("weapon", "thunder.tm2",
                                          (frame * 64, 64, (frame + 1) * 64, 128))
    CROPS[f"SwordBlade_{frame:02}"] = ("weapon", "shot02.tm2",
                                      (80 + frame * 32, 112, 112 + frame * 16, 128))
for frame, rect in enumerate(((0, 32, 48, 80), (48, 32, 96, 80),
                              (0, 80, 48, 128), (48, 80, 96, 128))):
    CROPS[f"Fireball_{frame + 2:02}"] = ("weapon", "shot02.tm2", rect)
for frame in range(8):
    CROPS[f"Ember_{frame:02}"] = ("weapon", "shot02.tm2",
                                 (96 + frame % 2 * 16, 32 + frame // 2 * 16,
                                  112 + frame % 2 * 16, 48 + frame // 2 * 16))
for frame, name in enumerate(("GreyOrb", "DimGreenOrb", "RedEmber", "OrangeEmber",
                              "MintOrb", "TealOrb", "OrangeOrb", "YellowOrb")):
    CROPS[name] = ("weapon", "shot05.tm2",
                   (frame % 4 * 16, 32 + frame // 4 * 16,
                    (frame % 4 + 1) * 16, 48 + frame // 4 * 16))
CROPS["ImpactCross"] = ("weapon", "shot07.tm2", (32, 0, 48, 16))
CROPS["ImpactPurple"] = ("weapon", "shot07.tm2", (48, 0, 64, 32))
CROPS["ImpactWhite"] = ("weapon", "shot07.tm2", (32, 16, 48, 32))
CROPS["ImpactSmoke"] = ("weapon", "shot07.tm2", (32, 32, 64, 64))
for frame, rect in enumerate(((0, 0, 48, 32), (48, 16, 96, 48))):
    CROPS[f"WhiteSwirl_{frame:02}"] = ("effect", "shot01.tm2", rect)
for frame, rect in enumerate(((48, 32, 96, 80), (0, 80, 48, 128), (48, 80, 96, 128))):
    CROPS[f"WhiteBlast_{frame + 1:02}"] = ("effect", "shot01.tm2", rect)
for frame in range(12):
    CROPS[f"EffectExplosion_{frame:02}"] = ("effect", "shot03.tm2",
                                          ((frame % 4) * 32, (frame // 4) * 32,
                                           (frame % 4 + 1) * 32, (frame // 4 + 1) * 32))
for member, variant in (("shot04.tm2", "DarkExplosion"),
                        ("shot04b.tm2", "DarkExplosionAlt")):
    for frame in range(4):
        CROPS[f"{variant}_{frame:02}"] = ("effect", member,
                                           ((frame % 2) * 32, (frame // 2) * 32,
                                            (frame % 2 + 1) * 32, (frame // 2 + 1) * 32))


def table(archive, marker):
    base = archive.index(marker + b"\0")
    while base >= 32 and re.fullmatch(rb"[\w.-]+\.(tm2|bin|anm|lst)",
                                      archive[base - 32:base - 16].split(b"\0")[0]):
        base -= 32
    result = {}
    for index in range(256):
        entry = archive[base + index * 32:base + (index + 1) * 32]
        name = entry[:16].split(b"\0")[0]
        if not name:
            break
        if not re.fullmatch(rb"[\w.-]+\.(tm2|bin|anm|lst)", name):
            raise ValueError("Invalid archive table member: " + repr(name))
        if not name.endswith(b".tm2"):
            continue
        offset, size = struct.unpack_from(">II", entry, 16)
        payload = archive[base + offset:base + offset + size]
        if len(payload) != size or payload[:4] != b"TIM2":
            raise ValueError("Invalid TIM2 member: " + repr(name))
        result[name.decode()] = (decode_texture(payload).convert("RGBA"),
                                  hashlib.sha256(payload).hexdigest())
    return result


def main():
    with ISO.open("rb") as source:
        archive = read_file(source, "DATA/DATA2.ROM")
    packs = {
        "weapon": table(archive, b"_tornado.bin"),
        "effect": table(archive, b"shot01.tm2"),
    }
    OUT.mkdir(parents=True, exist_ok=True)
    records = []
    for name, (pack, member, rect) in CROPS.items():
        source, digest = packs[pack][member]
        image = source.crop(rect) if rect else source
        # A few PS2 tiles use an opaque flat matte rather than TIM2 alpha. The
        # original game discards that matte through its blend mode; key it here
        # so Unity SpriteRenderer does not show a colored square around the art.
        if name in ("Bomb", "GreyOrb"):
            matte = image.getpixel((0, 0))[:3]
            pixels = []
            for red, green, blue, alpha in image.get_flattened_data():
                distance = max(abs(red - matte[0]), abs(green - matte[1]), abs(blue - matte[2]))
                pixels.append((red, green, blue, min(alpha, min(255, distance * 8))))
            cleaned = Image.new("RGBA", image.size)
            cleaned.putdata(pixels)
            image = cleaned
        elif name == "StarShot":
            cleaned = Image.new("RGBA", image.size)
            cleaned.putdata([(r, g, b, max(0, min(255, (a - 96) * 255 // 159)))
                             for r, g, b, a in image.get_flattened_data()])
            image = cleaned
        image.putalpha(image.getchannel("A").point(lambda alpha: 0 if alpha <= 2 else alpha))
        if image.getbbox() is None:
            raise ValueError("Empty sprite: " + name)
        image.save(OUT / (name + ".png"), optimize=True)
        records.append(dict(sprite=name + ".png", pack=pack + ".rom", member=member,
                            source_sha256=digest, crop=rect, size=list(image.size),
                            matte_removed=name in ("Bomb", "GreyOrb", "StarShot")))
    provenance = OUT.parent / "provenance.json"
    provenance.write_text(json.dumps(dict(source_iso=ISO.name,
        surveyed={pack + ".rom": sorted(members) for pack, members in packs.items()},
        skipped=["weapon.rom/shot04.tm2 (Japanese UI text)"],
        sprites=records), indent=2), encoding="utf-8")
    print("Extracted", len(records), "original projectile/effect sprites from",
          sum(len(members) for members in packs.values()), "TIM2 members")


if __name__ == "__main__":
    main()
