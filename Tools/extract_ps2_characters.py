"""Recover named character models, motions and textures from the local PS2 ISO.

These are source assets, not Unity-ready sprites. Does not modify game assets.
"""
import hashlib
import json
import struct
from pathlib import Path
from PIL import Image
from extract_ps2_hud import ISO, ROOT, read_file, decode

OUT = ROOT / "Analysis/PS2Characters"


def decode_texture(payload):
    if payload[35] != 4:
        return decode(payload, 0)
    _, palette_size, image_size, header_size, colors = struct.unpack_from("<IIIHH", payload, 16)
    width, height = struct.unpack_from("<HH", payload, 36)
    if colors != 16 or palette_size != 64 or payload[34] & 7 != 3:
        raise ValueError("Unsupported four-bit palette")
    start = 16 + header_size
    raw = payload[start:start + image_size]
    palette = payload[start + image_size:start + image_size + palette_size]
    entries = [(palette[i], palette[i + 1], palette[i + 2], min(255, palette[i + 3] * 2))
               for i in range(0, palette_size, 4)]
    pixels = [entries[(raw[i // 2] >> ((i % 2) * 4)) & 15] for i in range(width * height)]
    texture = Image.new("RGBA", (width, height))
    texture.putdata(pixels)
    return texture


def main():
    with ISO.open("rb") as source:
        archive = read_file(source, "DATA/DATA2.ROM")
    # The inner table's offsets are relative to its first entry, not the outer
    # archive's nominal player.rom offset. Validate against its named TIM2 file.
    base = archive.index(b"athra.anm" + b"\0" * 7)
    texture_offset = struct.unpack_from(">I", archive, base + 2 * 32 + 16)[0]
    if archive[base + texture_offset:base + texture_offset + 4] != b"TIM2":
        raise ValueError("Character table validation failed")
    OUT.mkdir(parents=True, exist_ok=True)
    records = []
    for i in range(256):
        entry = archive[base + i * 32:base + (i + 1) * 32]
        name = entry[:16].split(b"\0")[0].decode("ascii")
        if not name:
            break
        if Path(name).name != name or not name.endswith((".anm", ".bin", ".tm2", ".lst")):
            raise ValueError("Unexpected table entry: " + name)
        offset, size = struct.unpack_from(">II", entry, 16)
        payload = archive[base + offset:base + offset + size]
        if len(payload) != size:
            raise ValueError("Truncated asset: " + name)
        (OUT / name).write_bytes(payload)
        record = dict(file=name, offset=base + offset, size=size,
                      sha256=hashlib.sha256(payload).hexdigest())
        if name.endswith(".tm2"):
            try:
                texture = decode_texture(payload)
                texture.save(OUT / (name + ".png"))
                record["texture_size"] = list(texture.size)
            except (ValueError, IndexError, struct.error) as error:
                record["decode_error"] = str(error)
        elif name.endswith((".anm", ".bin")) and len(payload) >= 16:
            count = struct.unpack_from("<I", payload, 12)[0]
            if count < 64:
                record["sections"] = [payload[16 + j * 16:28 + j * 16].split(b"\0")[0].decode("ascii", errors="replace")
                                      for j in range(max(0, count - 1))]
        records.append(record)
    (OUT / "manifest.json").write_text(json.dumps(dict(source=ISO.name,
        archive_sha256=hashlib.sha256(archive).hexdigest(), table_offset=base,
        status="Source extraction only; model and motion decoding still required", assets=records), indent=2), encoding="utf-8")
    print("Recovered", len(records), "named character source files.")
    print("Models:", sum(r["file"].endswith(".bin") for r in records),
          "motions:", sum(r["file"].endswith(".anm") for r in records),
          "textures:", sum("texture_size" in r for r in records))


if __name__ == "__main__":
    main()
