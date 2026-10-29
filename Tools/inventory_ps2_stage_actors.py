"""Read PS2 map actor records for stage-object placement research.

Actor type codes are not yet associated reliably with model files. This script
only inventories positions; it deliberately does not change StageDefinitions.
"""

from collections import Counter
from pathlib import Path
import json
import math
import struct

from extract_ps2_hud import ISO, read_file
from inventory_ps2_stage_models import entries


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Validation/PS2StageActorInventory.json"


def actors(data):
    if data[:4] != b"00AG":
        raise ValueError("Invalid stage map")
    result = {}
    conflicts = []
    for start in (0x94000, 0x99800):
        if len(data) < start + 0x5000:
            continue
        for offset in range(start, start + 0x5000, 0x50):
            identifier, kind = struct.unpack_from("<2I", data, offset + 16)
            x, y, z, w = struct.unpack_from("<4f", data, offset + 32)
            yaw = struct.unpack_from("<f", data, offset + 48)[0]
            if (identifier >= 1000 or kind >= 100 or w != 1 or
                    not all(math.isfinite(v) and abs(v) < 1000 for v in (x, y, z, yaw))):
                continue
            item = {"id": identifier, "type": kind, "position": [x, y, z],
                    "yawRadians": round(yaw, 6)}
            if identifier in result and result[identifier] != item:
                conflicts.append({"id": identifier, "earlier": result[identifier], "later": item})
                continue
            result[identifier] = item
    return [result[key] for key in sorted(result)], conflicts


def main():
    with ISO.open("rb") as source:
        archive = read_file(source, "DATA/DATA2.ROM")
    stages = {}
    for round_number in range(1, 6):
        for stage_number in range(1, 11):
            name = f"fr{round_number}s{stage_number:02}.map"
            data = dict(entries(archive, name))[name]
            found, conflicts = actors(data)
            stages[f"{round_number}-{stage_number}"] = {
                "count": len(found),
                "conflicts": conflicts,
                "types": dict(sorted(Counter(str(item["type"]) for item in found).items())),
                "actors": found,
            }
    OUT.write_text(json.dumps({"source": ISO.name, "stages": stages}, indent=2) + "\n",
                   encoding="utf-8")
    print("Inventoried", sum(item["count"] for item in stages.values()),
          "unique actor records in", len(stages), "stage maps")


if __name__ == "__main__":
    main()
