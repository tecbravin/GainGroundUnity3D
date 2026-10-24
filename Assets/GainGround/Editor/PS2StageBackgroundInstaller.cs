using GainGround.Game;
using UnityEditor;
using UnityEngine;
public static class PS2StageBackgroundInstaller
{
 public const string Folder="Assets/GainGround/Editable/OriginalCampaign/Complete/Maps";
 [MenuItem("Gain Ground/Install Complete maps for stages 1-5")]
 public static void Install()
 {
  AssetDatabase.Refresh();
  for(int i=1;i<=5;i++)
  {
   string path=Folder+"/1-"+i.ToString("00")+".png";
   var stage=AssetDatabase.LoadAssetAtPath<StageDefinition>("Assets/GainGround/Editable/OriginalCampaign/Stage_1_"+i+".asset");
   stage.background=AssetDatabase.LoadAssetAtPath<Sprite>(path);stage.terrainAtlas=null;EditorUtility.SetDirty(stage);
  }
  AssetDatabase.SaveAssets();
 }
}
