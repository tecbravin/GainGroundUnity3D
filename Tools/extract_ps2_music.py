"""Read-only extraction of the 17 PS2 music streams. Decode with Decode-PS2Music.ps1."""
from pathlib import Path
import json, hashlib
from extract_ps2_hud import read_file,ISO,ROOT
NAMES=["0000","1000","1100","1101","2000","2100","2101","3000","3100","3101","4000","4100","4101","4102","5000","5001","6000"]
OUT=ROOT/"Analysis/PS2Music"
def main():
    OUT.mkdir(exist_ok=True);records=[]
    with ISO.open("rb") as f:
        for name in NAMES:
            filename="DATA"+name+".BIN";data=read_file(f,"DATA/SOUND/"+filename)
            if len(data)%16384:raise ValueError("Incomplete stereo interleave: "+filename)
            if any((data[i]>>4)>4 or (data[i]&15)>12 for i in range(0,len(data),16)):
                raise ValueError("Invalid PS ADPCM frame: "+filename)
            (OUT/filename).write_bytes(data)
            records.append(dict(id=name,file=filename,bytes=len(data),sha256=hashlib.sha256(data).hexdigest(),
                sampleRate=48000,channels=2,interleave=8192,samples=len(data)//32*28))
    (OUT/"manifest.json").write_text(json.dumps(records,indent=2),encoding="utf-8")
    print("Extracted",len(records),"streams from local ISO without modification.")
if __name__=="__main__":main()
