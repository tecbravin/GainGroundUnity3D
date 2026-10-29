using UnityEngine;
namespace GainGround.Game
{
 public sealed class PS2EnemyVisual : MonoBehaviour
 {
  public Transform rig;
  public PS2CharacterModel[] parts;
  public bool turnWithTarget=true;
  [Tooltip("Keeps a fixed emplacement centered on its stage spawn instead of lowering it like a standing actor.")]
  public bool mountedToStage;
  [Tooltip("Optional mesh that keeps its initial rotation and position relative to this enemy while its bones animate.")]
  public Transform fixedRotationPart;
  [Tooltip("Use -1 to take the action clip from the imported PS2 model data.")]
  public int walkClip=-1, attackClip=-1, deathClip=-1;
  [Header("Generated enemy effects")]
  public Sprite projectileSprite;
  [Tooltip("SpriteRenderer prefab for this enemy's projectile; overrides projectileSprite.")]
  public GameObject projectileVisualPrefab;
  [Tooltip("Original PS2 lightning bolt prefabs used by the giant's area strike.")]
  public GameObject[] lightningStrikePrefabs;
  [Tooltip("Original PS2 lightning burst prefabs used at the center of the strike.")]
  public GameObject[] lightningBurstPrefabs;
  public Sprite[] projectileFrames;
  [Min(1)] public float projectileFramesPerSecond=12;
  public Vector2 projectileSize=new Vector2(.16f,.16f);
  public Color projectileTint=Color.white;
  [Tooltip("Optional halo drawn behind this enemy's projectile.")]
  public Sprite projectileGlowSprite;
  public Vector2 projectileGlowSize;
  public Color projectileGlowTint=Color.white;
  [Tooltip("Sprite used by the two homing laser spheres fired by the final computer boss.")]
  public Sprite laserSphereSprite;
  public Sprite muzzleFlashSprite;
  public Sprite impactSprite;
  public GameObject muzzleVisualPrefab;
  public GameObject impactVisualPrefab;
  [Tooltip("Optional original PS2 animation frames for the impact prefab.")]
  public Sprite[] impactFrames;
  [Min(1)] public float impactFramesPerSecond=45;
  public Vector2 effectSize=new Vector2(.35f,.35f);
  [Header("Enemy sounds (editable on this prefab)")]
  [Tooltip("Played once per projectile volley or successful contact hit.")]
  public AudioClip attackSound;
  [Tooltip("Played when this enemy takes damage.")]
  public AudioClip hitSound;
  [Tooltip("Played when this enemy is defeated.")]
  public AudioClip deathSound;
  SpriteRenderer anchor;
  Camera viewCamera;
  float frame,loopFrame,animationPhase,attackTime,nextOffscreenSample,deathTime,deathDuration;
  bool deathPlaying,facingInitialized;
  bool proceduralSlimeMotion;
  Vector3 baseLocalPosition,baseLocalScale;
  Quaternion fixedRotation;
  Vector3 fixedLocalPosition;
  bool fixedRotationCaptured;
  public float AttackDuration=>AttackClipDuration(.45f);
  public static PS2EnemyVisual Bind(SpriteRenderer anchor,EnemySpawn spawn)
  {
   if(spawn.modelPrefab==null)return null;
   var view=Instantiate(spawn.modelPrefab,anchor.transform);
   view.anchor=anchor;view.viewCamera=Camera.main;
   // Stagger idle and locomotion cycles so groups do not move in lockstep.
   view.animationPhase=Mathf.Repeat(Mathf.Abs(spawn.position.x*19.37f+spawn.position.y*31.91f),60f);
   // The final-stage barriers use a 3:1 sprite to show the full blocking wall.
   // Their compact 3D model decorates it but cannot replace its full width.
   anchor.forceRenderingOff=spawn.behavior!=EnemyBehavior.Barrier;
   var scale=anchor.transform.lossyScale;
   float height=Mathf.Max(.5f,spawn.visualSize.y);
   view.transform.localScale=new Vector3(height/scale.x,height/scale.y,height/scale.z);
   view.transform.localPosition=new Vector3(0,view.mountedToStage?0:-height*.4f/scale.y,-.4f/scale.z);
   view.proceduralSlimeMotion=spawn.behavior==EnemyBehavior.Slime||
    spawn.modelPrefab.name.IndexOf("slime",System.StringComparison.OrdinalIgnoreCase)>=0;
   view.baseLocalPosition=view.transform.localPosition;
   view.baseLocalScale=view.transform.localScale;
   view.Tick(0,Vector2.down,false);
   view.CaptureFixedRotation();
   return view;
  }
  public void Attack()
  {
   // Enemy motion durations are stored in source frames at 60 fps. Composite
   // actors can have parts with different motion lengths, so keep the attack
   // pose active until the longest available part has completed its clip.
   attackTime=AttackClipDuration(.45f);
   frame=0;
  }
  int IdleClip(PS2CharacterModel part)
  {
   return part!=null&&part.data!=null&&ValidClip(part,part.data.idleClip)?part.data.idleClip:0;
  }
  int WalkClip(PS2CharacterModel part)
  {
   int clip=walkClip>=0?walkClip:part!=null&&part.data!=null?part.data.walkClip:-1;
   return part!=null&&ValidClip(part,clip)?clip:IdleClip(part);
  }
  int AttackClip(PS2CharacterModel part)
  {
   int clip=attackClip>=0?attackClip:part!=null&&part.data!=null?part.data.fireClip:-1;
   return part!=null&&ValidClip(part,clip)?clip:IdleClip(part);
  }
  static bool ValidClip(PS2CharacterModel part,int clip)
  {
   return part!=null&&part.data!=null&&part.data.clips!=null&&clip>=0&&clip<part.data.clips.Length;
  }
  float AttackClipDuration(float fallback)
  {
   float duration=0;
   foreach(var part in parts)
   {
    int clip=AttackClip(part);
    if(!ValidClip(part,clip))continue;
    duration=Mathf.Max(duration,part.data.clips[clip].duration/60f);
   }
   return duration>0?duration:fallback;
  }
  public bool PlayDeath()
  {
   if(deathClip<0)return false;
   float duration=0;
   foreach(var part in parts)
   {
    if(!ValidClip(part,deathClip))continue;
    duration=Mathf.Max(duration,part.data.clips[deathClip].duration/60f);
   }
   if(duration<=0)return false;
   deathPlaying=true;deathTime=0;deathDuration=duration;frame=0;attackTime=0;
   return true;
  }
  public bool TickDeath(float dt)
  {
   if(!deathPlaying)return true;
   deathTime=Mathf.Min(deathDuration,deathTime+dt);
   float deathFrame=deathTime*60f;
   foreach(var part in parts)
    if(part!=null&&part.data!=null)part.SampleEnemy(deathClip,deathFrame,false);
   return deathTime>=deathDuration;
  }
  public void Tick(float dt,Vector2 facing,bool moving)
  {
   frame+=dt*60;loopFrame+=dt*60;attackTime=Mathf.Max(0,attackTime-dt);
   // Enemy geometry shares the player model's forward axis. Positive yaw faces screen left.
   float yaw=turnWithTarget?Mathf.Atan2(-facing.x,-facing.y)*Mathf.Rad2Deg:0;
   var desiredRotation=Quaternion.Euler(-35,0,0)*Quaternion.Euler(0,yaw,0);
   if(!facingInitialized){rig.localRotation=desiredRotation;facingInitialized=true;}
   else if(dt>0)rig.localRotation=Quaternion.RotateTowards(rig.localRotation,desiredRotation,720f*dt);
   if(proceduralSlimeMotion)
   {
    float wave=Mathf.Sin((loopFrame+animationPhase)*Mathf.PI/(moving ? 18f : 54f));
    float verticalScale=1f+wave*(moving ? .1f : .035f);
    float horizontalScale=1f/Mathf.Sqrt(verticalScale);
    transform.localScale=new Vector3(baseLocalScale.x*horizontalScale,
     baseLocalScale.y*verticalScale,baseLocalScale.z*horizontalScale);
    float hop=moving ? Mathf.Max(0,wave)*baseLocalScale.y*.045f : 0;
    transform.localPosition=baseLocalPosition+Vector3.up*hop;
   }
   bool onscreen=anchor!=null&&anchor.enabled;
   if(onscreen&&viewCamera!=null)
   {
    var viewport=viewCamera.WorldToViewportPoint(anchor.transform.position);
    onscreen=viewport.z>0&&viewport.x>-.1f&&viewport.x<1.1f&&viewport.y>-.1f&&viewport.y<1.1f;
   }
   if(!onscreen&&Time.unscaledTime<nextOffscreenSample)return;
   if(!onscreen)nextOffscreenSample=Time.unscaledTime+.2f;
   foreach(var part in parts)
   {
    int clip=attackTime>0?AttackClip(part):moving?WalkClip(part):IdleClip(part);
    float sampleFrame=attackTime>0?frame:loopFrame+animationPhase;
    part.SampleEnemy(clip,sampleFrame);
   }
  }
  public void Sync()
  {
   if(anchor==null)return;
   foreach(var part in parts)foreach(var renderer in part.renderers)
   {
    bool visible=anchor.enabled&&anchor.color.a>=.5f;
    if(renderer.enabled!=visible)renderer.enabled=visible;
    if(renderer.sortingOrder!=anchor.sortingOrder)renderer.sortingOrder=anchor.sortingOrder;
   }
  }
  void CaptureFixedRotation()
  {
   if(fixedRotationPart==null)return;
   fixedRotation=fixedRotationPart.rotation;
   fixedLocalPosition=transform.InverseTransformPoint(fixedRotationPart.position);
   fixedRotationCaptured=true;
  }
  void LateUpdate()
  {
   Sync();
   if(fixedRotationPart==null)return;
   if(!fixedRotationCaptured)CaptureFixedRotation();
   fixedRotationPart.position=transform.TransformPoint(fixedLocalPosition);
   fixedRotationPart.rotation=fixedRotation;
  }
 }
}

