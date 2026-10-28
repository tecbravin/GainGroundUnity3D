using System;
using System.IO;
using System.Collections.Generic;
using GainGround.Game;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class PS2CharacterInstaller
{
 [Serializable] class Part { public int id; public Vector3[] vertices; public Vector2[] uv; public int[] triangles,lines,points; }
 [Serializable] class Source { public string source,character; public Part[] parts; public PS2ModelData.Bone[] bones; public PS2ModelData.Motion[] clips; }
 const string Folder="Assets/GainGround/Editable/Characters/Models3D";
 public static void InstallAthra(){Install("Athra");Preview("Athra");}
 [MenuItem("Gain Ground/Install original PS2 character models")]
 public static void InstallAll(){foreach(var name in OriginalCharactersInstaller.Names)Install(name.Replace(" ","_"));PreviewRoster();}
 static void Install(string name)
 {
  InstallSource(name, "Analysis/PS2Converted/"+name+".json", null, Folder+"/"+name, true);
 }
 public static void InstallSource(string name,string jsonPath,string texturePath,string folder,bool bindCharacter,
                                  Material sharedMaterial=null,float? sourceScale=null,bool stageObject=false,
                                  bool saveAssets=true)
 {
  var source=JsonUtility.FromJson<Source>(File.ReadAllText(jsonPath));
  Directory.CreateDirectory(folder);
  if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.Refresh();
  var material=sharedMaterial;
  if(material==null)
  {
   string textureName=source.source=="papi"?"papxi":source.source;
   File.Copy(texturePath??("Analysis/PS2Characters/"+textureName+".tm2.png"),folder+"/Texture.png",true);AssetDatabase.Refresh();
   var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"/Texture.png");
   importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;
   importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
   material=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Original.mat");
   if(material==null){material=new Material(Shader.Find("GainGround/PS2Character"));AssetDatabase.CreateAsset(material,folder+"/Original.mat");}
   material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Texture.png");EditorUtility.SetDirty(material);
  }
  var data=AssetDatabase.LoadAssetAtPath<PS2ModelData>(folder+"/Model.asset");
  if(data==null){data=ScriptableObject.CreateInstance<PS2ModelData>();AssetDatabase.CreateAsset(data,folder+"/Model.asset");}
  data.bones=source.bones;data.clips=source.clips;data.material=material;
  if(sourceScale.HasValue)data.modelScale=sourceScale.Value;
  data.meshes=new Mesh[source.parts.Length];data.partIds=new int[source.parts.Length];
  var root=new GameObject(name);var view=root.AddComponent<PS2CharacterModel>();view.data=data;
  if(name=="Betty")view.viewPitchDegrees=-35;
  view.rig=new GameObject("Original PS2 rig").transform;view.rig.SetParent(root.transform,false);
  view.rig.localScale=new Vector3(data.modelScale,stageObject?data.modelScale:-data.modelScale,data.modelScale);
  if(stageObject)view.rig.localRotation=Quaternion.Euler(60,0,0);
  view.bones=new Transform[data.bones.Length];var renderers=new List<Renderer>();
  for(int i=0;i<view.bones.Length;i++)view.bones[i]=new GameObject("Bone_"+i).transform;
  for(int i=0;i<view.bones.Length;i++)
  {
   var bone=data.bones[i];view.bones[i].SetParent(bone.parent<0?view.rig:view.bones[bone.parent],false);
   if(bone.part<0)continue;
   int partIndex=Array.FindIndex(source.parts,p=>p.id==bone.part);var part=source.parts[partIndex];
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/Part_"+part.id+".asset");
   if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,folder+"/Part_"+part.id+".asset");}
   mesh.Clear();mesh.name=name+"_"+part.id;var vertices=new Vector3[part.vertices.Length];
   for(int j=0;j<vertices.Length;j++)vertices[j]=part.vertices[j]-bone.pivot;
   mesh.vertices=vertices;mesh.uv=part.uv;
   mesh.subMeshCount=1+(part.lines.Length>0?1:0)+(part.points.Length>0?1:0);
   mesh.SetIndices(part.triangles,MeshTopology.Triangles,0);
   int submesh=1;
   if(part.lines.Length>0)mesh.SetIndices(part.lines,MeshTopology.Lines,submesh++);
   if(part.points.Length>0)mesh.SetIndices(part.points,MeshTopology.Points,submesh++);
   mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
   data.meshes[partIndex]=mesh;data.partIds[partIndex]=part.id;
   var visual=new GameObject("Mesh_"+part.id);visual.transform.SetParent(view.bones[i],false);
   visual.AddComponent<MeshFilter>().sharedMesh=mesh;
   var renderer=visual.AddComponent<MeshRenderer>();var materials=new Material[mesh.subMeshCount];for(int m=0;m<materials.Length;m++)materials[m]=material;
   renderer.sharedMaterials=materials;renderer.sortingOrder=4;renderers.Add(renderer);
  }
  view.renderers=renderers.ToArray();view.Sample(0,0);EditorUtility.SetDirty(data);
  PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+name+".prefab");Object.DestroyImmediate(root);
  if(bindCharacter)
  {
  var hero=CharacterAssets.Load(name.Replace("_"," "));
  hero.modelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/"+name+".prefab").GetComponent<PS2CharacterModel>();
  hero.characterTint=Color.white;EditorUtility.SetDirty(hero);
  }
  if(saveAssets)AssetDatabase.SaveAssets();
 }
 public static void Preview(string name)
 {
  var root=new GameObject("Preview");root.transform.position=new Vector3(100,0,0);
  for(int i=0;i<10;i++)
  {
   var view=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+"/"+name+".prefab"))).GetComponent<PS2CharacterModel>();
   view.transform.SetParent(root.transform,false);view.transform.localPosition=new Vector3(i%5*1.5f-3,-(i/5)*1.7f,0);
   view.rig.localRotation=Quaternion.Euler(20,0,0);view.Sample(i,view.data.clips[i].duration*.25f);
  }
  Capture("Validation/PS2_"+name+".png",1.8f,new Vector3(100,-.2f,-10),1500,650);Object.DestroyImmediate(root);
 }
 static void PreviewRoster()
 {
  var root=new GameObject("Roster preview");root.transform.position=new Vector3(100,0,0);
  for(int i=0;i<OriginalCharactersInstaller.Names.Length;i++)
  {
   string name=OriginalCharactersInstaller.Names[i].Replace(" ","_");
   var view=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+"/"+name+".prefab"))).GetComponent<PS2CharacterModel>();
   view.transform.SetParent(root.transform,false);view.transform.localPosition=new Vector3(i%5*1.5f-3,-(i/5)*1.7f,0);
   view.rig.localRotation=Quaternion.Euler(20,0,0);view.Sample(2,10);
  }
  Capture("Validation/PS2_Roster.png",4,new Vector3(100,-2,-10),1200,1200);Object.DestroyImmediate(root);
 }
 public static void Capture(string path,float size,Vector3 position,int width,int height)
 {
  var go=new GameObject("Preview camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=size;
  camera.transform.position=position;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.18f);
  var rt=new RenderTexture(width,height,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
  var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
  Directory.CreateDirectory("Validation");File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;
  Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);
 }
}
