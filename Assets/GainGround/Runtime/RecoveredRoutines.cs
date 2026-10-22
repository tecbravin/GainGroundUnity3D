using System;
namespace GainGround.Recovered {
    // Semantic translations: preserve data results, not CPU flags, registers, or timing.
    public static class RecoveredRoutines {
        // ROM 0045E6-004614 and 004698-0046BA. Inputs are actual TH-low/high reads.
        public static byte DecodePad(byte thLow, byte thHigh) => unchecked((byte)~(((thLow << 2) & 0xC0) | (thHigh & 0x3F)));
        public static byte Pressed(byte previous, byte current) => (byte)((previous ^ current) & current);

        // ROM 0043C8-004406. Status reflects nonzero entries BEFORE subtraction.
        // Do not clamp: original word arithmetic wraps even for odd/invalid CRAM values.
        public static byte Fade16(ushort[] colors) {
            if(colors==null || colors.Length!=16) throw new ArgumentException("Exactly 16 words required.");
            byte status=0;
            for(int i=0;i<16;i++) {
                ushort before=colors[i]; if(before==0) continue; status=2;
                int value=before;
                if((before & 0x0F00)!=0) value-=0x0200;
                if((before & 0x00F0)!=0) value-=0x0020;
                if((before & 0x000F)!=0) value-=0x0002;
                colors[i]=unchecked((ushort)value);
            }
            return status;
        }
        // ROM 004408-004422: D6 is a DBRA count, so zero means ONE word.
        public static void FillWords(byte[] destination,int offset,ushort value,ushort countMinusOne) {
            int count=(int)countMinusOne+1; Check(destination,offset,count*2);
            for(int i=0;i<count;i++) { destination[offset++]=(byte)(value>>8); destination[offset++]=(byte)value; }
        }
        // ROM 004424-004442: sequential copy, deliberately NOT memmove semantics.
        public static void CopyWords(byte[] source,int sourceOffset,byte[] destination,int destinationOffset,ushort countMinusOne) {
            int count=(int)countMinusOne+1; Check(source,sourceOffset,count*2); Check(destination,destinationOffset,count*2);
            for(int i=0;i<count;i++) {
                byte hi=source[sourceOffset++],lo=source[sourceOffset++];
                destination[destinationOffset++]=hi; destination[destinationOffset++]=lo;
            }
        }
        // ROM 000504-000546; 64 KiB RAM image indexed by low 16 address bits.
        public static void InitializeRecords(RomImage rom,byte[] ram) {
            if(!rom.IsKnownImage) throw new ArgumentException("Routine addresses require the supplied ROM revision.");
            Check(ram,0,65536);
            for(int i=0;i<10;i++) {
                int dst=0xF006+i*16; ram[dst]=0; ram[dst+1]=(byte)(i+1);
                Array.Copy(rom.Slice(0x548+i*12,12),0,ram,dst+2,12);
                // The final two bytes of each record are skipped, not cleared.
            }
            foreach(int offset in new[]{0xF000,0xF002,0xF004}) { ram[offset]=0; ram[offset+1]=1; }
            ram[0xF804]=ram[0xF805]=0;
        }
        // ROM 000A08-000A28. Returns a code address; it does NOT execute the state.
        public static uint StateTarget(RomImage rom,byte state) {
            int index=state & 0xFC;
            if(index>16) throw new ArgumentOutOfRangeException(nameof(state),"Beyond the five evidenced table entries.");
            return rom.U32(0xA18+index);
        }
        static void Check(byte[] b,int p,int n) { if(b==null || p<0 || n<0 || p>b.Length-n) throw new ArgumentOutOfRangeException(nameof(p)); }
    }
}
