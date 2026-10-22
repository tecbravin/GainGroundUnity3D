using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using GainGround.Recovered;
public sealed class RomWorkbench : EditorWindow {
    RomImage rom; Texture2D preview; string info="Open the supplied ROM to begin.", offset="20000"; Vector2 scroll; int assetIndex;
    [MenuItem("Gain Ground/ROM Workbench")]
    static void Open() => GetWindow<RomWorkbench>("Gain Ground RE");
    void OnDisable() { if(preview!=null) DestroyImmediate(preview); }
    void OnGUI() {
        EditorGUILayout.HelpBox("ROM research workbench. For the independent playable recreation, open Editable/Scenes/GainGround.unity. Recovered graphics here still use diagnostic palettes.",MessageType.Info);
        if(GUILayout.Button("Open supplied ROM")) Load(Path.GetFullPath(Path.Combine(Application.dataPath,"../Analysis/OriginalROM.bin")));
        if(GUILayout.Button("Choose ROM...")) { string p=EditorUtility.OpenFilePanel("Genesis ROM","",""); if(p.Length>0) Load(p); }
        scroll=EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.SelectableLabel(info,EditorStyles.wordWrappedLabel,GUILayout.Height(170));
        if(rom!=null) {
            if(rom.IsKnownImage) {
                assetIndex=EditorGUILayout.IntSlider("Recovered table entry",assetIndex,0,19);
                if(GUILayout.Button("Preview recovered zero-run asset")) Attempt(()=>{
                    int source=(int)rom.U32(0x3CC68+assetIndex*16);
                    var decoded=ZeroRunDecoder.Decode(rom,source,0x86); Show(decoded.Bytes);
                    info="Table entry "+assetIndex+" | source 0x"+source.ToString("X6")+" | "+decoded.Bytes.Length+" decoded bytes.\nDiagnostic grayscale: palette and sprite assembly are not recovered.";
                });
            }
            offset=EditorGUILayout.TextField("ROM offset (hex)",offset);
            if(GUILayout.Button("Preview 256 raw 4bpp tiles (diagnostic grayscale)")) Attempt(()=>Show(rom.Slice(ParseOffset(),8192)));
            if(GUILayout.Button("Decode mask blocks at offset and preview")) Attempt(()=>{
                var data=MaskBlockDecoder.Decode(rom,ParseOffset()); Show(data.Bytes);
                info+="\nDecoded "+data.BlockCount+" blocks; next byte 0x"+data.NextOffset.ToString("X6")+". Successful parsing alone does not prove an asset boundary.";
            });
            if(GUILayout.Button("Export decoded bytes...")) Attempt(()=>{
                var data=MaskBlockDecoder.Decode(rom,ParseOffset());
                string path=EditorUtility.SaveFilePanel("Export decoded blocks","","decoded","bin");
                if(path.Length>0) File.WriteAllBytes(path,data.Bytes);
            });
        }
        if(preview!=null) GUILayout.Label(preview,GUILayout.Width(512),GUILayout.Height(512f*preview.height/preview.width));
        EditorGUILayout.EndScrollView();
    }
    int ParseOffset() => Convert.ToInt32(offset.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?offset.Substring(2):offset,16);
    void Attempt(Action action) { try { action(); } catch(Exception e) { info="Error: "+e.Message; } }
    void Load(string path) { Attempt(()=>{
        rom=new RomImage(File.ReadAllBytes(path));
        info=rom.Text(0x150,48)+"\n"+rom.Length+" bytes | "+rom.Text(0x180,14)+"\nSHA-256 "+rom.Sha256+
            "\nKnown input: "+rom.IsKnownImage+"\nReset PC: 0x"+rom.U32(4).ToString("X6")+
            "\nChecksum stored/calculated: "+rom.U16(0x18E).ToString("X4")+" / "+rom.Checksum.ToString("X4");
        if(rom.IsKnownImage) { info+="\nState targets:"; for(byte s=0;s<=16;s+=4) info+=" "+RecoveredRoutines.StateTarget(rom,s).ToString("X6"); }
    }); }
    void Show(byte[] data) {
        int count=Math.Min(data.Length/32,4096); if(count==0) throw new InvalidDataException("No complete tiles.");
        int width=128,height=((count+15)/16)*8;
        if(preview!=null) DestroyImmediate(preview);
        preview=new Texture2D(width,height,TextureFormat.RGBA32,false); preview.filterMode=FilterMode.Point;
        var pixels=new Color32[width*height];
        for(int tile=0;tile<count;tile++) for(int y=0;y<8;y++) for(int x=0;x<8;x++) {
            byte packed=data[tile*32+y*4+x/2]; byte value=(byte)(((x%2==0)?packed>>4:packed&15)*17);
            pixels[(height-1-(tile/16*8+y))*width+(tile%16*8+x)]=new Color32(value,value,value,255);
        }
        preview.SetPixels32(pixels); preview.Apply();
    }
}
