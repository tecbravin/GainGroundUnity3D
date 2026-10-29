using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
  static string KeyboardPathKey(string path)=>string.IsNullOrEmpty(path)?string.Empty:(path.StartsWith("<Keyboard>/")?path.Substring("<Keyboard>/".Length):path);
  static bool TryGetKey(string path,out Key key) { key=Key.None; if(Keyboard.current==null||string.IsNullOrEmpty(path))return false; try { if(Enum.TryParse(KeyboardPathKey(path),true,out key)&&key!=Key.None)return true; } catch {} return false; }
  static bool TryGetLegacyKey(string path,out KeyCode key)
  {
   key=KeyCode.None;var name=KeyboardPathKey(path);if(string.IsNullOrEmpty(name))return false;
   if(string.Equals(name,"Enter",StringComparison.OrdinalIgnoreCase))name="Return";
   return Enum.TryParse(name,true,out key)&&key!=KeyCode.None;
  }
  static bool KeyHeld(string path)=>Keyboard.current!=null?(TryGetKey(path,out var key)&&Keyboard.current[key].isPressed):(TryGetLegacyKey(path,out var legacyKey)&&Input.GetKey(legacyKey));
  static bool KeyDown(string path)=>Keyboard.current!=null?(TryGetKey(path,out var key)&&Keyboard.current[key].wasPressedThisFrame):(TryGetLegacyKey(path,out var legacyKey)&&Input.GetKeyDown(legacyKey));
  static bool AnyKeyHeld=>Keyboard.current!=null?Keyboard.current.anyKey.isPressed:Input.anyKey;
  static bool AnyKeyDown=>Keyboard.current!=null?Keyboard.current.anyKey.wasPressedThisFrame:Input.anyKeyDown;
  internal static int TouchCount=>Touchscreen.current==null?0:Touchscreen.current.touches.Count(t=>t.press.isPressed);
  internal static bool TouchPressed=>Touchscreen.current!=null?Touchscreen.current.touches.Any(t=>t.press.wasPressedThisFrame):Input.touchCount>0;
  static ButtonControl MouseButton(int button)
  {
   if(Mouse.current==null)return null;
   return button switch {0=>Mouse.current.leftButton,1=>Mouse.current.rightButton,2=>Mouse.current.middleButton,_=>null};
  }
  internal static bool MouseDown(int button)=>Mouse.current!=null?MouseButton(button)?.isPressed==true:Input.GetMouseButton(button);
  internal static bool MousePressed(int button)=>Mouse.current!=null?MouseButton(button)?.wasPressedThisFrame==true:Input.GetMouseButtonDown(button);
  static readonly List<RaycastResult> mouseUiHits=new List<RaycastResult>(16);
  static bool MouseOverInteractiveUI()
  {
   var eventSystem=EventSystem.current;if(Mouse.current==null||eventSystem==null)return false;
   var pointer=new PointerEventData(eventSystem){position=Mouse.current.position.ReadValue()};mouseUiHits.Clear();eventSystem.RaycastAll(pointer,mouseUiHits);
   foreach(var hit in mouseUiHits)for(var node=hit.gameObject.transform;node!=null;node=node.parent)
    if(node.GetComponent<Selectable>()!=null||node.GetComponent<IPointerDownHandler>()!=null||node.GetComponent<IPointerClickHandler>()!=null)return true;
   return false;
  }
  static bool IsKeyPressed(Key key)=>Keyboard.current!=null?Keyboard.current[key].wasPressedThisFrame:(TryGetLegacyKey(key.ToString(),out var legacyKey)&&Input.GetKeyDown(legacyKey));
  static bool GamepadButtonHeld(int device,int button)
  {
   if(Gamepad.all.Count<device||device<1)return false;var pad=Gamepad.all[device-1];
   switch(button){case 0:return pad.buttonSouth.isPressed;case 1:return pad.buttonEast.isPressed;case 2:return pad.buttonWest.isPressed;case 3:return pad.buttonNorth.isPressed;case 4:return pad.leftShoulder.isPressed;case 5:return pad.rightShoulder.isPressed;case 6:return pad.leftTrigger.IsPressed();case 7:return pad.rightTrigger.IsPressed();case 8:return pad.selectButton.isPressed;case 9:return pad.startButton.isPressed;case 10:return pad.leftStickButton.isPressed;case 11:return pad.rightStickButton.isPressed;case 12:return pad.dpad.up.isPressed;case 13:return pad.dpad.down.isPressed;case 14:return pad.dpad.left.isPressed;case 15:return pad.dpad.right.isPressed;default:return false;}
  }
  static bool GamepadButtonPressed(Gamepad pad,int button)=>button switch {0=>pad.buttonSouth.wasPressedThisFrame,1=>pad.buttonEast.wasPressedThisFrame,2=>pad.buttonWest.wasPressedThisFrame,3=>pad.buttonNorth.wasPressedThisFrame,4=>pad.leftShoulder.wasPressedThisFrame,5=>pad.rightShoulder.wasPressedThisFrame,6=>pad.leftTrigger.wasPressedThisFrame,7=>pad.rightTrigger.wasPressedThisFrame,8=>pad.selectButton.wasPressedThisFrame,9=>pad.startButton.wasPressedThisFrame,10=>pad.leftStickButton.wasPressedThisFrame,11=>pad.rightStickButton.wasPressedThisFrame,12=>pad.dpad.up.wasPressedThisFrame,13=>pad.dpad.down.wasPressedThisFrame,14=>pad.dpad.left.wasPressedThisFrame,15=>pad.dpad.right.wasPressedThisFrame,_=>false};
  static float GamepadAxis(int device,int axis)
  {
   if(Gamepad.all.Count<device||device<1)return 0;var pad=Gamepad.all[device-1];
   switch(axis){case 0:return pad.leftStick.x.ReadValue();case 1:return pad.leftStick.y.ReadValue();case 2:return pad.rightStick.x.ReadValue();case 3:return pad.rightStick.y.ReadValue();case 4:return pad.leftTrigger.ReadValue();case 5:return pad.rightTrigger.ReadValue();default:return 0;}
  }
  bool ActionHeld(int player,GameAction action)=>controlHeld[player,(int)action];
  bool ActionDown(int player,GameAction action)=>controlDown[player,(int)action];
  bool MenuDown(GameAction action)=>ActionDown(0,action)||ActionDown(1,action);
  void PollBindings()
  {
   EnsureInputSystemEventSystem(FindAnyObjectByType<EventSystem>());
   for(int p=0;p<2;p++)for(int a=0;a<9;a++)
   {
    var b=Bindings(p).actions[a];
    bool held=!string.IsNullOrEmpty(b.keyboard)&&KeyHeld(b.keyboard);
    if(b.button>=0)held|=GamepadButtonHeld(b.joystick,b.button);
    if(b.axis>=0)held|=GamepadAxis(b.joystick,b.axis)*b.direction>.55f;
    controlDown[p,a]=held&&!controlHeld[p,a];controlHeld[p,a]=held;
   }
  }
  PlayerInput ReadMappedControls(int id)
  {
   var input=new PlayerInput {
    movement=new Vector2((ActionHeld(id,GameAction.Right)?1:0)-(ActionHeld(id,GameAction.Left)?1:0),(ActionHeld(id,GameAction.Up)?1:0)-(ActionHeld(id,GameAction.Down)?1:0)),
    fire=ActionHeld(id,GameAction.Fire),special=ActionHeld(id,GameAction.Special),dash=ActionDown(id,GameAction.Dash),switchMember=ActionDown(id,GameAction.Switch)
   };
   if(id==0 && TouchCount==0 && !players.Exists(p=>p.awaitingSelection) && !MouseOverInteractiveUI())
   {
    input.fire|=MouseDown(0);
    input.special|=MouseDown(1);
    input.dash|=MousePressed(2);
   }
   return input;
  }
  void BeginBindingCapture(int action,bool joystick)
  {
   captureAction=action;captureJoystick=joystick;captureFrame=Time.frameCount;captureReady=false;
   for(int j=0;j<8;j++)for(int a=0;a<10;a++)axisBaseline[j,a]=GamepadAxis(j+1,a);
  }
  void UpdateBindingCapture()
  {
   if(IsKeyPressed(Key.Escape)){if(captureAction>=0)captureAction=-1;else bindingsOpen=false;return;}
   if(captureAction<0||Time.frameCount<=captureFrame)return;
   if(!captureReady){if(!AnyKeyHeld&&TouchCount==0)captureReady=true;return;}
   var b=Bindings(bindingPlayer,true).actions[captureAction];
   if(!captureJoystick)
   {
    if(Keyboard.current!=null)
    {
     foreach(var key in Keyboard.current.allKeys)
      if(key.wasPressedThisFrame&&key.keyCode!=Key.Escape&&key.keyCode!=Key.None)
      {b.keyboard="<Keyboard>/"+key.name;captureAction=-1;return;}
    }
    else
    {
     foreach(KeyCode key in Enum.GetValues(typeof(KeyCode)))
      if(key!=KeyCode.None&&key!=KeyCode.Escape&&Input.GetKeyDown(key))
      {b.keyboard="<Keyboard>/"+(key==KeyCode.Return?"Enter":key.ToString());captureAction=-1;return;}
    }
   }
   else
   {
    for(int j=1;j<=Gamepad.all.Count&&j<=8;j++)
    {
     var pad=Gamepad.all[j-1];
     for(int n=0;n<16;n++)if(GamepadButtonPressed(pad,n))
     {b.joystick=j;b.button=n;b.axis=-1;captureAction=-1;return;}
     for(int a=0;a<6;a++)
     {
      float value=GamepadAxis(j,a);
      if(Mathf.Abs(value)>.65f&&Mathf.Abs(value-axisBaseline[j-1,a])>.65f)
      {b.joystick=j;b.axis=a;b.button=-1;b.direction=value<0?-1:1;captureAction=-1;return;}
     }
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
    if(IronLayoutButton("Teclado: "+(TryGetKey(b.keyboard,out var mappedKey)&&Keyboard.current!=null?Keyboard.current[mappedKey].displayName:"nenhum"),GUILayout.Height(44)))BeginBindingCapture(a,false);
    string joy=b.button>=0?"botão "+b.button:b.axis>=0?"eixo "+(b.axis+1)+(b.direction<0?" -":" +"):"nenhum";
    if(IronLayoutButton("Joy "+b.joystick+": "+joy,GUILayout.Height(44)))BeginBindingCapture(a,true);
    if(IronLayoutButton("Limpar",GUILayout.Width(60),GUILayout.Height(44))){b.keyboard="";b.button=-1;b.axis=-1;}
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
