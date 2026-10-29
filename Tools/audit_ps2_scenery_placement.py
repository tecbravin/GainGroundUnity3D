"""Count imported ISO scenery and actual stage object references.

This is an inventory, not an automatic placement pass. Static model transforms
are not reliably decoded from the PS2 map yet.
"""

from collections import defaultdict
from pathlib import Path
import json
import re


ROOT = Path(__file__).resolve().parents[1]
CAMPAIGN = ROOT / "Assets/GainGround/Editable/OriginalCampaign"
CATALOG = ROOT / "Analysis/PS2StageModels/import.json"
OUTPUT = ROOT / "Validation/PS2SceneryPlacementReview.md"


def main():
    entries = json.loads(CATALOG.read_text(encoding="utf-8"))["entries"]
    by_stage = defaultdict(list)
    model_guids = set()
    for entry in entries:
        by_stage[entry["stage"]].append(entry["name"])
        prefab = (CAMPAIGN / "Objects/Models3D" /
                  ("Stage_" + entry["stage"].replace("-", "_")) /
                  entry["name"] / (entry["name"] + ".prefab.meta"))
        match = re.search(r"^guid: ([a-f0-9]+)$", prefab.read_text(), re.M)
        if not match:
            raise ValueError(f"Prefab GUID missing: {prefab}")
        model_guids.add(match.group(1))

    lines = [
        "# Revisão de cenário da ISO por estágio", "",
        "Os modelos da ISO estão disponíveis nas paletas editáveis. A coluna "
        "**modelos colocados** conta apenas objetos do estágio que referenciam "
        "essas prefabs; ela inclui os oito objetos `PAINT:` da 1-1 que usam "
        "modelos nativos. A posição e orientação de outros modelos ainda exigem "
        "conferência visual antes da implantação.", "",
        "| Fase | Modelos ISO | Modelos colocados | Objetos do estágio | Situação |",
        "|---|---:|---:|---:|---|",
    ]
    total_placed = 0
    for round_number in range(1, 6):
        for number in range(1, 11):
            stage = f"{round_number}-{number:02}"
            text = (CAMPAIGN / f"Stage_{round_number}_{number}.asset").read_text(
                encoding="utf-8-sig")
            block = (text.split("  objects:", 1)[1].split("  hazards:", 1)[0]
                     if "  objects:" in text else "")
            objects = re.findall(r"^  - id: (.+)$", block, re.M)
            placed = sum(guid in model_guids for guid in
                         re.findall(r"    prefab: \{fileID: \d+, guid: ([a-f0-9]+)", block))
            total_placed += placed
            if round_number == 1 and number in (1, 2, 3):
                status = "Validado em Play Mode"
            elif round_number == 1 and number == 6:
                status = "Peças editáveis da paliçada existentes"
            else:
                status = "Posições nativas pendentes"
            lines.append(f"| {round_number}-{number} | {len(by_stage[stage])} | "
                         f"{placed} | {len(objects)} | {status} |")
    OUTPUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{len(entries)} prefabs da ISO; {total_placed} referências em estágios; "
          f"50 estágios auditados -> {OUTPUT}")


if __name__ == "__main__":
    main()
