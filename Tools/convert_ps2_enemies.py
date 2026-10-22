"""Extract enemy archives and convert original geometry without inventing missing data."""
import hashlib
import json
import re
import struct
from pathlib import Path
from extract_ps2_hud import ISO, ROOT, read_file
from extract_ps2_characters import decode_texture
import convert_ps2_characters as converter

OUT = ROOT / 'Analysis/PS2Enemies'

def main():
    with ISO.open('rb') as stream:
        archive = read_file(stream, 'DATA/DATA2.ROM')
    report = []
    # Search validated inner tables, rather than relying on outer ROM offsets.
    for match in re.finditer(rb'enemy\d\d[a-z]?\.lst\x00', archive):
        base = match.start()
        while base >= 32 and re.fullmatch(rb'[A-Za-z0-9_.-]+\.(lst|anm|bin|tm2)', archive[base-32:base-16].split(b'\0')[0]):
            base -= 32
        group = match.group()[:-1].decode().removesuffix('.lst')
        folder = OUT / group
        folder.mkdir(parents=True, exist_ok=True)
        names = []
        for i in range(512):
            entry = archive[base+i*32:base+(i+1)*32]
            raw = entry[:16].split(b'\0')[0]
            if not raw: break
            name = raw.decode('ascii')
            if not re.fullmatch(r'[\w.-]+\.(lst|anm|bin|tm2)', name):
                raise ValueError((group, name))
            offset, size = struct.unpack_from('>II', entry, 16)
            payload = archive[base+offset:base+offset+size]
            assert len(payload) == size and offset >= 32
            (folder/name).write_bytes(payload)
            names.append(name)
            if name.endswith('.tm2'):
                decode_texture(payload).save(folder/(name+'.png'))
        converter.SOURCE = folder
        for name in names:
            if not name.endswith('.bin'): continue
            stem = Path(name).stem
            record = dict(group=group, model=stem, sha256=hashlib.sha256((folder/name).read_bytes()).hexdigest())
            try:
                motion = stem
                if not (folder/(motion+'.anm')).exists():
                    candidates = [x[:-4] for x in names if x.endswith('.anm') and stem.startswith(x[:-4])]
                    motion = max(candidates, key=len) if candidates else stem
                data = converter.model(stem, motion)
                binary = (folder/name).read_bytes()
                textures = sorted(set(x.decode() for x in re.findall(rb'[A-Za-z0-9_]+\.tm2', binary)))
                data.update(source=stem, character=group+'_'+stem)
                (folder/(stem+'.json')).write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
                record.update(status='converted', meshes=len(data['parts']), bones=len(data['bones']), motions=len(data['clips']), textures=textures, motion_source=motion if data['clips'] else None)
            except (AssertionError, KeyError, ValueError, struct.error) as error:
                record.update(status='unsupported', error=repr(error))
            report.append(record)
    (OUT/'manifest.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    entries = []
    for item in report:
        if item['status'] != 'converted': continue
        for texture in item['textures']:
            candidates = [p for p in OUT.rglob('*.tm2.png') if p.name.lower() == texture.lower()+'.png']
            candidates.sort(key=lambda p: (p.parent.name != item['group'], str(p)))
            if not candidates: raise ValueError('Missing texture: '+texture)
            name = item['group']+'_'+item['model']
            if len(item['textures']) > 1: name += '_'+texture[:-4]
            entries.append(dict(name=name, json='Analysis/PS2Enemies/'+item['group']+'/'+item['model']+'.json',
                                texture=candidates[0].relative_to(ROOT).as_posix()))
    (OUT/'import.json').write_text(json.dumps(dict(entries=entries),indent=2),encoding='utf-8')
    for item in report: print(item)
    print('Converted',sum(x['status']=='converted' for x in report),'of',len(report))

if __name__ == '__main__': main()
