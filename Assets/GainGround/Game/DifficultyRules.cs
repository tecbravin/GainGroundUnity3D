using System.Collections.Generic;
using UnityEngine;
namespace GainGround.Game
{
 public static class DifficultyRules
 {
  public static float ScoreMultiplier(GameDifficulty difficulty)
  {
   return difficulty==GameDifficulty.Easy?.7f:difficulty==GameDifficulty.Hard?1.5f:1f;
  }
  public static int ScorePoints(GameDifficulty difficulty,int basePoints)
  {
   return Mathf.RoundToInt(basePoints*ScoreMultiplier(difficulty));
  }
  public static int EnemyHealth(GameDifficulty difficulty,int health,bool boss)
  {
   if(!boss||difficulty==GameDifficulty.Normal)return health;
   return Mathf.Max(1,Mathf.RoundToInt(health*(difficulty==GameDifficulty.Easy?.8f:1.2f)));
  }
  public static CharacterDefinition[] StartingRoster(GameDifficulty difficulty,CharacterDefinition[] starting,CharacterDefinition[] available)
  {
   if(difficulty==GameDifficulty.Normal)return starting;
   int target=difficulty==GameDifficulty.Easy?5:1;
   var result=new List<CharacterDefinition>();
   foreach(var source in new[]{starting,available})
   {
    if(source==null)continue;
    foreach(var hero in source)
    {
     if(hero!=null&&!result.Contains(hero))result.Add(hero);
     if(result.Count==target)return result.ToArray();
    }
   }
   return result.ToArray();
  }
 }
}
