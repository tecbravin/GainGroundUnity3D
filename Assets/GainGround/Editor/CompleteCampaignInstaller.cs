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
        var game=UnityEngine.Object.FindAnyObjectByType<GainGroundGame>();
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
        // 4-9 has its own terrain, actor positions and editable layout decoded from
        // the PS2 ISO. Reinstalling the Mega Drive campaign must not restore its
        // incompatible 12x14 maze or replace the PS2 enemy placements.
        if (map.round==4 && map.number==9 && s!=null &&
            File.Exists(Root + "/PS2Native/Stage_4_9.json")) return s;
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
            if (map.round == 2 && map.number == 5) ConfigureStage25(s);
            if (map.round == 2 && map.number == 4) ConfigureStage24(s);
            if (map.round == 2 && map.number == 3)
            {
                var blade = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/GainGround/Editable/Art/Projectiles/SpinningSword.png");
                if (blade == null) throw new InvalidDataException("Missing 2-3 spinning blade sprite.");
                foreach (var enemy in s.enemies)
                    if (enemy.sprite != null && enemy.sprite.name == "axeman" && enemy.position.y > 3f)
                    {
                        enemy.melee = false;
                        enemy.shotInterval = 2.2f;
                        enemy.projectileSprite = blade;
                        enemy.projectileSize = new Vector2(.42f, .42f);
                        enemy.projectileSpinDegreesPerSecond = 900f;
                    }
            }
            if (map.round == 1 && map.number == 9)
                for (int i = 0; i < s.enemies.Length; i++)
                    if (map.enemies[i].kind == "archer" && !map.enemies[i].elevated)
                    {
                        s.enemies[i].sentry = false;
                        s.enemies[i].speed = .85f;
                    }
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
    static void ConfigureStage24(StageDefinition stage)
    {
        if (stage.enemies.Length != 18) throw new InvalidDataException("Unexpected 2-4 enemy count.");
        var archer = AssetDatabase.LoadAssetAtPath<Sprite>($"{Content}/Sprites/archer.png");
        var archerModel = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/GainGround/Editable/Characters/Models3D/EnemyActors/archer.prefab")?.GetComponent<PS2EnemyVisual>();
        var knight = AssetDatabase.LoadAssetAtPath<Sprite>($"{Content}/Sprites/knight.png");
        var knightModel = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/GainGround/Editable/Characters/Models3D/EnemyActors/RunningKnight.prefab")?.GetComponent<PS2EnemyVisual>();
        if (archer == null || archerModel == null || knight == null || knightModel == null)
            throw new InvalidDataException("Missing 2-4 archer or knight assets.");
        for (int i = 0; i < 8; i++)
        {
            var enemy = stage.enemies[i];
            enemy.sprite = archer; enemy.modelPrefab = archerModel;
            enemy.visualSize = new Vector2(1.5f, 1.5f);
            enemy.behavior = i == 0 || i == 7 ? EnemyBehavior.BurstArcher : EnemyBehavior.WallArcher;
            enemy.shotInterval = i == 0 || i == 7 ? 2.8f : 2.4f;
            enemy.initialShotDelay = 1.2f + i * .12f;
        }
        for (int i = 8; i < 10; i++)
        {
            var enemy = stage.enemies[i];
            enemy.sprite = knight; enemy.modelPrefab = knightModel;
            enemy.visualSize = new Vector2(1.5f, 1.75f);
            enemy.health = 4; enemy.speed = 3.1f;
            enemy.behavior = EnemyBehavior.ChargeKnight; enemy.melee = true;
        }
        for (int i = 10; i < 18; i++)
        {
            var enemy = stage.enemies[i];
            enemy.position = new Vector2(i % 2 == 0 ? -5.35f : 5.35f,
                i < 14 ? -5.7f : 5.7f);
            enemy.speed = 2.25f; enemy.spawnDelay = 2f + (i - 10) * .8f;
            enemy.behavior = EnemyBehavior.Ambusher;
        }
    }
    static void ConfigureStage25(StageDefinition stage)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Content}/Sprites/archer.png");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/GainGround/Editable/Characters/Models3D/EnemyActors/archer.prefab")?.GetComponent<PS2EnemyVisual>();
        if (sprite == null || model == null) throw new InvalidDataException("Missing 2-5 archer assets.");
        var positions = new[] {
            new Vector2(-4.8f,-4.8f), new Vector2(-2f,-4.8f), new Vector2(2f,-4.8f), new Vector2(4.8f,-4.8f),
            new Vector2(-5.3f,-2.7f), new Vector2(-1.8f,-2.7f), new Vector2(1.8f,-2.7f), new Vector2(5.3f,-2.7f),
            new Vector2(-4.9f,-1.45f), new Vector2(4.9f,-1.45f),
            new Vector2(-5.4f,-5.7f), new Vector2(5.4f,-5.7f),
            new Vector2(-5.4f,-3.8f), new Vector2(5.4f,-3.8f),
            new Vector2(-5.4f,-5.7f), new Vector2(5.4f,-5.7f),
            new Vector2(-5.4f,-3.8f), new Vector2(5.4f,-3.8f) };
        stage.enemies = new EnemySpawn[positions.Length];
        for (int i = 0; i < stage.enemies.Length; i++)
        {
            var enemy = new EnemySpawn();
            enemy.sprite = sprite; enemy.modelPrefab = model;
            enemy.visualSize = new Vector2(1.5f, 1.5f);
            enemy.position = positions[i]; enemy.sentry = false; enemy.melee = false;
            enemy.health = 1;
            enemy.behavior = i < 10 ? EnemyBehavior.Chaser : EnemyBehavior.Ambusher;
            enemy.speed = i < 10 ? .9f : 1.7f;
            enemy.shotInterval = i < 10 ? 2.4f : 1.9f;
            enemy.spawnDelay = i < 10 ? 0f : 3f + (i - 10) * 1.2f;
            stage.enemies[i] = enemy;
        }
        var walls = new System.Collections.Generic.List<Rect>(stage.walls);
        walls.Add(new Rect(-6f, -1f, 5f, 2.5f));
        walls.Add(new Rect(1f, -1f, 5f, 2.5f));
        stage.walls = walls.ToArray();
    }
}
