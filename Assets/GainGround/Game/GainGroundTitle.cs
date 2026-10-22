using UnityEngine;
namespace GainGround.Game
{
 public sealed partial class GainGroundGame
 {
  public const float TitleIntroDuration=6.5f;
  float titleElapsed;
  bool titleMenuVisible,titleInputBlocked;
  int titleRevealFrame=-1;
  Texture2D titleLeft,titleRight;
  Texture2D[] titleLogos,titleNoise;
  public bool TitleMenuVisible=>titleMenuVisible;
  bool CanUseTitleMenu=>titleMenuVisible&&!titleInputBlocked&&Time.frameCount>titleRevealFrame;
  void BeginTitleIntro()
  {
   titleElapsed=0;titleMenuVisible=false;titleInputBlocked=false;titleRevealFrame=-1;
   LoadTitleArtwork();
  }
  void LoadTitleArtwork()
  {
   if(titleLogos!=null)return;
   titleLeft=Resources.Load<Texture2D>("PS2Title/TITLE_L");titleRight=Resources.Load<Texture2D>("PS2Title/TITLE_R");
   titleLogos=new Texture2D[5];titleNoise=new Texture2D[4];
   for(int i=0;i<5;i++)titleLogos[i]=Resources.Load<Texture2D>("PS2Title/LOGO_0"+(i+1));
   for(int i=0;i<4;i++)titleNoise[i]=Resources.Load<Texture2D>("PS2Title/NOISE"+(i+1));
  }
  void AdvanceTitleIntro(float dt,bool pressed,bool held)
  {
   titleElapsed+=Mathf.Max(0,dt);
   if(!titleMenuVisible&&(pressed||titleElapsed>=TitleIntroDuration))
   {titleMenuVisible=true;titleInputBlocked=pressed||held;titleRevealFrame=Time.frameCount;}
   else if(titleInputBlocked&&!held)titleInputBlocked=false;
  }
  bool UpdateTitleIntro()
  {
   if(state!=ScreenState.Title)return false;
   bool pressed=Input.anyKeyDown;
   for(int i=0;i<Input.touchCount;i++)pressed|=Input.GetTouch(i).phase==TouchPhase.Began;
   bool wasVisible=titleMenuVisible;
   AdvanceTitleIntro(Time.unscaledDeltaTime,pressed,Input.anyKey||Input.touchCount>0);
   return !wasVisible||!CanUseTitleMenu;
  }
  void ShowTitleMenuImmediately()
  {titleMenuVisible=true;titleElapsed=TitleIntroDuration;titleRevealFrame=Time.frameCount;titleInputBlocked=Input.anyKey||Input.touchCount>0;}
  void DrawTitleArtwork(Rect area)
  {
   LoadTitleArtwork();var color=GUI.color;
   GUI.color=Color.black;GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=Color.white;
   float titleMotion=titleMenuVisible?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(titleElapsed/4.8f));
   float halfWidth=area.width*.5f;
   var leftFinal=new Rect(area.x,area.y,halfWidth,area.height);
   var rightFinal=new Rect(area.center.x,area.y,halfWidth,area.height);
   // Slide both halves horizontally from the left and right edges at center height.
   const float edgeOffset=.8f;
   var leftStart=new Rect(area.x-halfWidth*edgeOffset,area.y,halfWidth,area.height);
   var rightStart=new Rect(area.center.x+halfWidth*edgeOffset,area.y,halfWidth,area.height);
   if(titleLeft!=null)GUI.DrawTexture(LerpRect(leftStart,leftFinal,titleMotion),titleLeft,ScaleMode.StretchToFill);
   if(titleRight!=null)GUI.DrawTexture(LerpRect(rightStart,rightFinal,titleMotion),titleRight,ScaleMode.StretchToFill);
   // Reveal the PS2 logo only after both title halves have finished converging.
   float progress=titleMenuVisible?1:Mathf.Clamp01((titleElapsed-4.8f)/(TitleIntroDuration-4.8f));
   int logo=progress<.3f?4:progress<.55f?3:progress<.7f?2:progress<.85f?1:0;
   GUI.color=new Color(1,1,1,Mathf.SmoothStep(0,1,progress*3));
   if(titleLogos[logo]!=null)GUI.DrawTexture(new Rect(area.x+area.width/8,area.y,area.width*.75f,area.height*256/448),titleLogos[logo],ScaleMode.StretchToFill);
   var gainWord = new Rect(area.x + area.width*.30f, area.y + area.height*.20f, area.width*.30f, area.height*.09f);
   if(state==ScreenState.Title && CanUseTitleMenu && Event.current.type==EventType.MouseDown && Event.current.button==0 && gainWord.Contains(Event.current.mousePosition))
   {CountSecretGainClick();Event.current.Use();}
   if(!titleMenuVisible)
   {
    GUI.color=new Color(1,1,1,.18f*(1-progress));
    var noise=titleNoise[Mathf.FloorToInt(titleElapsed*12)%4];
    if(noise!=null)GUI.DrawTexture(area,noise,ScaleMode.StretchToFill);
   }
   GUI.color=color;
  }
  static Rect LerpRect(Rect from,Rect to,float t)
  {return new Rect(Mathf.Lerp(from.x,to.x,t),Mathf.Lerp(from.y,to.y,t),Mathf.Lerp(from.width,to.width,t),Mathf.Lerp(from.height,to.height,t));}
 }
}
