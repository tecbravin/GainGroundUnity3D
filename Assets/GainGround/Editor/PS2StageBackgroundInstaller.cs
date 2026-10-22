using GainGround.Game;
using UnityEditor;
using UnityEngine;
public static class PS2StageBackgroundInstaller
{
 public const string Folder="Assets/GainGround/Editable/OriginalCampaign/PS2Stages";
 [MenuItem("Gain Ground/Install PS2 backgrounds for stages 1-5")]
 public static void Install()
 {
  AssetDatabase.Refresh();
  for(int i=1;i<=5;i++)
  {
   string path=Folder+"/Stage_1_"+i+"_PS2.png";
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);
   importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
   importer.spritePixelsPerUnit=64;importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;
   importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;
   importer.maxTextureSize=2048;importer.SaveAndReimport();
   var stage=AssetDatabase.LoadAssetAtPath<StageDefinition>("Assets/GainGround/Editable/OriginalCampaign/Stage_1_"+i+".asset");
   stage.background=AssetDatabase.LoadAssetAtPath<Sprite>(path);stage.terrainAtlas=null;EditorUtility.SetDirty(stage);
  }
  AssetDatabase.SaveAssets();
 }
}
