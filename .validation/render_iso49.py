import pathlib
import sys
import numpy as np
from PIL import Image

sys.path.insert(0, 'Tools')
import convert_ps2_characters as ps2

root = pathlib.Path('.validation/iso49')
ps2.SOURCE = root
width, height = 832, 640
image = np.asarray(Image.open('.validation/ps2_4_9_composite.png').convert('RGBA')).copy()
depth = np.full((height, width), -1e6, dtype=np.float32)


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


for side in (-192, 192):
    for position in (0, 80, 160, 240):
        draw_model('r4s09_b1', 'r4s09_b', side, 0, position)
draw_model('r4s09_02', 'r4s09_a', 0, 0, 120)
draw_model('r4s09_01', 'r4s09_a', 0, 0, 144)
draw_model('r4s09_03', 'r4s09_a', 0, 0, 120)
draw_model('r4s09_ex', 'r4s09_a', 0, 48, 120)
Image.fromarray(image).save('.validation/ps2_4_9_mesh_test.png')
