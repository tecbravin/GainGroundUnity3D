"""Extract original title artwork and effect frames from the user's ISO."""
import hashlib
import json
from extract_ps2_hud import ISO, ROOT, read_file
from extract_ps2_characters import decode_texture

def main():
    out=ROOT/'Assets/GainGround/Resources/PS2Title'
    out.mkdir(parents=True,exist_ok=True)
    records=[]
    names=['TITLE_L','TITLE_R']+['LOGO_0'+str(i) for i in range(1,6)]+['NOISE'+str(i) for i in range(1,5)]
    with ISO.open('rb') as stream:
        for name in names:
            source='DATA/TEX/'+name+'.BIN'
            payload=read_file(stream,source)
            image=decode_texture(payload)
            image.save(out/(name+'.png'))
            records.append(dict(source=source,sha256=hashlib.sha256(payload).hexdigest(),size=list(image.size)))
    (ROOT/'Analysis/PS2Title/manifest.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
    print('Extracted',len(records),'original title textures.')

if __name__=='__main__':main()
