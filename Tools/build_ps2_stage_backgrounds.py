"""Replace the first five stage backgrounds with original PS2 texture artwork.
Recompose textures over the current collision layout; does not claim PS2 map decoding.
"""
import hashlib
import json
import re
import struct
from PIL import Image, ImageDraw
from extract_ps2_hud import ISO, ROOT, read_file
from extract_ps2_characters import decode_texture

OUT=ROOT/'Assets/GainGround/Editable/OriginalCampaign/PS2Stages'
SOURCE=ROOT/'Analysis/PS2Stages'
PPU=64

def rectangles(text):
    return [tuple(map(float,m)) for m in re.findall(r'\n    x: ([\d.-]+)\n    y: ([\d.-]+)\n    width: ([\d.-]+)\n    height: ([\d.-]+)',text)]

def tiled(texture,size):
    result=Image.new('RGBA',size)
    for y in range(0,size[1],texture.height):
        for x in range(0,size[0],texture.width):result.paste(texture,(x,y))
    return result

def main():
    OUT.mkdir(parents=True,exist_ok=True);SOURCE.mkdir(parents=True,exist_ok=True)
    with ISO.open('rb') as stream:archive=read_file(stream,'DATA/DATA2.ROM')
    manifest=[]
    for stage in range(1,6):
        base=archive.index(f'fr1s{stage:02}.map'.encode()+b'\0')
        while re.fullmatch(rb'[\w.-]+\.(tm2|bin|map|anm|lst)',archive[base-32:base-16].split(b'\0')[0]):base-=32
        folder=SOURCE/f'1-{stage}';folder.mkdir(exist_ok=True);textures={};provenance=[]
        for i in range(256):
            entry=archive[base+i*32:base+(i+1)*32];raw=entry[:16].split(b'\0')[0]
            if not raw:break
            name=raw.decode('ascii');offset,size=struct.unpack_from('>II',entry,16)
            if not name.endswith('.tm2'):continue
            payload=archive[base+offset:base+offset+size];assert payload[:4]==b'TIM2' and len(payload)==size
            texture=decode_texture(payload);texture.save(folder/(name+'.png'));textures[name]=texture
            provenance.append(dict(file=name,offset=base+offset,size=size,sha256=hashlib.sha256(payload).hexdigest()))
        text=(ROOT/f'Assets/GainGround/Editable/OriginalCampaign/Stage_1_{stage}.asset').read_text(encoding='utf-8')
        width,height=map(float,re.search(r'^  size: \{x: ([\d.]+), y: ([\d.]+)\}',text,re.M).groups())
        size=(round(width*PPU),round(height*PPU))
        ground=textures[f'r1s{stage:02}_c.tm2']
        # Stage 2's C atlas includes a separate dark rock tile in its bottom-left.
        if stage==2:ground=ground.crop((0,0,256,128))
        image=tiled(ground,size)
        stone=textures.get(f'r1s{stage:02}_b.tm2')
        if stone is None:stone=textures[f'r1s{stage:02}_a.tm2'].crop((0,48,192,216))
        walls=rectangles(text.split('  walls:')[1].split('  enemies:')[0])
        exits=rectangles(text.split('  exit:')[1].split('  floorColor:')[0])
        def pixel_rect(rect):
            x,y,w,h=rect;return (round((x+width/2)*PPU),round((height/2-y-h)*PPU),round(w*PPU),round(h*PPU))
        # Shadow and raised rim make the unchanged collision boundaries legible.
        shadow=Image.new('RGBA',size);draw=ImageDraw.Draw(shadow)
        for rect in walls:
            x,y,w,h=pixel_rect(rect);draw.rectangle((x+6,y+6,x+w+5,y+h+7),fill=(0,0,0,95))
        image=Image.alpha_composite(image,shadow)
        for rect in walls:
            x,y,w,h=pixel_rect(rect);tile=tiled(stone,(w,h));image.paste(tile,(x,y))
            rim=ImageDraw.Draw(image);rim.line((x,y,x+w-1,y),fill=(180,174,134,255),width=2);rim.line((x+w-1,y,x+w-1,y+h-1,x,y+h-1),fill=(43,39,29,255),width=3)
        banner=decode_texture(read_file_from_archive(archive,1)).crop((0,0,512,40))
        for rect in exits:
            x,y,w,h=pixel_rect(rect)
            # Stretch only the blank sides; preserve the original EXIT lettering.
            sign=Image.new('RGBA',(w,40));middle=min(w,128);left=(w-middle)//2;right=w-middle-left
            if left:sign.paste(banner.crop((0,0,192,40)).resize((left,40)),(0,0))
            sign.paste(banner.crop((192,0,320,40)).resize((middle,40)),(left,0))
            if right:sign.paste(banner.crop((320,0,512,40)).resize((right,40)),(left+middle,0))
            image.alpha_composite(sign,(x,y+(h-sign.height)//2))
        image.convert('RGB').save(OUT/f'Stage_1_{stage}_PS2.png')
        manifest.append(dict(stage=f'1-{stage}',pixels=size,ppu=PPU,walls=walls,exits=exits,source_pack=f'fr1s{stage:02}.rom',textures=provenance))
    (OUT/'provenance.json').write_text(json.dumps(dict(source=ISO.name,method='Original PS2 textures recomposed on existing stage collision layouts',stages=manifest),indent=2),encoding='utf-8')
    print('Generated five PS2-textured stage backgrounds at 64 pixels per world unit.')

def read_file_from_archive(archive,stage):
    marker=f'fr1s{stage:02}.map'.encode()+b'\0';base=archive.index(marker)
    while re.fullmatch(rb'[\w.-]+\.(tm2|bin|map|anm|lst)',archive[base-32:base-16].split(b'\0')[0]):base-=32
    for i in range(256):
        p=base+i*32
        if archive[p:p+16].split(b'\0')[0]==f'r1s{stage:02}_a.tm2'.encode():
            offset,size=struct.unpack_from('>II',archive,p+16);return archive[base+offset:base+offset+size]
    raise ValueError('Missing original EXIT artwork')

if __name__=='__main__':main()
