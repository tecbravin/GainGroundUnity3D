using System;
using System.Collections.Generic;
using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OriginalStagesBuilder
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";
    const string Scene = "Assets/GainGround/Editable/Scenes/GainGround.unity";
    static Vector2 Point(float x, float y) => new Vector2((x - 96) / 16f, (112 - y) / 16f);
    static Rect Area(float x, float y, float w, float h) => new Rect((x - 96) / 16f, (112 - y - h) / 16f, w / 16f, h / 16f);

    // Explicit installation only. Existing campaign assets are never regenerated on import.
    [MenuItem("Gain Ground/Install first five Mega Drive stages (new assets only)")]
    public static void Install()
    {
        if (Directory.Exists(Root)) throw new InvalidOperationException("OriginalCampaign already exists. Edit its stage assets in the Inspector.");
        Directory.CreateDirectory(Root);
        File.Copy("Analysis/StageReferences/GainGround-Rounds.png", Root + "/Rounds.png");
        AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/Rounds.png");
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096; importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false; importer.SaveAndReimport();
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Rounds.png");
        var johnny = Hero("Johnny", "Batedor", 3.2f, 9, false);
        var athra = Hero("Athra", "Lanceira", 3.8f, 6, true);
        var betty = Hero("Betty", "Artilheira", 2.8f, 7, true);
        var glow = Hero("Glow Knight", "Guardiao", 2.6f, 7, false);
        var babalu = Hero("Babalu", "Explorador", 3.2f, 10, true);
        var valkyrie = Hero("Valkyrie", "Lanceira", 3.2f, 8, false);
        var stages = new StageDefinition[5];
        for (int i = 0; i < stages.Length; i++)
        {
            var s = ScriptableObject.CreateInstance<StageDefinition>();
            s.stageMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GainGround/Editable/Audio/01_Genesis_Remastered.wav");
            s.victoryMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GainGround/Editable/Audio/06_Victory_Remastered.wav");
            s.displayName = "1-" + (i + 1) + " / " + new[] { "Planicie", "Ponte", "Rochas", "Fortificacao", "Cerco" }[i];
            s.size = new Vector2(12, 14); s.start = Point(96, 208);
            s.timeLimit = 120; s.exit = Area(0, 0, 192, 16);
            s.terrainAtlas = atlas; s.atlasOrigin = new Vector2Int(166 + 324 * i, 133);
            var enemies = new List<EnemySpawn>(); var patches = new List<RectInt>(); var sources = new List<Vector2Int>();
            Action<int, int, bool, bool, float> enemy = (x, y, elevated, melee, delay) =>
            {
                enemies.Add(new EnemySpawn { position = Point(x, y), health = 1, sentry = !melee,
                    speed = melee ? 1.1f : 0, shotInterval = 2.8f, elevated = elevated, melee = melee, spawnDelay = delay });
                if (delay == 0)
                {
                    patches.Add(new RectInt(x - 10, Math.Max(0, y - 14), 20, 26));
                    sources.Add(new Vector2Int(8, 192));
                }
            };
            Action<int, int> archer = (x,y) => enemy(x,y,false,false,0);
            Action<int, int> warrior = (x,y) => enemy(x,y,false,true,0);
            Action<int, int> high = (x,y) => enemy(x,y,true,false,0);
            if (i == 0)
            {
                archer(18,34); archer(66,58); archer(138,50);
                archer(42,90); archer(114,90); archer(170,106);
                s.rescue = glow; s.rescuePosition = Point(72,88);
            }
            else if (i == 1)
            {
                s.walls = new[] { Area(0,120,72,64), Area(120,120,72,64), Area(64,32,64,16) };
                archer(18,34); archer(74,22); archer(106,22); archer(170,34);
                archer(82,58); archer(106,58); archer(42,74); archer(146,74);
                enemy(8,192,false,true,4); enemy(184,192,false,true,6);
                s.rescue = babalu; s.rescuePosition = Point(96,152);
            }
            else if (i == 2)
            {
                s.exit = Area(104,136,48,16);
                s.walls = new[] { Area(0,0,36,160), Area(88,32,16,128), Area(152,32,16,128), Area(104,152,48,16) };
                high(12,26); high(12,58); high(12,90); high(12,122);
                warrior(50,42); warrior(74,42); warrior(50,90); warrior(74,90);
                archer(50,138); archer(74,138); archer(178,82); archer(178,122);
            }
            else if (i == 3)
            {
                s.exit = Area(64,0,64,16);
                s.walls = new[] { Area(0,0,64,40), Area(128,0,64,40) };
                high(12,10); high(44,10); high(148,10); high(180,10);
                warrior(76,34); warrior(116,34); warrior(96,58);
                archer(50,66); archer(138,66); archer(26,98); archer(166,98);
                warrior(72,106); warrior(96,106); warrior(120,106);
                archer(12,122); archer(36,122); archer(156,122); archer(180,122);
                warrior(50,146); warrior(138,146);
                enemy(8,152,false,true,4); enemy(184,152,false,true,6);
            }
            else
            {
                foreach (int x in new[] { 16,56,96,136,176 }) archer(x,34);
                archer(76,66); archer(116,66); warrior(36,82); warrior(156,82);
                warrior(84,122); warrior(108,122); archer(28,130); archer(164,130);
                enemy(8,108,false,true,4); enemy(184,108,false,true,6);
                s.rescue = valkyrie; s.rescuePosition = Point(96,98);
            }
            if (s.rescue != null)
            {
                var p = s.rescuePosition * new Vector2(16,-16) + new Vector2(96,112);
                patches.Add(new RectInt((int)p.x - 6,(int)p.y - 8,12,16));
                sources.Add(i == 1 ? new Vector2Int(80,136) : new Vector2Int(8,192));
            }
            s.enemies = enemies.ToArray(); s.terrainPatches = patches.ToArray(); s.patchSources = sources.ToArray();
            AssetDatabase.CreateAsset(s, Root + "/Stage_1_" + (i + 1) + ".asset"); stages[i] = s;
        }
        EditorSceneManager.OpenScene(Scene);
        var game = UnityEngine.Object.FindAnyObjectByType<GainGroundGame>();
        game.stages = stages; game.playerCount = 2;
        game.startingParty = new[] { johnny, athra, betty };
        game.secondStartingParty = new[] { johnny, athra, betty };
        EditorUtility.SetDirty(game); EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Installed Mega Drive stages 1-1 through 1-5.");
    }

    static CharacterDefinition Hero(string name, string template, float speed, float range, bool elevated)
    {
        var path = CharacterAssets.Folder + "/" + name.Replace(" ", "_") + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
        if (existing != null) return existing;
        var source = CharacterAssets.Load(template);
        var hero = UnityEngine.Object.Instantiate(source);
        hero.name = hero.displayName = name; hero.speed = speed; hero.range = range; hero.specialPassesWalls = elevated;
        AssetDatabase.CreateAsset(hero, path);
        return hero;
    }
}
