using System;
using System.IO;
using GainGround.Game;
using GainGround.Recovered;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayableProjectBuilder
{
    const string Root = "Assets/GainGround/Editable";

    [MenuItem("Gain Ground/Create playable recreation (new assets only)")]
    public static void Create()
    {
        if (Directory.Exists(Root)) throw new InvalidOperationException("Editable assets already exist. Open Assets/GainGround/Editable/Scenes/GainGround.unity. Generation never overwrites your edits.");
        foreach (var folder in new[] { "Art", "Characters", "Stages", "Scenes", "RecoveredTiles" }) Directory.CreateDirectory(Root + "/" + folder);
        AssetDatabase.Refresh();
        string[] person = {
            "................", ".....111111.....", "....12222221....", "....12222221....",
            "....13333331....", ".....34343......", ".....33333......", "....1555551.....",
            "...155555551....", "..33155555133...", "..33155555133...", "....1555551.....",
            ".....16661......", ".....16.61......", "....116.611.....", "....111.111....." };
        Color skin = new Color(.93f, .69f, .45f);
        var characters = new CharacterDefinition[5];
        string[] names = { "Batedor", "Lanceira", "Guardiao", "Artilheira", "Explorador" };
        Color[] colors = { new Color(.25f,.75f,.95f), new Color(.95f,.65f,.22f), new Color(.38f,.85f,.55f), new Color(.85f,.42f,.85f), new Color(.95f,.42f,.4f) };
        for (int i = 0; i < characters.Length; i++)
        {
            var sprite = Art("hero_" + i, person, new[] { Color.clear, new Color(.08f,.1f,.15f), colors[i] * .7f, skin, Color.black, colors[i], new Color(.2f,.23f,.32f) });
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            definition.displayName = names[i]; definition.sprite = sprite; definition.speed = i == 0 || i == 4 ? 3.5f : 2.7f;
            definition.shotInterval = i == 3 ? .18f : .32f; definition.range = i == 1 ? 6 : 8;
            definition.damage = i == 2 ? 2 : 1; definition.specialPassesWalls = i != 0;
            definition.shotColor = colors[i];
            AssetDatabase.CreateAsset(definition, CharacterAssets.Folder + "/" + names[i] + ".asset"); characters[i] = definition;
        }
        var enemy = Art("enemy", person, new[] { Color.clear, new Color(.16f,.05f,.08f), new Color(.48f,.17f,.2f), skin, Color.black, new Color(.86f,.26f,.3f), new Color(.29f,.16f,.22f) });
        var floor = Art("floor", new[] { "11111111", "11111111", "11211111", "11111111", "11111111", "11111121", "11111111", "11111111" }, new[] { Color.clear, Color.white, new Color(.8f,.8f,.8f) });
        var wall = Art("wall", new[] { "11111111", "12221222", "13331333", "11111111", "22122212", "33133313", "11111111", "44444444" }, new[] { Color.clear, new Color(.23f,.28f,.32f), new Color(.54f,.59f,.6f), new Color(.4f,.46f,.49f), new Color(.12f,.17f,.2f) });
        var bullet = Art("projectile", new[] { "0110", "1221", "1221", "0110" }, new[] { Color.clear, Color.white, Color.white });
        var exit = Art("exit", new[] { "11111111", "12222221", "12211221", "12111121", "12211221", "12211221", "12222221", "11111111" }, new[] { Color.clear, new Color(.45f,.95f,.69f), new Color(.08f,.3f,.24f) });

        var stages = new StageDefinition[3];
        for (int i = 0; i < 3; i++)
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            stage.displayName = new[] { "Posto avancado", "Passagem de pedra", "Ultima defesa" }[i];
            stage.timeLimit = 120 + i * 15; stage.rescue = characters[i + 2];
            stage.rescuePosition = new Vector2(-6.4f, 2.5f);
            stage.floorColor = new[] { new Color(.16f,.29f,.25f), new Color(.27f,.25f,.2f), new Color(.19f,.23f,.32f) }[i];
            stage.walls = i == 0 ? new[] { new Rect(-4,-1,2,1), new Rect(2,1,2,1) }
                : i == 1 ? new[] { new Rect(-5,-2,3,1), new Rect(2,-2,3,1), new Rect(-1,0,2,2), new Rect(-5,2,2,1) }
                : new[] { new Rect(-5,-2,2,3), new Rect(3,-2,2,3), new Rect(-1,1,2,1), new Rect(-6,3,2,1), new Rect(4,3,2,1) };
            Vector2[] points = { new Vector2(-6,0), new Vector2(6,1), new Vector2(-3,3.5f), new Vector2(3,3.5f), new Vector2(0,3), new Vector2(6,-2), new Vector2(-2,1) };
            stage.enemies = new EnemySpawn[4 + i];
            for (int e = 0; e < stage.enemies.Length; e++) stage.enemies[e] = new EnemySpawn { position = points[e], health = i + 2, speed = .55f + .15f * i, sentry = e % 2 == 0, shotInterval = 2.8f - i * .4f };
            AssetDatabase.CreateAsset(stage, Root + "/Stages/Stage_0" + (i + 1) + ".asset"); stages[i] = stage;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 7;
        camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = new Color(.035f,.05f,.075f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        var game = new GameObject("Gain Ground - editable recreation").AddComponent<GainGroundGame>();
        game.titleMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GainGround/Soundtrack/simulation-of-war-opening.mp3");
        game.stages = stages; game.playerCount = 2;
        game.startingParty = new[] { characters[0], characters[1] };
        game.secondStartingParty = new[] { characters[0], characters[1] }; game.gameCamera = camera;
        game.floorSprite = floor; game.wallSprite = wall; game.enemySprite = enemy; game.projectileSprite = bullet; game.exitSprite = exit;
        string scenePath = Root + "/Scenes/GainGround.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        PlayerSettings.productName = "Gain Ground - Unity Recreation";
        PlayerSettings.defaultScreenWidth = 1000; PlayerSettings.defaultScreenHeight = 700;
        PlayerSettings.runInBackground = true;
        ExportRecoveredTiles();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("Playable recreation created with editable scene, stage assets, character assets and PNG art.");
    }

    static Sprite Art(string name, string[] rows, Color[] palette)
    {
        var texture = new Texture2D(rows[0].Length, rows.Length, TextureFormat.RGBA32, false);
        for (int y = 0; y < rows.Length; y++) for (int x = 0; x < rows[y].Length; x++)
            texture.SetPixel(x, rows.Length - 1 - y, rows[y][x] == '.' ? Color.clear : palette[rows[y][x] - '0']);
        texture.Apply();
        string file = Root + "/Art/" + name + ".png";
        File.WriteAllBytes(file, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        return ImportSprite(file);
    }
    static Sprite ImportSprite(string file)
    {
        AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(file);
        importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(file);
    }
    static void ExportRecoveredTiles()
    {
        string romPath = "Analysis/OriginalROM.bin";
        if (!File.Exists(romPath)) return;
        var rom = new RomImage(File.ReadAllBytes(romPath));
        if (!rom.IsKnownImage) throw new InvalidDataException("ROM revision does not match recovered offsets.");
        for (int i = 0; i < 20; i++)
        {
            var data = ZeroRunDecoder.Decode(rom, (int)rom.U32(0x3CC68 + i * 16), 0x86).Bytes;
            var texture = new Texture2D(128, 72, TextureFormat.RGBA32, false);
            var pixels = new Color32[128 * 72];
            for (int tile = 0; tile < data.Length / 32; tile++) for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                byte packed = data[tile * 32 + y * 4 + x / 2]; byte value = (byte)((x % 2 == 0 ? packed >> 4 : packed & 15) * 17);
                pixels[(71 - (tile / 16 * 8 + y)) * 128 + tile % 16 * 8 + x] = new Color32(value, value, value, 255);
            }
            texture.SetPixels32(pixels); texture.Apply();
            string file = Root + "/RecoveredTiles/ROM_tiles_" + i.ToString("D2") + "_diagnostic.png";
            File.WriteAllBytes(file, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); ImportSprite(file);
        }
    }

    public static void BuildWindows()
    {
        var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, "Builds/Windows/GainGround.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Windows build failed: " + report.summary.result);
    }
}
