using System;
using System.Collections.Generic;
namespace GainGround.Recovered {
    public static class ZeroRunDecoder {
        // ROM 004788-0047C0. Caller supplies D0 (DBRA count = blocks minus one).
        // 00 escapes the next byte; 01..DF literal; E0..FF = 1..32 zero bytes.
        // A run may cross the 32-byte threshold: original stops after the run.
        public static DecodedBlocks Decode(RomImage rom,int offset,ushort countMinusOne) {
            var bytes=new List<byte>();
            for(int block=0;block<=(int)countMinusOne;block++) {
                int produced=0;
                while(produced<32) {
                    byte token=rom.U8(offset++);
                    if(token==0) { bytes.Add(rom.U8(offset++)); produced++; }
                    else if(token<0xE0) { bytes.Add(token); produced++; }
                    else { int count=token-0xE0+1; for(int n=0;n<count;n++) bytes.Add(0); produced+=count; }
                }
            }
            return new DecodedBlocks {Bytes=bytes.ToArray(),NextOffset=offset};
        }
    }
}
