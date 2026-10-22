"""Extract VAG sound effects from the locally supplied PS2 disc, without modifying it."""
import struct,re,wave,json,hashlib
from array import array
from pathlib import Path
from extract_ps2_hud import ISO,ROOT,read_file
OUT=ROOT/"Assets/GainGround/Editable/Audio/PS2SFX"
COEFFICIENTS=[(0,0),(60,0),(115,-52),(98,-55),(122,-60)]
def decode(data):
    samples=array("h");h1=h2=0
    for offset in range(0,len(data),16):
        frame=data[offset:offset+16];predictor=frame[0]>>4;shift=frame[0]&15
        if predictor>4 or shift>12:raise ValueError("Invalid ADPCM header")
        a,b=COEFFICIENTS[predictor]
        for byte in frame[2:]:
            for v in (byte&15,byte>>4):
                if v>=8:v-=16
                sample=max(-32768,min(32767,((v<<12)>>shift)+((h1*a+h2*b+32)>>6)))
                samples.append(sample);h2,h1=h1,sample
    return samples
def main():
    with ISO.open("rb") as f:archive=read_file(f,"DATA/DATA2.ROM")
    for i in range(struct.unpack_from("<I",archive)[0]):
        entry=archive[32+i*32:64+i*32]
        if entry[:16].split(b"\0")[0]==b"se.rom":
            offset,size=struct.unpack_from(">II",entry,16);bank=archive[offset:];bank_size=size;break
    else:raise ValueError("Missing se.rom")
    OUT.mkdir(exist_ok=True);records=[]
    for match in re.finditer(b"VAGp",bank[:bank_size]):
        o=match.start();size,rate=struct.unpack_from(">II",bank,o+12)
        name=bank[o+32:o+48].split(b"\0")[0].decode("ascii");name=Path(name).stem.upper()
        if size%16 or not 8000<=rate<=48000 or o+48+size>len(bank):raise ValueError("Invalid VAG size/rate")
        raw=bank[o+48:o+48+size];pcm=decode(raw)
        with wave.open(str(OUT/(name+".wav")),"wb") as w:
            w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes(pcm.tobytes())
        records.append(dict(name=name,offset=o,rate=rate,samples=len(pcm),seconds=len(pcm)/rate,sha256=hashlib.sha256(raw).hexdigest()))
    (OUT/"provenance.json").write_text(json.dumps(records,indent=2),encoding="utf-8")
    print("Extracted",len(records),"VAG clips; original sample rates preserved.")
if __name__=="__main__":main()


