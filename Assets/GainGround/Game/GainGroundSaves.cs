using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace GainGround.Game
{
 public sealed partial class GainGroundGame
 {
  CampaignSaveStore saveStore;
  readonly CampaignSave[] saveSlots=new CampaignSave[3];
  readonly string[] slotErrors=new string[3];
  readonly bool[] slotOccupied=new bool[3];
  bool saveBrowserOpen,saveBrowserWriting;
  int saveSlotIndex,overwriteSlot=-1;
  string saveError="";
  bool campaignClassicMode;
  bool ClassicMode=>stage!=null?campaignClassicMode:settings.ClassicMode;
  bool HasSaves=>slotOccupied[0]||slotOccupied[1]||slotOccupied[2];
  string[] TitleMenuLabels
  {
   get
   {
    if(HasSaves)return HideDuoOption?new[]{"Solo","Continue","Menu","Sair"}:new[]{"Solo","Duo","Continue","Menu","Sair"};
    return HideDuoOption?new[]{"Solo","Menu","Sair"}:new[]{"Solo","Duo","Menu","Sair"};
   }
  }
  void RefreshSaves()
  {
   if(saveStore==null)saveStore=new CampaignSaveStore(Path.Combine(Path.GetDirectoryName(LocalSettings.FilePath),"Saves"));
   for(int i=0;i<3;i++)
   {
    saveSlots[i]=null;slotErrors[i]="";
    try{slotOccupied[i]=saveStore.Exists(i);if(slotOccupied[i])saveSlots[i]=saveStore.Read(i);}
    catch(Exception e){slotOccupied[i]=true;slotErrors[i]=e.Message;}
   }
  }
  void AwardPoints(PlayerState player,int points)
  {
   score+=points;
   if(player!=null){player.score+=points;UpdateHighScore();return;}
   for(int i=0;i<players.Count;i++)players[i].score+=points/players.Count+(i<points%players.Count?1:0);
   UpdateHighScore();
  }
  CampaignSave CaptureCampaign()
  {
   if(state!=ScreenState.Cleared&&state!=ScreenState.Victory)throw new InvalidOperationException("Conclua a fase antes de salvar.");
   var save=new CampaignSave{lastCompletedStage=stageIndex,stageId=stage.name,stageLabel=stage.displayName,
    savedAt=DateTime.UtcNow.ToString("o"),playerCount=players.Count,difficulty=campaignDifficulty,classicMode=campaignClassicMode,players=new SavedPlayer[players.Count]};
   for(int i=0;i<players.Count;i++)
   {
    var p=players[i];var heroes=new SavedHero[p.party.Count];
    for(int h=0;h<heroes.Length;h++)heroes[h]=new SavedHero{id=p.party[h].definition.name,lost=p.party[h].lost};
    save.players[i]=new SavedPlayer{score=p.score,heroes=heroes};
   }
   save.Validate();return save;
  }
  Dictionary<string,CharacterDefinition> SaveHeroCatalog()
  {
   var catalog=new Dictionary<string,CharacterDefinition>();
   Action<CharacterDefinition> add=h=>{if(h==null)return;if(catalog.TryGetValue(h.name,out var existing)&&existing!=h)throw new InvalidDataException("ID de heroi duplicado: "+h.name);catalog[h.name]=h;};
   foreach(var list in new[]{allCharacters,startingParty,secondStartingParty})if(list!=null)foreach(var h in list)add(h);
   foreach(var s in stages){add(s.rescue);foreach(var c in s.additionalCaptives)add(c.character);}
   return catalog;
  }
  void RestoreCampaign(CampaignSave save)
  {
   save.Validate();
   if(save.lastCompletedStage>=stages.Length||stages[save.lastCompletedStage].name!=save.stageId)throw new InvalidDataException("A campanha deste save nao corresponde as fases instaladas.");
   var catalog=SaveHeroCatalog();var restored=new List<PlayerState>();bool alive=false;
   for(int i=0;i<save.players.Length;i++)
   {
    var p=new PlayerState{id=i,score=save.players[i].score};
    foreach(var hero in save.players[i].heroes)
    {
     if(!catalog.TryGetValue(hero.id,out var definition))throw new InvalidDataException("Heroi nao encontrado: "+hero.id);
     p.party.Add(new Member{definition=definition,lost=hero.lost});alive|=!hero.lost;
    }
    restored.Add(p);
   }
   bool complete=save.lastCompletedStage==stages.Length-1;
   if(!alive&&!complete)throw new InvalidDataException("O save nao tem herois disponiveis.");
   players.Clear();players.AddRange(restored);playerCount=save.playerCount;campaignDifficulty=save.difficulty;campaignClassicMode=save.classicMode;
   score=0;foreach(var p in players)score+=p.score;
   UpdateHighScore();
   stageIndex=complete?save.lastCompletedStage:save.lastCompletedStage+1;
   pendingRestoredVictory=complete;LoadStage();
  }
  void OpenSaveSlots(bool writing)
  {
   RefreshSaves();saveBrowserOpen=true;saveBrowserWriting=writing;saveSlotIndex=0;overwriteSlot=-1;saveError="";ResetMobileInput();
  }
  void SelectSaveSlot(int slot)
  {
   try
   {
    if(saveBrowserWriting)
    {
     if(saveStore.Exists(slot)&&overwriteSlot!=slot){overwriteSlot=slot;saveError="Confirme a substituicao do save nesta posicao.";return;}
     saveStore.Write(slot,CaptureCampaign(),overwriteSlot==slot);saveBrowserOpen=false;ReturnToMenu();
    }
    else
    {
     var save=saveStore.Read(slot);
     RestoreCampaign(save);
     saveBrowserOpen=false;saveError="";RefreshSaves();
    }
   }
   catch(Exception e)
   {
    if(!saveBrowserWriting){pendingRestoredVictory=false;ReturnToMenu();saveBrowserOpen=true;}
    saveError=e.Message;RefreshSaves();
   }
  }
  void UpdateSaveSlotsInput()
  {
   if(Input.GetKeyDown(KeyCode.Escape)){saveBrowserOpen=false;return;}
   int delta=Input.GetKeyDown(KeyCode.DownArrow)||MenuDown(GameAction.Down)?1:Input.GetKeyDown(KeyCode.UpArrow)||MenuDown(GameAction.Up)?-1:0;
   if(delta!=0){saveSlotIndex=(saveSlotIndex+delta+3)%3;overwriteSlot=-1;saveError="";}
   if(Input.GetKeyDown(KeyCode.Return)||MenuDown(GameAction.Fire))SelectSaveSlot(saveSlotIndex);
  }
  string SaveSlotLabel(int slot)
  {
   var save=saveSlots[slot];string prefix="Posicao "+(slot+1)+": ";
   if(save==null)return prefix+(slotOccupied[slot]?"Arquivo indisponivel — "+slotErrors[slot]:"Vazio");
   string points="";for(int i=0;i<save.players.Length;i++)points+="  P"+(i+1)+": "+save.players[i].score;
   return prefix+save.stageLabel+" concluida\n"+save.playerCount+" jogador(es) | "+save.difficulty+(save.classicMode?" | Classic":"")+points;
  }
  void DrawSaveSlots(Vector2 size,bool mobile)
  {
   var button=mobile?mobileButton:new GUIStyle(IronButtonStyle){fontSize=17,wordWrap=true};
   var label=GoldMenuText(mobile?mobileLabel:labelStyle);
   DrawSelectionPanel(new Rect(0,0,size.x,size.y));
   float width=Mathf.Min(650,size.x-40),left=(size.x-width)/2;
   GUILayout.BeginArea(new Rect(left,20,width,size.y-40));
   MobileSafeSaveHeading(saveBrowserWriting?"Salvar e voltar para o menu":"Continuar — escolha um save",SelectionHeading);
   saveScroll=GUILayout.BeginScrollView(saveScroll);
   for(int i=0;i<3;i++)
   {
    bool enabled=GUI.enabled;GUI.enabled=saveBrowserWriting||saveSlots[i]!=null;
    string text=(saveSlotIndex==i?"> ":"")+SaveSlotLabel(i)+(overwriteSlot==i?"\nSUBSTITUIR ESTE SAVE":"");
    if(IronLayoutButton(text,button,GUILayout.MinHeight(82))){saveSlotIndex=i;SelectSaveSlot(i);}
    GUI.enabled=enabled;
   }
   if(!string.IsNullOrEmpty(saveError))GUILayout.Label(saveError,label);
   if(IronLayoutButton("Voltar",button,GUILayout.MinHeight(48))){saveBrowserOpen=false;overwriteSlot=-1;}
   GUILayout.EndScrollView();GUILayout.EndArea();
  }
  Vector2 saveScroll;
  bool pendingRestoredVictory;
  void FinishRestoredStage()
  {
   if(!pendingRestoredVictory)return;
   pendingRestoredVictory=false;state=ScreenState.Victory;message="Campanha concluida.";BeginVictoryMusicDownload();
  }
  void MobileSafeSaveHeading(string text,GUIStyle style){GUILayout.Label(text,style,GUILayout.MinHeight(48));}
 }
}
