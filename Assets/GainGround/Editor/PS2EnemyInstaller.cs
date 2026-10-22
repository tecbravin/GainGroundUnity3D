using System;
using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class PS2EnemyInstaller
{
 [Serializable] public class Entry { public string name,json,texture; }
 [Serializable] public class Catalog { public Entry[] entries; }
 public const string Folder="Assets/GainGround/Editable/Characters/Models3D/Enemies";
 [MenuItem("Gain Ground/Import original PS2 enemy models")]
 public static void InstallAll()
 {
  var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText("Analysis/PS2Enemies/import.json"));
  foreach(var entry in catalog.entries)
   PS2CharacterInstaller.InstallSource(entry.name,entry.json,entry.texture,Folder+"/"+entry.name,false);
  ValidateAndPreview();
 }
 public static void ValidateAndPreview()
 {
  var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText("Analysis/PS2Enemies/import.json"));
  int motions=0;
  for(int page=0;page*16<catalog.entries.Length;page++)
  {
   var root=new GameObject("Enemy preview");root.transform.position=new Vector3(1000,0,0);
   for(int cell=0;cell<16 && page*16+cell<catalog.entries.Length;cell++)
   {
    string name=catalog.entries[page*16+cell].name;
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+"/"+name+".prefab");
    if(prefab==null)throw new Exception("Missing prefab: "+name);
    var view=Object.Instantiate(prefab,root.transform).GetComponent<PS2CharacterModel>();
    if(view.data.meshes.Length==0||view.data.material.mainTexture==null||!view.data.material.shader.isSupported)
     throw new Exception("Invalid model: "+name);
    for(int clip=0;clip<view.data.clips.Length;clip++)
    {
     for(int sample=0;sample<5;sample++)
     {
      view.Sample(clip,view.data.clips[clip].duration*sample/5f);
      foreach(var bone in view.bones)
       if(float.IsNaN(bone.position.sqrMagnitude)||float.IsInfinity(bone.position.sqrMagnitude))throw new Exception("Invalid animation: "+name);
     }
     motions++;
    }
    view.Sample(-1,0);view.rig.localRotation=Quaternion.Euler(20,0,0);
    var bounds=new Bounds(view.renderers[0].bounds.center,Vector3.zero);
    foreach(var renderer in view.renderers)bounds.Encapsulate(renderer.bounds);
    float scale=1.25f/Mathf.Max(.01f,Mathf.Max(bounds.size.x,bounds.size.y));
    view.transform.localScale*=scale;
    view.transform.position+=new Vector3(1000+cell%4*1.8f-2.7f,-cell/4*1.8f,0)-(bounds.center-root.transform.position)*scale-root.transform.position;
   }
   PS2CharacterInstaller.Capture("Validation/PS2_Enemies_"+page+".png",4,new Vector3(1000,-2.7f,-100),1200,1200);
   Object.DestroyImmediate(root);
  }
  File.WriteAllText("Validation/PS2Enemies.txt",catalog.entries.Length+" prefabs validated; "+motions+" motions sampled at five times each.");
 }
}
