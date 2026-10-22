#!/usr/bin/env python3
"""Reproducible analysis of the supplied revision; no heuristic full-ROM disassembly."""
import argparse,hashlib,json,struct
from pathlib import Path
EXPECTED='38116145d3e4dd086bd364ba44ee0cb176d75299dbdd516dc0e2de8aba73faab'
def zero_run(b,p,count_minus_one):
    out=bytearray(); sizes=[]
    for _ in range(count_minus_one+1):
        start=len(out)
        while len(out)-start<32:
            t=b[p];p+=1
            if t==0:out.append(b[p]);p+=1
            elif t<0xe0:out.append(t)
            else:out.extend(bytes(t-0xe0+1))
        sizes.append(len(out)-start)
    return bytes(out),p,sizes

def mask_blocks(b,p,limit=4096):
    out=bytearray()
    while True:
        count=b[p];p+=1
        if count==255:return bytes(out),p
        if len(out)//32>=limit:raise ValueError('block limit')
        covered=0;block=bytearray(32)
        for _ in range(count):
            value=b[p];mask=struct.unpack_from('>I',b,p+1)[0];p+=5;covered|=mask
            for bit in range(32):
                if mask & (1<<(31-bit)):block[bit]=value
        for bit in range(32):
            if not covered & (1<<(31-bit)):block[bit]=b[p];p+=1
        out.extend(block)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('rom',type=Path);parser.add_argument('--output',type=Path,default=Path('Analysis'))
    args=parser.parse_args();b=args.rom.read_bytes();out=args.output;out.mkdir(parents=True,exist_ok=True)
    sha=hashlib.sha256(b).hexdigest()
    if sha!=EXPECTED:raise SystemExit('Different ROM revision: fixed addresses are not safe to apply.')
    u16=lambda p:struct.unpack_from('>H',b,p)[0]
    u32=lambda p:struct.unpack_from('>I',b,p)[0]
    report={'sha256':sha,'size':len(b),'title':b[0x150:0x180].decode('ascii').strip(),'serial':b[0x180:0x18e].decode('ascii'),
        'stored_checksum':hex(u16(0x18e)),'calculated_checksum':hex(sum(u16(p) for p in range(512,len(b),2))&65535),
        'initial_sp':hex(u32(0)),'reset_pc':hex(u32(4)),
        'vectors':[{'number':n,'target':hex(u32(n*4))} for n in range(64)],
        'state_targets':[hex(u32(0xa18+i*4)) for i in range(5)],'asset_candidates':[]}
    # Table base and 16-byte stride observed at 0050B6 and 00521E-00522C.
    # 20 entries fit before first data pointer 03CDA8; table count inferred, not a decoded loop bound.
    for i in range(20):
        p=u32(0x3cc68+i*16);decoded,end,sizes=zero_run(b,p,0x86)
        (out/f'asset_{i:02d}.4bpp').write_bytes(decoded)
        report['asset_candidates'].append({'index':i,'source':hex(p),'end':hex(end),'bytes':len(decoded),'all_blocks_32_bytes':all(n==32 for n in sizes),'decoded_sha256':hashlib.sha256(decoded).hexdigest()})
    # Source copies are proven by LEA and DBRA bounds at 000444-000482.
    for label,start,size in [('z80_bank_0000',0x1f524,0x10b2),('z80_bank_1100',0x205d6,0x636),('z80_parameters_1c00',0x4a8,10)]:
        (out/(label+'.bin')).write_bytes(b[start:start+size])
    (out/'rom_report.json').write_text(json.dumps(report,indent=2)+'\n')
    from capstone import Cs,CS_ARCH_M68K,CS_MODE_BIG_ENDIAN,CS_MODE_M68K_000
    dis=Cs(CS_ARCH_M68K,CS_MODE_BIG_ENDIAN|CS_MODE_M68K_000)
    ranges=[(0x202,0x290),(0x2fc,0x31e),(0x326,0x336),(0x33e,0x360),(0x360,0x3f2),(0x41a,0x4a8),(0x4b2,0x548),(0xa02,0xa18),(0x43c8,0x4444),(0x44e0,0x44fa),(0x4580,0x46bc),(0x46d6,0x47c2),(0x50b6,0x50da),(0x521e,0x5248)]
    lines=['; Curated code regions only. Gaps include inline arguments and data.','; Addresses and bytes are evidence, symbolic purposes are analyst interpretation.']
    for start,end in ranges:
        lines.append(f'\n; [{start:06X}, {end:06X})')
        for ins in dis.disasm(b[start:end],start):lines.append(f'{ins.address:06X}  {ins.bytes.hex():24s} {ins.mnemonic:10s} {ins.op_str}')
    (out/'verified_regions.asm').write_text('\n'.join(lines)+'\n')
    print(json.dumps({'sha256':sha,'assets_decoded':20,'exact_32_byte_blocks':all(x['all_blocks_32_bytes'] for x in report['asset_candidates']),'output':str(out)},indent=2))
if __name__=='__main__':main()
