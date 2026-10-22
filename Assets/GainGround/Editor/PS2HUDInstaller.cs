using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PS2HUDInstaller
{
    const string Root="Assets/GainGround/Editable/PS2HUD";
    [MenuItem("Gain Ground/Install PS2 HUD")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        foreach(var file in Directory.GetFiles(Root,"*.png"))
        {
            var path=file.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.npotScale=TextureImporterNPOTScale.None;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        foreach(var name in OriginalCharactersInstaller.Names)
        {
            var hero=CharacterAssets.Load(name);
            var portrait=Load(name.Replace(" ","_"));
            if(portrait!=null) {hero.hudPortrait=portrait;EditorUtility.SetDirty(hero);}
        }
        EditorSceneManager.OpenScene("Assets/GainGround/Editable/Scenes/GainGround.unity");
        var game=Object.FindObjectOfType<GainGroundGame>();
        game.hudScore=Load("Score");game.hudPlayerFrame=Load("PlayerFrame");game.hudTimeEnemy=Load("TimeEnemy");
        EditorUtility.SetDirty(game);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();
        Debug.Log("PS2 HUD installed with original disc textures.");
    }
    static Texture2D Load(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+name+".png");
}
