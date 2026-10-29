"""Rebuild the PS2 4-9 terrain from the user's Gain Ground ISO.

The 53x41 terrain vertices and 4-9 texture coordinates come from fr4s09.map.
The side and centre structures use the original PS2 model meshes and TIM2 art.
Output is a 13x10, 64-pixel-per-cell image for Unity's editable Tilemap.
"""
import hashlib
import json
import pathlib
import re
import struct
import numpy as np
from PIL import Image

import convert_ps2_characters as ps2
from extract_ps2_characters import decode_texture
from extract_ps2_hud import ISO, ROOT, read_file

output = ROOT / 'Assets/GainGround/Editable/OriginalCampaign/PS2Native'
width, height = 832, 640
image = None
depth = None
root = None


def read_stage_pack():
    with ISO.open('rb') as source:
        archive = read_file(source, 'DATA/DATA2.ROM')
    base = archive.index(b'fr4s09.map\0')
    files = {}
    for index in range(32):
        entry = archive[base + index * 32:base + (index + 1) * 32]
        name = entry[:16].split(b'\0')[0].decode('ascii')
        if not name:
            break
        offset, size = struct.unpack_from('>II', entry, 16)
        payload = archive[base + offset:base + offset + size]
        if len(payload) != size:
            raise ValueError('Truncated PS2 stage asset: ' + name)
        files[name] = payload
    if files['fr4s09.map'][:4] != b'00AG':
        raise ValueError('Unexpected PS2 terrain header')
    return files


def native_terrain(data, floor_atlas, shared_atlas):
    floor = np.asarray(floor_atlas.convert('RGBA'))
    shared = np.asarray(shared_atlas.convert('RGBA'))
    yy, xx = np.indices((height, width))
    result = shared[(yy // 2) % 128, (xx // 2) % 256].copy()
    for row in range(40):
        for col in range(52):
            vertex = row * 208 + col * 2
            u0, v0 = struct.unpack_from('<2f', data, 0x12000 + vertex * 64 + 48)
            u1, _ = struct.unpack_from('<2f', data, 0x12000 + (vertex + 1) * 64 + 48)
            _, v1 = struct.unpack_from('<2f', data, 0x12000 + (vertex + 104) * 64 + 48)
            left = max(0, min(255, round(min(u0, u1) * 256)))
            right = max(left + 1, min(256, round(max(u0, u1) * 256)))
            top = max(0, min(255, round((1 - max(v0, v1)) * 256)))
            bottom = max(top + 1, min(256, round((1 - min(v0, v1)) * 256)))
            tile = Image.fromarray(floor[top:bottom, left:right]).resize((16, 16), Image.Resampling.NEAREST)
            pixels = np.asarray(tile)
            # PS2's black texels denote uncovered ground below separate models.
            if np.max(pixels[:, :, :3]) > 4:
                result[row * 16:(row + 1) * 16, col * 16:(col + 1) * 16] = pixels
    return result


def native_metadata(data, files):
    heights = []
    for row in range(41):
        heights.append([struct.unpack_from('<f', data, 0x1000 + (row * 53 + col) * 32 + 4)[0]
                        for col in range(53)])
    enemies = {}
    for offset in range(0x90000, min(len(data) - 80, 0xa0000), 16):
        actor = struct.unpack_from('<I', data, offset + 16)[0]
        if actor not in range(26, 48):
            continue
        x, y, z, w = struct.unpack_from('<4f', data, offset + 32)
        if -208 <= x <= 208 and -48 <= z <= 272 and -100 <= y <= 100 and w == 1:
            enemies[actor] = [round(x / 32, 5), round((112 - z) / 32, 5)]
    if len(enemies) != 22 or sum(value == 48 for row in heights for value in row) != 148:
        raise ValueError('PS2 4-9 geometry does not match the verified ISO layout')
    return {
        'source': ISO.name,
        'files': {key: hashlib.sha256(value).hexdigest() for key, value in files.items()},
        'nativeSize': [416, 320], 'unitySize': [13, 10],
        'heightVertices': heights,
        'enemyPositions': [{'x': enemies[index][0], 'y': enemies[index][1]}
                           for index in range(26, 48)],
        'note': 'Side model placements follow the repeated structure visible in PS2 gameplay; terrain heights and UVs are decoded directly from the ISO.'
    }


def draw_model(name, texture, ox=0, oy=0, oz=0):
    global image, depth
    model = ps2.model(name)
    tex = np.asarray(Image.open(root / (texture + '.tm2.png')).convert('RGBA'))
    th, tw = tex.shape[:2]
    for part in model['parts']:
        verts = part['vertices']
        uvs = part['uv']
        tris = part['triangles']
        for i in range(0, len(tris), 3):
            ids = tris[i:i + 3]
            a, b, c = [verts[j] for j in ids]
            ax, ay = (a['x'] + ox + 208) * 2, (a['z'] + oz + 48) * 2
            bx, by = (b['x'] + ox + 208) * 2, (b['z'] + oz + 48) * 2
            cx, cy = (c['x'] + ox + 208) * 2, (c['z'] + oz + 48) * 2
            d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(d) < 1e-5:
                continue
            x0 = max(0, int(min(ax, bx, cx)))
            x1 = min(width, int(max(ax, bx, cx)) + 1)
            y0 = max(0, int(min(ay, by, cy)))
            y1 = min(height, int(max(ay, by, cy)) + 1)
            if x1 <= x0 or y1 <= y0:
                continue
            xx, yy = np.meshgrid(np.arange(x0, x1) + .5, np.arange(y0, y1) + .5)
            wa = ((by - cy) * (xx - cx) + (cx - bx) * (yy - cy)) / d
            wb = ((cy - ay) * (xx - cx) + (ax - cx) * (yy - cy)) / d
            wc = 1 - wa - wb
            z = wa * (a['y'] + oy) + wb * (b['y'] + oy) + wc * (c['y'] + oy)
            sub_depth = depth[y0:y1, x0:x1]
            mask = (wa >= -1e-4) & (wb >= -1e-4) & (wc >= -1e-4) & (z > sub_depth)
            if not mask.any():
                continue
            ua, ub, uc = [uvs[j]['x'] for j in ids]
            va, vb, vc = [1 - uvs[j]['y'] for j in ids]
            tx = np.clip(((wa * ua + wb * ub + wc * uc) % 1 * tw).astype(int), 0, tw - 1)
            ty = np.clip(((wa * va + wb * vb + wc * vc) % 1 * th).astype(int), 0, th - 1)
            color = tex[ty, tx]
            mask &= color[:, :, 3] > 0
            sub_depth[mask] = z[mask]
            image[y0:y1, x0:x1][mask] = color[mask]


def main():
    global root, image, depth
    files = read_stage_pack()
    root = ROOT / 'Analysis/PS2StageObjects/Stage_4_9_Source'
    root.mkdir(parents=True, exist_ok=True)
    for name, payload in files.items():
        if name.endswith('.bin'):
            (root / name).write_bytes(payload)
        elif name.endswith('.tm2'):
            decode_texture(payload).save(root / (name + '.png'))
    ps2.SOURCE = root
    image = native_terrain(files['fr4s09.map'],
                           Image.open(root / 'r4s09_c.tm2.png'),
                           Image.open(root / 'map7.tm2.png'))
    depth = np.full((height, width), -1e6, dtype=np.float32)
    for side in (-192, 192):
        for position in (0, 80, 160, 240):
            draw_model('r4s09_b1', 'r4s09_b', side, 0, position)
    draw_model('r4s09_02', 'r4s09_a', 0, 0, 120)
    draw_model('r4s09_01', 'r4s09_a', 0, 0, 144)
    draw_model('r4s09_03', 'r4s09_a', 0, 0, 120)
    draw_model('r4s09_ex', 'r4s09_a', 0, 48, 120)
    output.mkdir(parents=True, exist_ok=True)
    Image.fromarray(image).save(output / 'Stage_4_9.png')
    (output / 'Stage_4_9.json').write_text(json.dumps(native_metadata(files['fr4s09.map'], files),
                                                   indent=2), encoding='utf-8')
    print('Rebuilt PS2 stage 4-9 terrain, mesh art, and height metadata.')


if __name__ == '__main__':
    main()
