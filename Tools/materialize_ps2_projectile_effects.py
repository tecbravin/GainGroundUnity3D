"""Create editable Unity prefabs for extracted PS2 projectile/effect pixels.

Also installs prefab references into the weapons and enemy actor prefabs. Existing
sprite, sound and model settings are kept; the new prefab references take visual
priority in the runtime code.
"""
import re
from pathlib import Path

from materialize_ps2_stage_objects import folder, guid_for, prefab_yaml, sprite_meta
from repair_ps2_effect_sprites import repair as repair_effect_sprites

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets/GainGround/Editable"
FX = ASSETS / "PS2Effects"
SPRITES = FX / "Sprites"
PREFABS = FX / "Prefabs"
ENEMIES = ASSETS / "Characters/Models3D/EnemyActors"
CHARACTERS = ASSETS / "Characters"

# Projectile, impact, optional muzzle. Names refer to original TIM2 crops.
ENEMY_EFFECTS = {
    "archer": ("Arrow", "ImpactSpark", None),
    "rifleman": ("WhiteBolt", "ImpactSpark", "ImpactSpark"),
    "soldier": ("WhiteBolt", "ImpactSpark", "ImpactSpark"),
    "gunner": ("WhiteBolt", "ImpactSpark", "ImpactSpark"),
    "streetgun": ("WhiteBolt", "ImpactSpark", "ImpactSpark"),
    "robot": ("BlueBolt", "ImpactSpark", "BlueOrb"),
    "turret": ("RedBolt", "ImpactSpark", "RedOrb"),
    "tank": ("RedBolt", "Explosion_00", "ImpactSpark"),
    "computer": ("BlueBolt", "Lightning_00", "BlueOrb"),
    "core": ("BlueBolt", "WhiteBlast", "BlueOrb"),
    "bomber": ("Bomb", "Explosion_00", "Smoke"),
    "grenadier": ("Bomb", "Explosion_00", "Smoke"),
    "wizard": ("PurpleOrb", "WhiteBlast", None),
    "worm": ("GreenOrb", "WhiteBlast", None),
    "orb": ("BlueOrb", "WhiteBlast", None),
    "spider": ("GreenOrb", "WhiteBlast", None),
    "demon": ("Fireball_00", "FireRing", None),
    "dragon": ("Fireball_01", "Explosion_00", None),
    "giant": ("RedOrb", "Explosion_00", None),
    "sorcererBlue": ("BlueOrb", "WhiteBlast", None),
    "sorcererRed": ("RedOrb", "FireRing", None),
}

WEAPONS = {
    0: ("WhiteBolt", "ImpactSpark"),   # Bullet
    1: ("Spear", "ImpactSpark"),       # Spear
    2: ("Arrow", "ImpactSpark"),       # Arrow
    3: ("PurpleOrb", "WhiteBlast"),    # Magic
    4: ("Bomb", "Explosion_00"),      # Grenade
    5: ("Boomerang", None),            # Boomerang
    6: ("Fireball_00", "FireRing"),    # Flame
    7: ("RedBolt", "Explosion_00"),    # Rocket
    8: ("Tornado", "ImpactRing"),      # Tornado
    9: ("Flame_01", "FireRing"),       # FireTrap
    11: ("StarShot", "WhiteBlast"),    # Orbit
}

PROJECTILE_SEQUENCES = {
    "Fireball_00": ("Fireball_00", "Fireball_01"),
    "Fireball_01": ("Fireball_01", "Fireball_00"),
    "Flame_01": ("Flame_00", "Flame_01"),
}
IMPACT_SEQUENCES = {
    "Explosion_00": tuple(f"Explosion_{i:02}" for i in range(12)),
    "WhiteBlast": ("WhiteBlast", "WhiteBlast_01", "WhiteBlast_02", "WhiteBlast_03"),
    "Lightning_00": tuple(f"Lightning_{i:02}" for i in range(4)),
}


def prefab_ref(name, guids):
    return ("{fileID: 100000, guid: " + guids[name] + ", type: 3}") if name else "{fileID: 0}"


def sprite_lines(names, guids, indent):
    if not names:
        return " []\n"
    return "\n" + "".join(f"{indent}- {{fileID: 21300000, guid: {guids[name]}, type: 3}}\n"
                        for name in names)


def upsert_field(block, key, value, after):
    line = f"  {key}: {value}\n"
    if re.search(rf"^  {key}:.*$", block, flags=re.M):
        return re.sub(rf"^  {key}:.*\n", line, block, count=1, flags=re.M)
    if after not in block:
        raise ValueError("Missing serialized anchor: " + after)
    return block.replace(after, after + line, 1)


def update_enemy(path, visual, impact, muzzle, guids, sprite_guids):
    text = path.read_text(encoding="utf-8")
    # All actor prefabs contain exactly one PS2EnemyVisual component.
    if text.count("  projectileSprite:") != 1:
        raise ValueError("Unexpected actor prefab: " + str(path))
    text = upsert_field(text, "projectileVisualPrefab", prefab_ref(visual, guids),
                        re.search(r"^  projectileSprite:.*\n", text, re.M).group())
    text = upsert_field(text, "muzzleVisualPrefab", prefab_ref(muzzle, guids),
                        re.search(r"^  muzzleFlashSprite:.*\n", text, re.M).group())
    text = upsert_field(text, "impactVisualPrefab", prefab_ref(impact, guids),
                        re.search(r"^  impactSprite:.*\n", text, re.M).group())
    sequence = PROJECTILE_SEQUENCES.get(visual)
    if sequence:
        text = re.sub(r"^  projectileFrames:.*\n(?:  - .*\n)*",
                      "  projectileFrames:" + sprite_lines(sequence, sprite_guids, "  "),
                      text, count=1, flags=re.M)
    text = re.sub(r"^  impactFrames:.*\n(?:  - .*\n)*", "", text, flags=re.M)
    text = re.sub(r"^  impactFramesPerSecond:.*\n", "", text, flags=re.M)
    impact_line = re.search(r"^  impactVisualPrefab:.*\n", text, re.M).group()
    text = text.replace(impact_line, impact_line + "  impactFrames:" +
                        sprite_lines(IMPACT_SEQUENCES.get(impact), sprite_guids, "  ") +
                        "  impactFramesPerSecond: 45\n", 1)
    if path.stem == "archer" and "projectileSize: {x: 1, y: 1}" in text:
        text = text.replace("projectileSize: {x: 1, y: 1}",
                            "projectileSize: {x: 0.16, y: 0.48}", 1)
        text = text.replace("projectileTint: {r: 0.109024554, g: 0.22947708, b: 0.6603774, a: 1}",
                            "projectileTint: {r: 1, g: 1, b: 1, a: 1}", 1)
    path.write_text(text, encoding="utf-8", newline="\n")


def update_weapon_block(block, guids, sprite_guids):
    kind = re.search(r"^    kind: (\d+)$", block, re.M)
    mode = re.search(r"^    mode: (\d+)$", block, re.M)
    sheet = re.search(r"^    effectSpriteSheet: (.+)$", block, re.M)
    if kind is None or sheet is None or (mode and int(mode.group(1)) != 0) or "fileID: 0" not in sheet.group(1):
        return block
    entry = WEAPONS.get(int(kind.group(1)))
    if entry is None:
        return block
    visual, impact = entry
    block = re.sub(r"^    (projectileVisualPrefab|impactVisualPrefab|impactFramesPerSecond):.*\n", "", block, flags=re.M)
    block = re.sub(r"^    (projectileFrames|impactFrames):.*\n(?:    - .*\n)*", "", block, flags=re.M)
    line = sheet.group() + "\n"
    replacement = (line + "    projectileVisualPrefab: " + prefab_ref(visual, guids) + "\n" +
                   "    projectileFrames:" + sprite_lines(PROJECTILE_SEQUENCES.get(visual), sprite_guids, "    ") +
                   "    impactVisualPrefab: " + prefab_ref(impact, guids) + "\n" +
                   "    impactFrames:" + sprite_lines(IMPACT_SEQUENCES.get(impact), sprite_guids, "    ") +
                   "    impactFramesPerSecond: 45\n")
    return block.replace(line, replacement, 1)


def update_character(path, guids, sprite_guids):
    text = path.read_text(encoding="utf-8")
    for field, next_field in (("normalWeapon", "specialWeapon"),
                              ("specialWeapon", "characterTint")):
        pattern = rf"(?ms)(^  {field}:\n)(.*?)(?=^  {next_field}:)"
        text, count = re.subn(pattern,
                              lambda m: m.group(1) + update_weapon_block(m.group(2), guids, sprite_guids), text)
        if count != 1:
            raise ValueError("Unexpected weapon serialization: " + str(path))
    path.write_text(text, encoding="utf-8", newline="\n")


def main():
    folder(FX)
    folder(SPRITES)
    folder(PREFABS)
    guid_for(FX / "provenance.json", "DefaultImporter:\n  externalObjects: {}\n"
             "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    guids = {}
    sprite_guids = {}
    for sprite_path in sorted(SPRITES.glob("*.png")):
        name = sprite_path.stem
        sprite_guid = sprite_meta(sprite_path)
        sprite_guids[name] = sprite_guid
        prefab_path = PREFABS / (name + ".prefab")
        guids[name] = guid_for(prefab_path, "PrefabImporter:\n  externalObjects: {}\n"
                               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
        if not prefab_path.exists():
            prefab_path.write_text(prefab_yaml(name, sprite_guid, False, 6), encoding="utf-8")
    if len(guids) != 127:
        raise ValueError("Expected all 127 extracted sprites")
    for enemy, (visual, impact, muzzle) in ENEMY_EFFECTS.items():
        update_enemy(ENEMIES / (enemy + ".prefab"), visual, impact, muzzle, guids, sprite_guids)
    for character in CHARACTERS.glob("*.asset"):
        update_character(character, guids, sprite_guids)
    repair_effect_sprites()
    print("Installed", len(guids), "effect prefabs on", len(ENEMY_EFFECTS),
          "enemy actors and", len(list(CHARACTERS.glob('*.asset'))), "character assets")


if __name__ == "__main__":
    main()
