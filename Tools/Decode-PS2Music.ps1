$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Add-Type -TypeDefinition @'
using System;
using System.IO;
public static class PS2MusicDecoder {
    static readonly int[] C1={0,60,115,98,122}, C2={0,0,-52,-55,-60};
    public static void Decode(string input,string output) {
        byte[] data=File.ReadAllBytes(input);
        if(data.Length%16384!=0)throw new InvalidDataException("Stereo block is incomplete");
        int count=data.Length/32*28;
        short[][] channels={new short[count],new short[count]};
        for(int channel=0;channel<2;channel++) {
            int h1=0,h2=0,index=0;
            for(int block=0;block<data.Length;block+=16384)
                for(int frame=block+channel*8192;frame<block+(channel+1)*8192;frame+=16) {
                    int predictor=data[frame]>>4,shift=data[frame]&15;
                    if(predictor>4 || shift>12)throw new InvalidDataException("Unsupported ADPCM frame");
                    for(int nibble=0;nibble<28;nibble++) {
                        int v=(data[frame+2+nibble/2]>>(4*(nibble%2)))&15;
                        if(v>=8)v-=16;
                        int sample=((v<<12)>>shift)+((h1*C1[predictor]+h2*C2[predictor]+32)>>6);
                        sample=Math.Max(-32768,Math.Min(32767,sample));
                        channels[channel][index++]=(short)sample;h2=h1;h1=sample;
                    }
                }
        }
        using(var w=new BinaryWriter(File.Create(output))) {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*4);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)2);
            w.Write(48000);w.Write(192000);w.Write((short)4);w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*4);
            byte[] pcm=new byte[count*4];
            for(int i=0;i<count;i++)for(int c=0;c<2;c++) {
                int v=channels[c][i],at=i*4+c*2;pcm[at]=(byte)v;pcm[at+1]=(byte)(v>>8);
            }
            w.Write(pcm);
        }
    }
}
'@
$outputRoot = Join-Path $projectRoot 'Assets/GainGround/Editable/Audio/PS2'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'Analysis/PS2Music/manifest.json') -Raw | ConvertFrom-Json
foreach ($track in $manifest) {
    $source = Join-Path $projectRoot ('Analysis/PS2Music/' + $track.file)
    $output = Join-Path $outputRoot ('PS2_' + $track.id + '.wav')
    [PS2MusicDecoder]::Decode($source,$output)
    Write-Output ('Decoded PS2_' + $track.id + ' (' + [math]::Round($track.samples/48000,1) + ' seconds)')
}
