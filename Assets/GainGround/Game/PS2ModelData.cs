using System;
using UnityEngine;
namespace GainGround.Game
{
 public sealed class PS2ModelData : ScriptableObject
 {
  [Serializable] public class Bone { public int part,parent; public Vector3 translation,rotation,scale,pivot; }
  [Serializable] public class Track { public int bone; public int[] frames; public Vector3[] translation,rotation,scale; }
  [Serializable] public class Motion { public string name; public float duration; public Track[] tracks; }
  public Bone[] bones; public Motion[] clips; public Mesh[] meshes; public int[] partIds; public Material material;
  public float modelScale=.045f;
  public int idleClip,walkClip=2,fireClip=3,specialClip=4;
 }
}
