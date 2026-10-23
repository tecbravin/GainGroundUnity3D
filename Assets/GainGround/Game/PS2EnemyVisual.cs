using UnityEngine;
namespace GainGround.Game
{
 public sealed class PS2EnemyVisual : MonoBehaviour
 {
  public Transform rig;
  public PS2CharacterModel[] parts;
  public bool turnWithTarget=true;
  public int walkClip=1, attackClip=2;
  [Header("Generated enemy effects")]
  public Sprite projectileSprite;
  public Sprite[] projectileFrames;
  [Min(1)] public float projectileFramesPerSecond=12;
  public Vector2 projectileSize=new Vector2(.16f,.16f);
  public Color projectileTint=Color.white;
  public Sprite muzzleFlashSprite;
  public Sprite impactSprite;
  public Vector2 effectSize=new Vector2(.35f,.35f);
  [Header("Enemy sounds (editable on this prefab)")]
  [Tooltip("Played once per projectile volley or successful contact hit.")]
  public AudioClip attackSound;
  [Tooltip("Played when this enemy takes damage.")]
  public AudioClip hitSound;
  [Tooltip("Played when this enemy is defeated.")]
  public AudioClip deathSound;
  SpriteRenderer anchor;
  float frame,attackTime;
  public static PS2EnemyVisual Bind(SpriteRenderer anchor,EnemySpawn spawn)
  {
   if(spawn.modelPrefab==null)return null;
   var view=Instantiate(spawn.modelPrefab,anchor.transform);
   view.anchor=anchor;anchor.forceRenderingOff=true;
   var scale=anchor.transform.lossyScale;
   float height=Mathf.Max(.5f,spawn.visualSize.y);
   view.transform.localScale=new Vector3(height/scale.x,height/scale.y,height/scale.z);
   view.transform.localPosition=new Vector3(0,-height*.4f/scale.y,-.4f/scale.z);
   view.Tick(0,Vector2.down,false);return view;
  }
  public void Attack(){attackTime=.45f;frame=0;}
  public void Tick(float dt,Vector2 facing,bool moving)
  {
   frame+=dt*60;attackTime=Mathf.Max(0,attackTime-dt);
   float yaw=turnWithTarget?Mathf.Atan2(facing.x,-facing.y)*Mathf.Rad2Deg:0;
   rig.localRotation=Quaternion.Euler(35,0,0)*Quaternion.Euler(0,yaw,0);
   foreach(var part in parts)part.SampleEnemy(attackTime>0?attackClip:moving?walkClip:0,frame);
   Sync();
  }
  public void Sync()
  {
   if(anchor==null)return;
   foreach(var part in parts)foreach(var renderer in part.renderers)
   {renderer.enabled=anchor.enabled&&anchor.color.a>=.5f;renderer.sortingOrder=anchor.sortingOrder;}
  }
  void LateUpdate(){Sync();}
 }
}

