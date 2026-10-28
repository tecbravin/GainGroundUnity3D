using System;
using UnityEngine;
namespace GainGround.Game
{
 public sealed partial class GainGroundGame
 {
  readonly bool[,] controlHeld=new bool[2,9], controlDown=new bool[2,9];
  bool bindingsOpen, captureJoystick, captureReady;
  int bindingPlayer, captureAction=-1, captureFrame;
  Vector2 bindingsScroll;
  readonly float[,] axisBaseline=new float[8,10];
  static readonly string[] ActionNames={"Cima","Baixo","Esquerda","Direita","Tiro / Confirmar","Especial","Dash","Trocar personagem","Pausa"};
  PlayerBindings Bindings(int player,bool draft=false) => player==0?(draft?draftSettings:settings).Player1:(draft?draftSettings:settings).Player2;
  static string AxisName(int device,int axis)=>"GG_J"+device+"_A"+axis;
  static KeyCode JoyKey(int device,int button)=>(KeyCode)((int)KeyCode.Joystick1Button0+(device-1)*20+button);
  bool ActionHeld(int player,GameAction action)=>controlHeld[player,(int)action];
  bool ActionDown(int player,GameAction action)=>controlDown[player,(int)action];
  bool MenuDown(GameAction action)=>ActionDown(0,action)||ActionDown(1,action);
  void PollBindings()
  {
   for(int p=0;p<2;p++)for(int a=0;a<9;a++)
   {
    var b=Bindings(p).actions[a];
    bool held=b.keyboard!=KeyCode.None&&Input.GetKey(b.keyboard);
    if(b.button>=0)held|=Input.GetKey(JoyKey(b.joystick,b.button));
    if(b.axis>=0)held|=Input.GetAxisRaw(AxisName(b.joystick,b.axis))*b.direction>.55f;
    controlDown[p,a]=held&&!controlHeld[p,a];controlHeld[p,a]=held;
   }
  }
  PlayerInput ReadMappedControls(int id)
  {
   var input=new PlayerInput {
    movement=new Vector2((ActionHeld(id,GameAction.Right)?1:0)-(ActionHeld(id,GameAction.Left)?1:0),(ActionHeld(id,GameAction.Up)?1:0)-(ActionHeld(id,GameAction.Down)?1:0)),
    fire=ActionHeld(id,GameAction.Fire),special=ActionHeld(id,GameAction.Special),dash=ActionDown(id,GameAction.Dash),switchMember=ActionDown(id,GameAction.Switch)
   };
   if(id==0 && Input.touchCount==0 && !players.Exists(p=>p.awaitingSelection) &&
      (UnityEngine.EventSystems.EventSystem.current==null ||
       !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
   {
    input.fire|=Input.GetMouseButton(0);
    input.special|=Input.GetMouseButton(1);
    input.dash|=Input.GetMouseButtonDown(2);
   }
   return input;
  }
  void BeginBindingCapture(int action,bool joystick)
  {
   captureAction=action;captureJoystick=joystick;captureFrame=Time.frameCount;captureReady=false;
   for(int j=0;j<8;j++)for(int a=0;a<10;a++)axisBaseline[j,a]=Input.GetAxisRaw(AxisName(j+1,a));
  }
  void UpdateBindingCapture()
  {
   if(Input.GetKeyDown(KeyCode.Escape)){if(captureAction>=0)captureAction=-1;else bindingsOpen=false;return;}
   if(captureAction<0||Time.frameCount<=captureFrame)return;
   if(!captureReady){if(!Input.anyKey&&Input.touchCount==0)captureReady=true;return;}
   var b=Bindings(bindingPlayer,true).actions[captureAction];
   if(!captureJoystick)
   {
    foreach(KeyCode key in Enum.GetValues(typeof(KeyCode)))
     if(key!=KeyCode.None&&key!=KeyCode.Escape&&(int)key<(int)KeyCode.Mouse0&&Input.GetKeyDown(key))
     {b.keyboard=key;captureAction=-1;return;}
   }
   else
   {
    for(int j=1;j<=8;j++)for(int n=0;n<20;n++)if(Input.GetKeyDown(JoyKey(j,n)))
    {b.joystick=j;b.button=n;b.axis=-1;captureAction=-1;return;}
    for(int j=1;j<=8;j++)for(int a=0;a<10;a++)
    {
     float value=Input.GetAxisRaw(AxisName(j,a));
     if(Mathf.Abs(value)>.65f&&Mathf.Abs(value-axisBaseline[j-1,a])>.65f)
     {b.joystick=j;b.axis=a;b.button=-1;b.direction=value<0?-1:1;captureAction=-1;return;}
    }
   }
  }
  void DrawBindings(Vector2 size)
  {
   DrawSelectionPanel(new Rect(0,0,size.x,size.y));
   GUILayout.BeginArea(new Rect(20,20,size.x-20,size.y-40));
   GUILayout.Label("MAPEAR CONTROLES — P"+(bindingPlayer+1),SelectionHeading);
   bool enabled=GUI.enabled;GUI.enabled=captureAction<0;
   GUILayout.BeginHorizontal();
   if(IronLayoutButton("Jogador 1",GUILayout.Height(40)))bindingPlayer=0;
   if(IronLayoutButton("Jogador 2",GUILayout.Height(40)))bindingPlayer=1;
   GUILayout.EndHorizontal();
   GUI.enabled=enabled;
   GUILayout.Label(captureAction>=0?"Solte os botões e pressione "+(captureJoystick?"um botão ou direção do joystick":"uma tecla")+". Esc: cancelar.":"Cada ação aceita teclado e joystick simultaneamente. P1: tiro = mouse esquerdo, especial = direito, dash = meio.",SelectionHeading);
   if(captureAction>=0&&IronLayoutButton("Cancelar captura",GUILayout.Height(40)))captureAction=-1;
   bindingsScroll=GUILayout.BeginScrollView(bindingsScroll);
   ScrollActiveViewWithMouseWheel(ref bindingsScroll);
   GUILayout.BeginVertical(GUILayout.Width(size.x-40-GUI.skin.verticalScrollbar.fixedWidth));
   for(int a=0;a<9;a++)
   {
    var b=Bindings(bindingPlayer,true).actions[a];
    GUILayout.Label(ActionNames[a],SelectionHeading);
    GUI.enabled=captureAction<0;GUILayout.BeginHorizontal();
    if(IronLayoutButton("Teclado: "+b.keyboard,GUILayout.Height(44)))BeginBindingCapture(a,false);
    string joy=b.button>=0?"botão "+b.button:b.axis>=0?"eixo "+(b.axis+1)+(b.direction<0?" -":" +"):"nenhum";
    if(IronLayoutButton("Joy "+b.joystick+": "+joy,GUILayout.Height(44)))BeginBindingCapture(a,true);
    if(IronLayoutButton("Limpar",GUILayout.Width(60),GUILayout.Height(44))){b.keyboard=KeyCode.None;b.button=-1;b.axis=-1;}
    GUILayout.EndHorizontal();GUI.enabled=enabled;
   }
   GUI.enabled=captureAction<0;
   if(IronLayoutButton("Restaurar padrões de P"+(bindingPlayer+1),GUILayout.Height(44)))
   {if(bindingPlayer==0)draftSettings.Player1=PlayerBindings.Defaults(0);else draftSettings.Player2=PlayerBindings.Defaults(1);}
   if(IronLayoutButton("Voltar às opções",GUILayout.Height(44)))bindingsOpen=false;
   GUILayout.Label("Use Salvar nas opções para manter as alterações.",SelectionHeading);
   GUI.enabled=enabled;GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndArea();
  }
 }
}
