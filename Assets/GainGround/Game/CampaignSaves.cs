using System;
using System.IO;
using UnityEngine;
namespace GainGround.Game
{
 [Serializable] public sealed class SavedHero { public string id; public bool lost; }
 [Serializable] public sealed class SavedPlayer { public int score; public SavedHero[] heroes; }
 [Serializable] public sealed class CampaignSave
 {
  public int version=1;
  public int lastCompletedStage=-1;
  public string stageId,stageLabel,savedAt;
  public int playerCount;
  public GameDifficulty difficulty;
  public bool classicMode;
  public SavedPlayer[] players;
  public void Validate()
  {
   if(version!=1)throw new InvalidDataException("Versao de save nao suportada.");
   if(lastCompletedStage<0||string.IsNullOrEmpty(stageId)||playerCount<1||playerCount>2||players==null||players.Length!=playerCount||!Enum.IsDefined(typeof(GameDifficulty),difficulty))
    throw new InvalidDataException("Save incompleto ou invalido.");
   long total=0;
   foreach(var p in players)
   {
    if(p==null||p.score<0||p.heroes==null||p.heroes.Length==0||p.heroes.Length>256)throw new InvalidDataException("Equipe invalida no save.");
    total+=p.score;
    foreach(var h in p.heroes)if(h==null||string.IsNullOrEmpty(h.id))throw new InvalidDataException("Heroi invalido no save.");
   }
   if(total>int.MaxValue)throw new InvalidDataException("Pontuacao invalida.");
  }
 }
 public sealed class CampaignSaveStore
 {
  public const int SlotCount=3;
  public readonly string directory;
  public CampaignSaveStore(string directory){this.directory=directory;}
  public string SlotPath(int slot)
  {if(slot<0||slot>=SlotCount)throw new ArgumentOutOfRangeException(nameof(slot));return Path.Combine(directory,"save"+(slot+1)+".json");}
  public bool Exists(int slot)=>File.Exists(SlotPath(slot));
  public CampaignSave Read(int slot)
  {
   var result=JsonUtility.FromJson<CampaignSave>(File.ReadAllText(SlotPath(slot)));
   if(result==null)throw new InvalidDataException("Save vazio.");result.Validate();return result;
  }
  public void Write(int slot,CampaignSave save,bool overwrite)
  {
   save.Validate();string path=SlotPath(slot);Directory.CreateDirectory(directory);
   if(File.Exists(path)&&!overwrite)throw new IOException("Esta posicao ja possui um save.");
   string temporary=path+".tmp";
   File.WriteAllText(temporary,JsonUtility.ToJson(save,true));
   if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
  }
  public void Consume(int slot){File.Delete(SlotPath(slot));}
 }
}
