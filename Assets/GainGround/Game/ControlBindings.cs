using System;
using UnityEngine;
namespace GainGround.Game
{
 public enum GameAction { Up, Down, Left, Right, Fire, Special, Dash, Switch, Pause }
 [Serializable] public sealed class ActionBinding
 {
  public KeyCode keyboard;
  public int joystick=1, button=-1, axis=-1, direction=1;
  public ActionBinding Copy() => (ActionBinding)MemberwiseClone();
 }
 [Serializable] public sealed class PlayerBindings
 {
  public ActionBinding[] actions;
  public static PlayerBindings Defaults(int player)
  {
   var keys=player==0?new[]{KeyCode.W,KeyCode.S,KeyCode.A,KeyCode.D,KeyCode.Z,KeyCode.X,KeyCode.C,KeyCode.Tab,KeyCode.P}:
    new[]{KeyCode.UpArrow,KeyCode.DownArrow,KeyCode.LeftArrow,KeyCode.RightArrow,KeyCode.J,KeyCode.K,KeyCode.L,KeyCode.U,KeyCode.RightControl};
   var result=new PlayerBindings{actions=new ActionBinding[9]};
   for(int i=0;i<9;i++) result.actions[i]=new ActionBinding{keyboard=keys[i],joystick=player+1,axis=i<4?(i<2?1:0):-1,direction=i==0||i==2?-1:1,button=i>=4?i-4:-1};
   return result;
  }
  public PlayerBindings Copy()
  {
   var copy=new PlayerBindings{actions=new ActionBinding[actions.Length]};
   for(int i=0;i<actions.Length;i++)copy.actions[i]=actions[i].Copy();return copy;
  }
  public void Validate(int player)
  {
   var defaults=Defaults(player);
   if(actions==null||actions.Length!=9){actions=defaults.actions;return;}
   for(int i=0;i<9;i++)
   {
    if(actions[i]==null)actions[i]=defaults.actions[i];
    var b=actions[i];
    if(!Enum.IsDefined(typeof(KeyCode),b.keyboard)||(int)b.keyboard>=(int)KeyCode.Mouse0)b.keyboard=defaults.actions[i].keyboard;
    b.joystick=Mathf.Clamp(b.joystick,1,8);b.button=Mathf.Clamp(b.button,-1,19);b.axis=Mathf.Clamp(b.axis,-1,9);b.direction=b.direction<0?-1:1;
    if(b.button>=0)b.axis=-1;
   }
  }
 }
}
