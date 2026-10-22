"""Recover Gain Ground SMPS Z80 notes from the supplied, hash-verified ROM.
Reference: sonicretro/smps-rips Gain Ground definitions and ValleyBell/SMPSPlay.
This is a sampled rearrangement: chip envelopes/modulation are not emulated.
"""
from pathlib import Path
import hashlib, json, struct, sys
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT.parent / '.audio-tools/python'))
ROM = ROOT / 'Analysis/OriginalROM.bin'
OUT = ROOT / 'Analysis/Music'
SONGS = [(0x1887a,4737,'01_Genesis'),(0x19afb,7009,'02_Europe'),(0x1b65c,2672,'03_China'),(0x1c0cc,4030,'04_Present'),(0x1d08a,4103,'05_Future'),(0x1e091,281,'06_Victory'),(0x1e1aa,4172,'07_SpaceTime')]
LENS = [2,2,2,1,6,3,2,1,2,3,4,4,2,3,3,2,5,3,1,2,2,2,3,5,3,1,2,2,2,2,5,1]
def signed(x): return x if x<128 else x-256
def decode(data,offset,name):
    base=offset-0x10000
    def ptr(p): return int.from_bytes(data[p:p+2],'little')-base
    fm,psg,mult,tempo=data[2:6]
    assert tempo==0, 'Unexpected tempo divider'
    tracks=[]; timers=[]; h=6
    for ch in range(fm+psg):
        ispsg=ch>=fm; drum=ch==0
        p=ptr(h); trans=signed(data[h+2]); vol=data[h+3]; h+=6 if ispsg else 4
        t=0; duration=1; last=0x80; ins=0; gate=0; hold=False; stack=[]; loops={}; seen={}; notes=[]; commands={}; loop_start=None; pan=64
        for _ in range(100000):
            assert 0<=p<len(data),(name,ch,hex(p))
            seen.setdefault(p,t)
            b=data[p]; at=p; p+=1
            if b>=0xe0:
                commands[f'{b:02x}']=commands.get(f'{b:02x}',0)+1
                n=LENS[b-0xe0]; a=data[p:p+n-1]; p=at+n
                if b in (0xe3,0xf2): break
                if b==0xf6:
                    target=ptr(at+1)
                    if target in seen and not stack:
                        loop_start=seen[target]; break
                    p=target
                elif b==0xf7:
                    index,count=a[:2]; loops[index]=loops.get(index,0) or count or 256
                    loops[index]-=1
                    if loops[index]: p=ptr(at+3)
                elif b==0xf8: stack.append(p); p=ptr(at+1)
                elif b==0xf9: p=stack.pop()
                elif b==0xef:
                    ins=a[0]
                    if ins>=128: p+=1
                elif b==0xfb: trans=signed((trans+a[0])&255)
                elif b==0xfa: mult=a[0]
                elif b==0xe8: gate=a[0]*mult
                elif b==0xe7: hold=True
                elif b==0xe0: pan={0x80:24,0x40:104,0xc0:64}.get(a[0]&0xc0,64)
                elif b==0xea: timers.append((t,int.from_bytes(a[:2],'little')))
                elif b==0xe6 and not ispsg: vol=(vol+signed(a[0]))&127
                elif b==0xec and ispsg: vol=(vol+signed(a[0]))&15
                elif b==0xe5: vol=(vol+signed(a[1 if ispsg else 0]))&(15 if ispsg else 127)
                elif b in (0xfc,0xfd,0xfe,0xff,0xeb): raise ValueError(f'Unsupported structural command {b:02x} at {at:x}')
                continue
            rest=b==0x80
            if b>=0x80:
                if not rest: last=b
                if p<len(data) and data[p]<0x80: duration=(data[p]*mult)&255; p+=1
            else: duration=(b*mult)&255
            duration=duration or 256
            if not rest and last!=0x80:
                pitch=last-0x81+trans+(48 if ispsg else 12)
                length=min(duration,gate) if gate else duration
                if drum:
                    # Dual FM/PSG drum commands can trigger two percussion voices.
                    ids=[]
                    if last&15: ids.append({1:36,2:38,3:45,4:47,5:50,6:36,7:38,8:49,9:51}.get(last&15,42))
                    if last&0x70: ids.append({0x10:42,0x20:46,0x30:44,0x40:49,0x50:51}.get(last&0x70,42))
                    for key in ids: notes.append(dict(t=t,d=min(duration,6),key=key,ins=ins,vol=vol,pan=64))
                elif hold and notes and notes[-1]['key']==pitch:
                    notes[-1]['d']=t+length-notes[-1]['t']
                else:
                    assert 0<=pitch<128,(name,ch,pitch)
                    notes.append(dict(t=t,d=length,key=pitch,ins=ins,vol=vol,pan=pan))
            hold=False; t+=duration
        else: raise ValueError('Decoder instruction limit')
        tracks.append(dict(channel=ch,kind='drums' if drum else 'psg' if ispsg else 'fm',ticks=t,loop_start=loop_start,commands=commands,notes=notes))
    timer_values=set(v for _,v in timers)
    assert len(timer_values)<=1, 'Tempo changes require piecewise timing'
    timer=next(iter(timer_values),0xb4)
    tick_seconds=144*(1024-(timer&1023))/(53693175/7)
    return dict(name=name,rom_offset=hex(offset),bytes=len(data),sha256=hashlib.sha256(data).hexdigest(),timer_a=timer,tick_seconds=tick_seconds,tracks=tracks)

def main():
    rom=ROM.read_bytes()
    assert hashlib.sha256(rom).hexdigest()=='38116145d3e4dd086bd364ba44ee0cb176d75299dbdd516dc0e2de8aba73faab'
    OUT.mkdir(exist_ok=True)
    for offset,size,name in SONGS:
        b=rom[offset:offset+size]; (OUT/(name+'.smy')).write_bytes(b)
        if name not in ('01_Genesis','06_Victory'): continue
        song=decode(b,offset,name)
        (OUT/(name+'.json')).write_text(json.dumps(song,indent=2))
        print(name,'tick',song['tick_seconds'],'tracks',[(x['kind'],len(x['notes']),x['ticks'],x['loop_start'],sorted(set(n['ins'] for n in x['notes']))) for x in song['tracks']])
if __name__=='__main__': main()
