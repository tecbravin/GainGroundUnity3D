using UnityEngine;
namespace GainGround.Game
{
 public sealed class PS2CharacterModel : MonoBehaviour
 {
  public PS2ModelData data;
  public Transform rig;
  [Tooltip("Inclinação vertical do modelo. Valores negativos mostram o personagem visto de cima.")]
  public float viewPitchDegrees=35;
  public Transform[] bones;
  public Renderer[] renderers;
  float frame,attackRemaining; int currentClip=-1,attackClip;
  public int CurrentClip=>currentClip;
  public float CurrentFrame=>frame;
  public static Quaternion SourceRotation(Vector3 r)=>Quaternion.AngleAxis(r.z*Mathf.Rad2Deg,Vector3.forward)*Quaternion.AngleAxis(r.y*Mathf.Rad2Deg,Vector3.up)*Quaternion.AngleAxis(r.x*Mathf.Rad2Deg,Vector3.right);
  public void Attack(bool special)
  {
   attackClip=special?data.specialClip:data.fireClip;
   attackRemaining=Mathf.Max(.15f,data.clips[attackClip].duration/60f);currentClip=-1;
  }
  public void Tick(float dt,Vector2 facing,bool moving,bool dash,bool visible)
  {
   foreach(var renderer in renderers) renderer.enabled=visible;
   // The source model faces the camera at zero yaw; positive yaw turns it toward screen left.
   float yaw=Mathf.Atan2(-facing.x,-facing.y)*Mathf.Rad2Deg;
   rig.localRotation=Quaternion.Euler(viewPitchDegrees,0,0)*Quaternion.Euler(0,yaw,0);
   int clip=attackRemaining>0?attackClip:moving||dash?data.walkClip:data.idleClip;
   if(clip!=currentClip){currentClip=clip;frame=0;}
   frame+=dt*(dash?100:60);attackRemaining=Mathf.Max(0,attackRemaining-dt);Sample(moving||dash?data.walkClip:-1,moving||dash?frame:0);
   ApplyMotion(1,moving||dash?frame:0);
   if(attackRemaining>0)ApplyMotion(attackClip,frame);
  }
  public void Sample(int clip,float time)
  {
   for(int i=0;i<bones.Length;i++) Apply(i,data.bones[i].translation,data.bones[i].rotation,data.bones[i].scale);
   ApplyMotion(0,0);
   ApplyMotion(1,0);
   ApplyMotion(2,0);
   ApplyMotion(clip,time);
  }
  public void SampleEnemy(int clip,float time)
  {
   for(int i=0;i<bones.Length;i++) Apply(i,data.bones[i].translation,data.bones[i].rotation,data.bones[i].scale);
   ApplyMotion(0,0);ApplyMotion(clip,time);
  }
  void ApplyMotion(int clip,float time)
  {
   if(clip<0||clip>=data.clips.Length)return;
   var motion=data.clips[clip];float at=motion.duration>0?Mathf.Repeat(time,motion.duration):0;
   foreach(var t in motion.tracks)
   {
    var b=data.bones[t.bone];Apply(t.bone,Interpolate(t.translation,t.frames,at,b.translation),Interpolate(t.rotation,t.frames,at,b.rotation),Interpolate(t.scale,t.frames,at,b.scale));
   }
  }
  static Vector3 Interpolate(Vector3[] keys,int[] frames,float at,Vector3 fallback)
  {
   if(keys==null||keys.Length==0)return fallback;
   for(int i=1;i<keys.Length;i++)if(at<=frames[i])return Vector3.Lerp(keys[i-1],keys[i],Mathf.InverseLerp(frames[i-1],frames[i],at));
   return keys[keys.Length-1];
  }
  void Apply(int i,Vector3 t,Vector3 r,Vector3 s)
  {
   var b=data.bones[i];bones[i].localPosition=t+b.pivot-(b.parent>=0?data.bones[b.parent].pivot:Vector3.zero);
   bones[i].localRotation=SourceRotation(r);bones[i].localScale=s;
  }
 }
}

