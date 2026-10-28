"""Repair PS2 effect prefab sprite references without rewriting unrelated assets."""
import re
from pathlib import Path

from materialize_ps2_stage_objects import sprite_meta

ROOT = Path(__file__).resolve().parents[1]
FX = ROOT / "Assets/GainGround/Editable/PS2Effects"
SPRITES = FX / "Sprites"
PREFABS = FX / "Prefabs"


def repair():
    corrected_importers = 0
    corrected_prefabs = []
    for image in sorted(SPRITES.glob("*.png")):
        meta = Path(str(image) + ".meta")
        previous = meta.read_text(encoding="utf-8")
        guid = sprite_meta(image)
        if meta.read_text(encoding="utf-8") != previous:
            corrected_importers += 1

        prefab = PREFABS / (image.stem + ".prefab")
        if not prefab.is_file():
            raise FileNotFoundError(prefab)
        text = prefab.read_text(encoding="utf-8")
        desired = f"  m_Sprite: {{fileID: 21300000, guid: {guid}, type: 3}}"
        text, count = re.subn(r"(?m)^  m_Sprite: .*?$", desired, text, count=1)
        if count != 1:
            raise ValueError(f"Missing SpriteRenderer sprite field: {prefab}")
        old = prefab.read_text(encoding="utf-8")
        if text != old:
            text = text.replace("  m_WasSpriteAssigned: 0", "  m_WasSpriteAssigned: 1", 1)
            prefab.write_text(text, encoding="utf-8", newline="")
            corrected_prefabs.append(prefab.name)
    print(f"Sprite importers corrected: {corrected_importers}; prefabs corrected: {corrected_prefabs}")


if __name__ == "__main__":
    repair()
