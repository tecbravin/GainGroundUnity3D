using System.IO;
using UnityEditor;
using UnityEngine;
public static class PS2TitleInstaller
{
 [MenuItem("Gain Ground/Import PS2 title artwork")]
 public static void Install()
 {
  AssetDatabase.Refresh();
  foreach(var file in Directory.GetFiles("Assets/GainGround/Resources/PS2Title","*.png"))
  {
   var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
   importer.textureType=TextureImporterType.Default;importer.npotScale=TextureImporterNPOTScale.None;
   importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;
   importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
  }
  AssetDatabase.SaveAssets();
 }
}
