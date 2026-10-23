using System.IO;
using UnityEditor;
using UnityEngine;

public static class PS2HUDInstaller
{
    const string Root="Assets/GainGround/Editable/PS2HUD";
    [MenuItem("Gain Ground/Install PS2 portraits")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        foreach(var file in Directory.GetFiles(Root,"*.png"))
        {
            var path=file.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            // The HUD prefab previews Johnny and Athra as sprites. Keep the other
            // portraits as textures for the streamed portrait bundles.
            var name=Path.GetFileNameWithoutExtension(file);
            importer.textureType=name=="Johnny"||name=="Athra"?
                TextureImporterType.Sprite:TextureImporterType.Default;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        foreach(var name in OriginalCharactersInstaller.Names)
        {
            var hero=CharacterAssets.Load(name);
            var portrait=Load(name.Replace(" ","_"));
            if(portrait!=null) {hero.hudPortrait=portrait;EditorUtility.SetDirty(hero);}
        }
        AssetDatabase.SaveAssets();
        Debug.Log("PS2 character portraits imported.");
    }
    static Texture2D Load(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+name+".png");
}
