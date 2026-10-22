using System;
using System.Security.Cryptography;
using System.Text;
namespace GainGround.Recovered {
    public sealed class RomImage {
        public const string ExpectedSha256 = "38116145d3e4dd086bd364ba44ee0cb176d75299dbdd516dc0e2de8aba73faab";
        readonly byte[] data;
        public int Length => data.Length;
        public RomImage(byte[] bytes) {
            if (bytes == null || bytes.Length < 512 || (bytes.Length & 1) != 0)
                throw new ArgumentException("Expected an even-sized Genesis ROM, at least 512 bytes.");
            data = (byte[])bytes.Clone();
        }
        public byte U8(int offset) { Check(offset, 1); return data[offset]; }
        public ushort U16(int offset) { Check(offset, 2); return (ushort)((data[offset] << 8) | data[offset+1]); }
        public uint U32(int offset) { return ((uint)U16(offset) << 16) | U16(offset+2); }
        public byte[] Slice(int offset, int size) { Check(offset,size); var result=new byte[size]; Array.Copy(data,offset,result,0,size); return result; }
        public string Text(int offset, int size) => Encoding.ASCII.GetString(Slice(offset,size)).Trim();
        public ushort Checksum { get { uint sum=0; for(int i=512;i<Length;i+=2) sum+=U16(i); return (ushort)sum; } }
        public string Sha256 { get { using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(data)).Replace("-","").ToLowerInvariant(); } }
        public bool IsKnownImage => Sha256 == ExpectedSha256;
        void Check(int offset,int size) { if(offset<0 || size<0 || offset>data.Length-size) throw new ArgumentOutOfRangeException(nameof(offset)); }
    }
}
