"""Extract the original TIM2 HUD artwork from the user-supplied PS2 ISO.
Read-only ISO9660 reader; exports only interface images, never executable code.
Requires Pillow. Run from any directory. Original disc images remain unchanged.
"""
from pathlib import Path
import struct, re, json, hashlib
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
ISO=ROOT.parent/"Sega Ages Vol. 9 Gain Ground.iso"
OUT=ROOT/"Assets/GainGround/Editable/PS2HUD"
PORTRAITS={1:"Athra",3:"Betty",4:"Cyber",5:"General",6:"Zaemon",7:"Gaston",
8:"Kou",9:"Glow Knight",10:"Honey",11:"Johnny",12:"Kiddo",13:"Professor",
14:"Robbie",15:"Fire Knight",16:"Mamu",17:"Babalu",18:"Moss",19:"Water Knight",25:"Valkyrie"}

def read_file(f,target):
    f.seek(16*2048);pvd=f.read(2048)
    if pvd[1:6]!=b"CD001":raise ValueError("Not an ISO9660 image")
    record=pvd[156:]
    for part in target.split("/"):
        extent=struct.unpack_from("<I",record,2)[0];size=struct.unpack_from("<I",record,10)[0]
        f.seek(extent*2048);directory=f.read(size);i=0;found=None
        while i<len(directory):
            length=directory[i]
            if not length:i=(i//2048+1)*2048;continue
            entry=directory[i:i+length];i+=length
            name=entry[33:33+entry[32]].split(b";")[0].decode("ascii")
            if name==part:found=entry;break
        if found is None:raise ValueError("Missing ISO entry: "+part)
        record=found
    f.seek(struct.unpack_from("<I",record,2)[0]*2048)
    return f.read(struct.unpack_from("<I",record,10)[0])

def decode(data,offset):
    h=offset+16
    total,clut_size,image_size,header_size,colors=struct.unpack_from("<IIIHH",data,h)
    clut_type,image_type=data[h+18:h+20];width,height=struct.unpack_from("<HH",data,h+20)
    start=h+header_size;raw=data[start:start+image_size];palette=data[start+image_size:start+image_size+clut_size]
    if image_type==5 and clut_type&7==3:
        entries=[tuple(palette[i:i+4]) for i in range(0,len(palette),4)]
        if not clut_type&128:
            entries=[entries[(i&~24)|((i&8)<<1)|((i&16)>>1)] for i in range(len(entries))]
        pixels=[entries[i] for i in raw[:width*height]]
    elif image_type==3:
        pixels=[tuple(raw[i:i+4]) for i in range(0,width*height*4,4)]
    else:raise ValueError("Unsupported TIM2 format")
    image=Image.new("RGBA",(width,height))
    image.putdata([(r,g,b,min(255,a*2)) for r,g,b,a in pixels])
    return image

def main():
    with ISO.open("rb") as f:archive=read_file(f,"DATA/DATA2.ROM")
    packs={}
    for i in range(struct.unpack_from("<I",archive)[0]):
        entry=archive[32+i*32:64+i*32];name=entry[:16].split(b"\0")[0].decode("ascii")
        offset,size=struct.unpack_from(">II",entry,16)
        if name:packs[name]=(offset,size)
    base,size=packs["select.rom"]
    pack=archive[base:]
    offsets=[m.start() for m in re.finditer(b"TIM2",pack[:size])]
    OUT.mkdir(parents=True,exist_ok=True);records=[]
    for index,name in {**PORTRAITS,21:"Score",22:"PlayerFrame",23:"Status",24:"TimeEnemy"}.items():
        image=decode(pack,offsets[index]);filename=name.replace(" ","_")+".png"
        image.save(OUT/filename)
        records.append(dict(file=filename,pack="select.rom",texture=index,offset=offsets[index],size=list(image.size)))
    # Mad Poppy's portrait is embedded in player.rom, separate from its model texture.
    
    # Texture 10 is Johnny again, so no portrait is falsely assigned to Mad Poppy.
    (OUT/"provenance.json").write_text(json.dumps(dict(source=ISO.name,
        archive_sha256=hashlib.sha256(archive).hexdigest(),assets=records,
        missing_portrait=["Mad Poppy"]),indent=2),encoding="utf-8")
    print("Extracted",len(records),"original HUD textures.")
if __name__=="__main__":main()
