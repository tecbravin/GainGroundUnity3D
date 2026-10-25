using System;
using System.IO;
using System.Linq;
using GainGround.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CompleteCampaignInstaller
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";
    const string Content = Root + "/Complete";
    [Serializable] public class Point { public float x, y; public Vector2 World => new Vector2((x-96)/16, (112-y)/16); }
    [Serializable] public class Area : Point { public float w,h; public Rect WorldRect => new Rect((x-96)/16,(112-y-h)/16,w/16,h/16); }
    [Serializable] public class Unit : Point { public string kind; public bool elevated; public float delay; }
    [Serializable] public class Captive : Point { public string name; public int players; }
    [Serializable] public class Map { public int round,number; public string title; public Point start; public Area[] walls,exits,hazards; public Unit[] enemies; public Captive[] captives; }
    [Serializable] public class Manifest { public Map[] stages; }

    [MenuItem("Gain Ground/Install complete Mega Drive campaign")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        foreach (var file in Directory.GetFiles(Content,"*.png",SearchOption.AllDirectories))
        {
            var path=file.Replace('\\','/');
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            bool isMap=path.Contains("/Maps/");
            importer.spritePixelsPerUnit=isMap ? MapPixelsPerUnit(file) : 16;
            importer.npotScale=TextureImporterNPOTScale.None;
            if(!isMap)
            {
                importer.filterMode=FilterMode.Point;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
            }
            importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true; importer.SaveAndReimport();
        }
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Content+"/campaign.json"));
        if (manifest.stages.Length!=50) throw new InvalidDataException("Expected all 50 original stages.");
        var stages=manifest.stages.Select(InstallMap).ToArray();
        EditorSceneManager.OpenScene("Assets/GainGround/Editable/Scenes/GainGround.unity");
        var game=UnityEngine.Object.FindObjectOfType<GainGroundGame>();
        game.stages=stages;
        EditorUtility.SetDirty(game);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
        if (AssetDatabase.IsValidFolder(Root + "/Objects/Prefabs"))
            PS2StageObjectInstaller.Install();
        Debug.Log("Installed complete campaign: 50 stages.");
    }
    static StageDefinition InstallMap(Map map)
    {
        string path=$"{Root}/Stage_{map.round}_{map.number}.asset";
        var s=AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
        bool preserve=map.round==1 && map.number<=5 && s!=null;
        if(s==null) { s=ScriptableObject.CreateInstance<StageDefinition>(); AssetDatabase.CreateAsset(s,path); }
        s.background=AssetDatabase.LoadAssetAtPath<Sprite>($"{Content}/Maps/{map.round}-{map.number:00}.png");
        if(!preserve)
        {
            s.displayName=$"{map.round}-{map.number} / {map.title}";
            s.size=new Vector2(12,14); s.start=map.start.World; s.timeLimit=180;
            s.walls=map.walls.Select(a=>a.WorldRect).ToArray();
            s.noExit=map.exits.Length==0;
            s.exit=s.noExit ? new Rect() : map.exits[0].WorldRect;
            s.additionalExits=map.exits.Skip(1).Select(a=>a.WorldRect).ToArray();
            s.enemies=map.enemies.Select(Enemy).ToArray();
            if (map.round == 1 && map.number == 10)
                foreach (var enemy in s.enemies)
                    if (enemy.behavior == EnemyBehavior.Giant && !enemy.boss)
                        enemy.initialShotDelay = enemy.position.y < 0 ? 1f : 2f;
            s.rescue=map.captives.Length==0 ? null : CharacterAssets.Load(map.captives[0].name);
            if(s.rescue!=null) s.rescuePosition=map.captives[0].World;
            s.additionalCaptives=map.captives.Skip(1).Select(c=>new CaptiveSpawn {
                character=CharacterAssets.Load(c.name),position=c.World,minimumPlayers=c.players }).ToArray();
            s.hazards=map.hazards.Select(h=>new HazardSpawn { area=h.WorldRect,
                controllers=map.enemies.Select((e,i)=>new {e,i}).Where(x=>x.e.kind=="generator")
                    .OrderBy(x=>Mathf.Abs(x.e.y-h.y-10)).Take(2).Select(x=>x.i).ToArray() }).ToArray();
            s.stageMusic=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GainGround/Editable/Audio/01_Genesis_Remastered.wav");
            s.victoryMusic=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GainGround/Editable/Audio/06_Victory_Remastered.wav");
        }
        else
        {
            foreach(var enemy in s.enemies)
            {
                var definition=Enemy(new Unit { kind=enemy.melee?"warrior":"archer" });
                enemy.sprite=definition.sprite; enemy.visualSize=definition.visualSize;
            }
        }
        EditorUtility.SetDirty(s);
        return s;
    }
    static int MapPixelsPerUnit(string path)
    {
        using(var stream=File.OpenRead(path))
        using(var reader=new BinaryReader(stream))
        {
            if(stream.Length<24 || reader.ReadUInt64()!=0x0A1A0A0D474E5089UL)
                throw new InvalidDataException("Invalid PNG map: "+path);
            stream.Position=16;
            int width=ReadBigEndianInt(reader);
            int height=ReadBigEndianInt(reader);
            if(width<=0 || width%12!=0 || (long)width*14!=(long)height*12)
                throw new InvalidDataException("Unexpected map dimensions: "+path);
            return width/12;
        }
    }
    static int ReadBigEndianInt(BinaryReader reader)
    {
        var bytes=reader.ReadBytes(4);
        if(bytes.Length!=4) throw new EndOfStreamException();
        return (bytes[0]<<24)|(bytes[1]<<16)|(bytes[2]<<8)|bytes[3];
    }
    static EnemySpawn Enemy(Unit e)
    {
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>($"{Content}/Sprites/{e.kind}.png");
        var s=new EnemySpawn {position=e.World,elevated=e.elevated,spawnDelay=e.delay,
            sprite=sprite,visualSize=sprite.rect.size/16,health=1,sentry=true,speed=0,shotInterval=2.8f};
        switch(e.kind)
        {
            case "warrior": case "axeman": case "ninja": case "brawler": case "runner":
                s.behavior=EnemyBehavior.Chaser; s.melee=true; s.sentry=false; s.speed=e.kind=="runner"?1.8f:1.1f; break;
            case "slime": s.behavior=EnemyBehavior.Slime; s.melee=true;s.speed=.65f;s.health=2;break;
            case "wizard": s.behavior=EnemyBehavior.Wizard;break;
            case "worm": s.behavior=EnemyBehavior.Burrow;s.speed=.6f;break;
            case "orb": s.behavior=EnemyBehavior.Orb;s.speed=.9f;s.health=2;s.shotInterval=3;break;
            case "turret": case "core": s.behavior=EnemyBehavior.Turret;s.health=3;break;
            case "gunner": case "bomber": case "grenadier": case "streetgun": case "robot":
                s.behavior=EnemyBehavior.Spread;s.shotInterval=2.6f;break;
            case "spider": s.behavior=EnemyBehavior.Spider;s.speed=.8f;s.health=4;break;
            case "demon": s.behavior=EnemyBehavior.Giant;s.health=12; s.hitRadius=.8f;break;
            case "dragon": s.behavior=EnemyBehavior.Dragon;s.health=32;s.boss=true;s.hitRadius=1.8f;break;
            case "giant": s.behavior=EnemyBehavior.Giant;s.health=36;s.boss=true;s.hitRadius=1.5f;break;
            case "sorcererBlue": case "sorcererRed":
                s.behavior=EnemyBehavior.Sorcerer;s.health=24;s.boss=true;s.hitRadius=1;break;
            case "tank": s.behavior=EnemyBehavior.Tank;s.health=40;s.boss=true;s.speed=.55f;s.hitRadius=1.3f;break;
            case "computer": s.behavior=EnemyBehavior.Computer;s.health=60;s.boss=true;s.hitRadius=1.5f;break;
            case "barrier": s.behavior=EnemyBehavior.Barrier;s.health=5;s.obstacleSize=new Vector2(3,1);break;
            case "generator": s.behavior=EnemyBehavior.Generator;s.health=3;s.obstacleSize=new Vector2(.6f,.8f);break;
        }
        s.freezeImmune=s.boss || s.obstacleSize!=Vector2.zero;
        return s;
    }
}
