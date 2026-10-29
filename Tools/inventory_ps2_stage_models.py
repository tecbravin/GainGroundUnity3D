"""Inventory PS2 stage pack models and textures without changing Unity assets."""

from collections import Counter
from pathlib import Path
import json
import re
import struct
import tempfile

from extract_ps2_hud import ISO, read_file
import convert_ps2_characters as converter


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Validation/PS2StageModelInventory.json"


def entries(archive, marker):
    base = archive.index(marker.encode("ascii") + b"\0")
    while base >= 32 and re.fullmatch(rb"[\w.-]+\.(tm2|bin|map|anm|lst)",
                                       archive[base - 32:base - 16].split(b"\0")[0]):
        base -= 32
    result = []
    for index in range(256):
        entry = archive[base + index * 32:base + (index + 1) * 32]
        name = entry[:16].split(b"\0")[0]
        if not name:
            break
        if not re.fullmatch(rb"[\w.-]+\.(tm2|bin|map|anm|lst)", name):
            break
        offset, size = struct.unpack_from(">II", entry, 16)
        payload = archive[base + offset:base + offset + size]
        if len(payload) != size:
            raise ValueError("Truncated stage file " + name.decode("ascii"))
        result.append((name.decode("ascii"), payload))
    return result


def main():
    with ISO.open("rb") as source:
        archive = read_file(source, "DATA/DATA2.ROM")
    stages = {}
    totals = Counter()
    decode_errors = {}
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=OUT.parent) as scratch:
        converter.SOURCE = Path(scratch)
        for round_number in range(1, 6):
          for stage_number in range(1, 11):
            prefix = f"r{round_number}s{stage_number:02}"
            files = entries(archive, "f" + prefix + ".map")
            models = []
            for name, data in files:
                if not name.startswith(prefix) or not name.endswith(".bin"):
                    continue
                sections = re.findall(rb"(?:Shape|Hrchy|Micro)\x00", data[:256])
                if b"Shape\0" in sections and b"Hrchy\0" in sections:
                    models.append(name)
                    (converter.SOURCE / name).write_bytes(data)
                    try:
                        decoded = converter.model(name[:-4])
                        totals["decodedModels"] += 1
                        totals["meshParts"] += len(decoded["parts"])
                    except (AssertionError, KeyError, ValueError, struct.error) as error:
                        decode_errors[f"{round_number}-{stage_number}/{name}"] = str(error)
                totals["binaryFiles"] += 1
            textures = [name for name, _ in files if name.startswith(prefix) and name.endswith(".tm2")]
            stages[f"{round_number}-{stage_number}"] = {
                "modelFiles": models,
                "textureFiles": textures,
                "packFiles": len(files),
            }
            totals["models"] += len(models)
            totals["textures"] += len(textures)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps({"iso": ISO.name, "totals": totals, "decodeErrors": decode_errors,
                               "stages": stages}, indent=2) + "\n",
                   encoding="utf-8")
    print(dict(totals), "across", len(stages), "stages")


if __name__ == "__main__":
    main()
