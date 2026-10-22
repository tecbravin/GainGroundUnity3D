using System;
using System.Collections.Generic;
using System.IO;
namespace GainGround.Recovered {
    public sealed class DecodedBlocks {
        public byte[] Bytes;
        public int NextOffset;
        public int BlockCount => Bytes.Length/32;
    }
    public static class MaskBlockDecoder {
        // Translation of ROM 0046FC-004786, RAM-output branch 004768.
        // Each record builds 32 bytes: [count][value,u32 mask]*count[literals for uncovered bits].
        // count FF terminates. Bits map MSB-first. Later masks overwrite earlier masks.
        // The hardware-output branch emits the same bytes to a fixed VDP port.
        public static DecodedBlocks Decode(RomImage rom,int offset,int maxBlocks=4096) {
            if(maxBlocks<1 || maxBlocks>65535) throw new ArgumentOutOfRangeException(nameof(maxBlocks));
            var output=new List<byte>();
            while(true) {
                int count=rom.U8(offset++);
                if(count==255) return new DecodedBlocks { Bytes=output.ToArray(),NextOffset=offset };
                if(output.Count/32>=maxBlocks) throw new InvalidDataException("Block limit exceeded; wrong offset or missing terminator.");
                byte[] block=new byte[32]; uint covered=0;
                for(int n=0;n<count;n++) {
                    byte value=rom.U8(offset++); uint mask=rom.U32(offset); offset+=4; covered|=mask;
                    for(int bit=0;bit<32;bit++) if((mask & (0x80000000u>>bit))!=0) block[bit]=value;
                }
                for(int bit=0;bit<32;bit++) if((covered & (0x80000000u>>bit))==0) block[bit]=rom.U8(offset++);
                output.AddRange(block);
            }
        }
    }
}
