using System;
using UnityEngine;
using UnityEngine.InputSystem;
namespace GainGround.Game
{
 public enum GameAction { Up, Down, Left, Right, Fire, Special, Dash, Switch, Pause }
 [Serializable] public sealed class ActionBinding
 {
  public string keyboard;
  public int joystick=1, button=-1, axis=-1, direction=1;
  public ActionBinding Copy() => (ActionBinding)MemberwiseClone();
 }
 [Serializable] public sealed class PlayerBindings
 {
  public ActionBinding[] actions;
  public static PlayerBindings Defaults(int player)
  {
   var keys=player==0?new[]{"w","s","a","d","q","e","f","tab","p"}:
    new[]{"upArrow","downArrow","leftArrow","rightArrow","j","k","l","u","rightCtrl"};
   var result=new PlayerBindings{actions=new ActionBinding[9]};
   for(int i=0;i<9;i++) result.actions[i]=new ActionBinding{keyboard="<Keyboard>/"+keys[i],joystick=player+1,axis=i<4?(i<2?1:0):-1,direction=i==0||i==2?-1:1,button=i>=4?i-4:-1};
   return result;
  }
  public PlayerBindings Copy()
  {
   var copy=new PlayerBindings{actions=new ActionBinding[actions.Length]};
   for(int i=0;i<actions.Length;i++)copy.actions[i]=actions[i].Copy();return copy;
  }
  internal static string LegacyKeyPath(int code,string fallback)
  {
   if(code>=(int)KeyCode.A&&code<=(int)KeyCode.Z)return "<Keyboard>/"+char.ToLowerInvariant(((KeyCode)code).ToString()[0])+((KeyCode)code).ToString().Substring(1);
   switch((KeyCode)code){case KeyCode.UpArrow:return "<Keyboard>/upArrow";case KeyCode.DownArrow:return "<Keyboard>/downArrow";case KeyCode.LeftArrow:return "<Keyboard>/leftArrow";case KeyCode.RightArrow:return "<Keyboard>/rightArrow";case KeyCode.Tab:return "<Keyboard>/tab";case KeyCode.RightControl:return "<Keyboard>/rightCtrl";case KeyCode.LeftControl:return "<Keyboard>/leftCtrl";case KeyCode.Return:return "<Keyboard>/enter";case KeyCode.Escape:return "<Keyboard>/escape";case KeyCode.Space:return "<Keyboard>/space";case KeyCode.Backspace:return "<Keyboard>/backspace";}
   if(code>=(int)KeyCode.Alpha0&&code<=(int)KeyCode.Alpha9)return "<Keyboard>/"+(char)(code-((int)KeyCode.Alpha0-48));
   if(code>=(int)KeyCode.Keypad0&&code<=(int)KeyCode.Keypad9)return "<Keyboard>/numpad"+(code-(int)KeyCode.Keypad0);
   if(code>=(int)KeyCode.F1&&code<=(int)KeyCode.F12)return "<Keyboard>/f"+(code-(int)KeyCode.F1+1);
   string name=((KeyCode)code).ToString();
   if(Enum.TryParse(name,true,out Key key)&&key!=Key.None)return "<Keyboard>/"+char.ToLowerInvariant(key.ToString()[0])+key.ToString().Substring(1);
   return fallback;
  }
  public void Validate(int player)
  {
   var defaults=Defaults(player);
   if(actions==null||actions.Length!=9){actions=defaults.actions;return;}
   for(int i=0;i<9;i++)
   {
    if(actions[i]==null)actions[i]=defaults.actions[i];
    var b=actions[i];
    if(string.IsNullOrWhiteSpace(b.keyboard))b.keyboard=defaults.actions[i].keyboard;
    else if(int.TryParse(b.keyboard,out int legacyKey))b.keyboard=LegacyKeyPath(legacyKey,defaults.actions[i].keyboard);
    else if(!b.keyboard.StartsWith("<Keyboard>/")){try{b.keyboard=LegacyKeyPath((int)Enum.Parse(typeof(KeyCode),b.keyboard,true),defaults.actions[i].keyboard);}catch{b.keyboard=defaults.actions[i].keyboard;}}
    b.joystick=Mathf.Clamp(b.joystick,1,8);b.button=Mathf.Clamp(b.button,-1,19);b.axis=Mathf.Clamp(b.axis,-1,9);b.direction=b.direction<0?-1:1;
    if(b.button>=0)b.axis=-1;
   }
  }
 }
}
