"""Decode every visible stage model from the local ISO for the Unity importer.

The catalog keeps the source SHA-256 and uses the editable stage texture atlases.
It does not guess object placement from the still undecoded PS2 instance types.
"""

from hashlib import sha256
from pathlib import Path
import json
import tempfile

import convert_ps2_characters as converter
from extract_ps2_hud import ISO, read_file
from inventory_ps2_stage_models import entries


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Analysis/PS2StageModels"
TEXTURES = ROOT / "Assets/GainGround/Editable/OriginalCampaign/StageTextures"


def texture_for(stage, name):
    role = "Elevations" if name.split("_")[-1].startswith("b") else "Constructions"
    path = TEXTURES / stage / (role + ".png")
    if not path.is_file():
        raise FileNotFoundError(path)
    return path.relative_to(ROOT).as_posix()


def main():
    with ISO.open("rb") as source:
        archive = read_file(source, "DATA/DATA2.ROM")
    OUT.mkdir(parents=True, exist_ok=True)
    catalog = []
    failures = []
    with tempfile.TemporaryDirectory(dir=OUT) as scratch:
        converter.SOURCE = Path(scratch)
        for round_number in range(1, 6):
            for stage_number in range(1, 11):
                prefix = f"r{round_number}s{stage_number:02}"
                stage = f"{round_number}-{stage_number:02}"
                for filename, data in entries(archive, "f" + prefix + ".map"):
                    if not filename.startswith(prefix) or not filename.endswith(".bin"):
                        continue
                    name = filename[:-4]
                    (converter.SOURCE / filename).write_bytes(data)
                    try:
                        model = converter.model(name)
                    except (AssertionError, KeyError, ValueError) as error:
                        failures.append({"stage": stage, "name": name, "reason": str(error),
                                         "sha256": sha256(data).hexdigest()})
                        continue
                    target = OUT / (name + ".json")
                    target.write_text(json.dumps(model, separators=(",", ":")), encoding="utf-8")
                    catalog.append({"stage": stage, "name": name,
                                    "json": target.relative_to(ROOT).as_posix(),
                                    "texture": texture_for(stage, name),
                                    "sha256": sha256(data).hexdigest()})
    (OUT / "import.json").write_text(json.dumps({"source": ISO.name, "entries": catalog,
                                                 "nonRenderable": failures}, indent=2) + "\n",
                                     encoding="utf-8")
    print(f"Decoded {len(catalog)} stage models; {len(failures)} non-renderable: "
          + ", ".join(item["name"] for item in failures))


if __name__ == "__main__":
    main()
