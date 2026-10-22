"""Decode Gain Ground PS2's named float geometry/hierarchy/motion chunks.
Exports source-space data; Unity performs rendering, not image synthesis.
"""
import json, struct, math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Analysis/PS2Characters'
OUT = ROOT / 'Analysis/PS2Converted'
NAMES = {'athra':'Athra','baru':'Babalu','betexi':'Betty','cyber':'Cyber',
         'fessor':'Professor','fire':'Fire_Knight','gasu':'Gaston','general':'General',
         'glow':'Glow_Knight','honey':'Honey','jyoni':'Johnny','kid':'Kiddo',
         'kou':'Kou','lobby':'Robbie','mam':'Mamu','mars':'Moss','papi':'Mad_Poppy',
         'verbal':'Valkyrie','water':'Water_Knight','zaemon':'Zaemon'}

def ints(d, p): return struct.unpack_from('<4I', d, p)
def vec(d, p):
    values = struct.unpack_from('<3f', d, p)
    assert all(math.isfinite(x) for x in values)
    return dict(zip(('x','y','z'), values))
def sections(d):
    return {d[16+i*16:28+i*16].split(b'\0')[0].decode(): ints(d,16+i*16)[3]
            for i in range(ints(d,0)[3]-1)}
def links(d, p):
    return {(ints(d,o)[0], ints(d,o)[1]):ints(d,o)[2]
            for o in range(p+32,p+ints(d,p)[3]*16,16)}

def model(name, motion_name=None):
    d=(SOURCE/(name+'.bin')).read_bytes(); sec=sections(d)
    shape=links(d,sec['Shape'])[(0x30101,0x1000001)]
    pos=shape+32; parts=[]
    for k in range(ints(d,shape+16)[3]):
        h=ints(d,pos); end=pos+16+h[1]*4
        vertices=[]; uv=[]; triangles=[]; lines=[]; points=[]; cursor=pos+16
        while cursor+16<=end:
            index, size, flags, count=ints(d,cursor)
            if flags & 0xfffff000 == 0x80001000 and size==count*16 and 2<=count<256:
                assert cursor+16+count*64<=end, (name,k,cursor)
                start=len(vertices)
                for j in range(count):
                    p=cursor+16+j*16
                    assert struct.unpack_from('<f',d,p+12)[0] == 1
                    vertices.append(vec(d,p))
                    u,v=struct.unpack_from('<2f',d,cursor+16+count*32+j*16)
                    assert math.isfinite(u) and math.isfinite(v)
                    uv.append(dict(x=u,y=1-v))
                primitive=flags&7
                if primitive==4:
                    for j in range(2,count):
                        triangles.extend([start+j-2,start+j-1,start+j] if j%2==0 else [start+j-1,start+j-2,start+j])
                elif primitive==3:
                    assert count%3==0
                    triangles.extend(range(start,start+count))
                elif primitive==2:
                    for j in range(1,count): lines.extend([start+j-1,start+j])
                elif primitive==1:
                    lines.extend(range(start,start+count-count%2))
                elif primitive==0: points.extend(range(start,start+count))
                else: raise ValueError((name,'unknown primitive',hex(flags)))
                cursor+=16+count*64
            else: cursor+=16
        assert vertices, (name,k,'empty mesh')
        parts.append(dict(id=h[0],vertices=vertices,uv=uv,triangles=triangles,lines=lines,points=points))
        pos=end
    assert pos==shape+ints(d,shape)[3]*16
    hierarchy=links(d,sec['Hrchy'])
    h=hierarchy[(0x40101,0x1000001)]; pivots=hierarchy[(0x40101,0x1000002)]
    bones=[];count=ints(d,h+16)[3]
    assert count==ints(d,pivots+16)[3]
    for i in range(count):
        p=h+32+i*64;part,parent,_,_=struct.unpack_from('<4i',d,p+48)
        assert -1<=parent<count and parent!=i
        bones.append(dict(part=part,parent=parent,translation=vec(d,p),rotation=vec(d,p+16),
                          scale=vec(d,p+32),pivot=vec(d,pivots+32+i*16)))
    assert set(p['id'] for p in parts)==set(b['part'] for b in bones if b['part']>=0)
    return dict(parts=parts,bones=bones,clips=motions(motion_name or name,count) if (SOURCE/((motion_name or name)+'.anm')).exists() else [])

def records(d, start, values=False):
    result=[];p=start+32
    for i in range(ints(d,start+16)[3]):
        kind,index,words,count=ints(d,p)
        assert words*4%16==0
        if values:
            stride=words//count
            assert stride in (4,8,12)
            result.append((kind,[ [vec(d,p+16+j*stride*4+c*16) for c in range(stride//4)] for j in range(count)]))
        else: result.append(list(struct.unpack_from('<'+str(count)+'I',d,p+16)))
        p+=16+words*4
    assert p==start+ints(d,start)[3]*16
    return result

def motions(name,bone_count):
    d=(SOURCE/(name+'.anm')).read_bytes();clips=[]
    for label,p in sections(d).items():
        refs=links(d,p)
        times=records(d,refs[(0x50101,0x1000002)])
        values=records(d,refs[(0x50101,0x1000003)],True)
        mapping=refs[(0x50101,0x1000001)];tracks=[];duration=0
        for i in range(ints(d,mapping+16)[3]):
            bone,n,time_id,value_id=ints(d,mapping+32+i*16)
            flags,keys=values[value_id];frames=times[time_id]
            assert bone<bone_count and n==len(keys)==len(frames)
            assert flags & ~0x11200 == 0, hex(flags)
            channels=[]
            for flag,channel in [(0x200,'translation'),(0x1000,'rotation'),(0x10000,'scale')]:
                if flags&flag:channels.append(channel)
            assert len(channels)==len(keys[0]), (name,label,hex(flags))
            track=dict(bone=bone,frames=frames,translation=[],rotation=[],scale=[])
            for key in keys:
                for channel,value in zip(channels,key):track[channel].append(value)
            duration=max(duration,max(frames));tracks.append(track)
        clips.append(dict(name=label,duration=duration,tracks=tracks))
    return clips

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for source,name in NAMES.items():
        data=model(source);data['source']=source;data['character']=name
        (OUT/(name+'.json')).write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
        print(name,len(data['parts']),'meshes',len(data['bones']),'nodes',len(data['clips']),'motions')

if __name__=='__main__':main()

