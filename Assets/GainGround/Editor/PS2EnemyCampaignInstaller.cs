using System;
using System.Collections.Generic;
using GainGround.Game;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class PS2EnemyCampaignInstaller
{
 public const string Folder="Assets/GainGround/Editable/Characters/Models3D/EnemyActors";
 static readonly Dictionary<string,string[]> Sources=new Dictionary<string,string[]>{
  {"warrior",new[]{"enemy01_ken01"}},{"axeman",new[]{"enemy01_ken02"}},
  {"archer",new[]{"enemy01_yumi"}},{"ninja",new[]{"enemy01_nife1"}},
  {"knight",new[]{"enemy02_knight1"}},{"soldier",new[]{"enemy02_kensi1"}},
  {"wizard",new[]{"enemy02_magic"}},{"slime",new[]{"enemy02_slime"}},
  {"brawler",new[]{"enemy03_nife1"}},{"runner",new[]{"enemy03_nife1"}},
  {"bomber",new[]{"enemy03_bomb"}},{"grenadier",new[]{"enemy03_bomb"}},
  {"rifleman",new[]{"enemy03_gun1"}},{"gunner",new[]{"enemy03_gun2"}},
  {"streetgun",new[]{"enemy03_kasa1"}},{"worm",new[]{"enemy03_musi01"}},
  {"robot",new[]{"enemy04_4_01a"}},{"spider",new[]{"enemy04_4_03a"}},
  {"turret",new[]{"enemy04_4_04a"}},{"core",new[]{"enemy04_4_05a"}},
  {"orb",new[]{"enemy04a_4st_08"}},{"barrier",new[]{"enemy04_4st_07"}},
  {"generator",new[]{"enemy04_4_06b"}},
  {"demon",new[]{"enemy01_zou_b","enemy01_zou_h"}},
  {"dragon",new[]{"enemy01a_r1b_b","enemy01a_r1b_hl","enemy01a_r1b_hr"}},
  {"giant",new[]{"enemy02a_r2b"}},
  {"sorcererBlue",new[]{"enemy03a_r3bb"}},{"sorcererRed",new[]{"enemy03a_r3br"}},
  {"tank",new[]{"enemy04a_r4b"}},{"computer",new[]{"enemy08a_r8b"}}
 };
 [MenuItem("Gain Ground/Assign PS2 enemies and assemble bosses")]
 public static void Install()
 {
  System.IO.Directory.CreateDirectory(Folder);
  foreach(var entry in Sources)Build(entry.Key,entry.Value);
  int assigned=0;
  foreach(string guid in AssetDatabase.FindAssets("t:StageDefinition",new[]{"Assets/GainGround/Editable"}))
  {
   var stage=AssetDatabase.LoadAssetAtPath<StageDefinition>(AssetDatabase.GUIDToAssetPath(guid));
   foreach(var spawn in stage.enemies)
   {
    string kind=spawn.sprite!=null?spawn.sprite.name:spawn.melee?"warrior":"archer";
    if(!Sources.ContainsKey(kind))throw new Exception("Unmapped enemy: "+kind+" in "+stage.name);
    spawn.modelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+kind+".prefab").GetComponent<PS2EnemyVisual>();assigned++;
   }
   EditorUtility.SetDirty(stage);
  }
  AssetDatabase.SaveAssets();Debug.Log("PS2 enemy assignments: "+assigned);
  PreviewBosses();
 }
 static void Build(string kind,string[] sources)
 {
  var root=new GameObject(kind);var view=root.AddComponent<PS2EnemyVisual>();
  view.rig=new GameObject("Facing").transform;view.rig.SetParent(root.transform,false);
  var geometry=new GameObject("Original assembled geometry").transform;geometry.SetParent(view.rig,false);
  view.parts=new PS2CharacterModel[sources.Length];
  for(int i=0;i<sources.Length;i++)
  {
   var asset=AssetDatabase.LoadAssetAtPath<GameObject>(PS2EnemyInstaller.Folder+"/"+sources[i]+"/"+sources[i]+".prefab");
   if(asset==null)throw new Exception("Missing source "+sources[i]);
   var part=Object.Instantiate(asset,geometry).GetComponent<PS2CharacterModel>();
   part.name=sources[i];part.rig.localRotation=Quaternion.identity;part.SampleEnemy(0,0);view.parts[i]=part;
  }
  var renderers=root.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
  foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
  float scale=1/Mathf.Max(.01f,bounds.size.y);geometry.localScale=Vector3.one*scale;
  geometry.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
  view.turnWithTarget=kind!="barrier"&&kind!="generator"&&kind!="computer"&&kind!="tank";
  PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+kind+".prefab");Object.DestroyImmediate(root);
 }
 public static void PreviewBosses()
 {
  string[] names={"dragon","giant","demon","sorcererBlue","sorcererRed","tank","computer"};
  var root=new GameObject("Boss preview");root.transform.position=new Vector3(100,0,0);
  for(int i=0;i<names.Length;i++)
  {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+names[i]+".prefab");
   var view=Object.Instantiate(prefab,root.transform).GetComponent<PS2EnemyVisual>();
   view.transform.localPosition=new Vector3(i%4*2-3,-i/4*2,0);view.Tick(0,Vector2.down,false);
  }
  PS2CharacterInstaller.Capture("Validation/PS2_AssembledBosses.png",2.5f,new Vector3(100,-.5f,-10),1400,800);Object.DestroyImmediate(root);
 }
}
