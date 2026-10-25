using System;
using System.Collections.Generic;
using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WebGLStreamedStageBuild
{
    const string SourceScene = "Assets/GainGround/Editable/Scenes/GainGround.unity";
    const string GeneratedFolder = "Assets/GainGround/GeneratedWebGL";
    const string GeneratedScene = GeneratedFolder + "/GainGroundWebGL.unity";
    const string OutputFolder = "Build/WebGLStreaming";

    [MenuItem("Gain Ground/Build WebGL with streamed stages")]
    public static void Build()
    {
        var source = AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene);
        if (source == null) throw new FileNotFoundException("Cena principal não encontrada", SourceScene);

        var originalStages = ReadStageList();
        if (AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.DeleteAsset(GeneratedFolder);
        AssetDatabase.CreateFolder("Assets/GainGround", "GeneratedWebGL");
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string output = Path.GetFullPath(Path.Combine(projectRoot, OutputFolder));
        string candidate = Path.GetFullPath(Path.Combine(projectRoot, "Build", "WebGLStreamingNext"));
        string content = Path.GetFullPath(Path.Combine(projectRoot, "Build", "WebGLStageContentTemp"));
        EnsureGeneratedPath(projectRoot, output);
        EnsureGeneratedPath(projectRoot, candidate);
        EnsureGeneratedPath(projectRoot, content);
        if (Directory.Exists(candidate)) Directory.Delete(candidate, true);
        if (Directory.Exists(content)) Directory.Delete(content, true);
        Directory.CreateDirectory(candidate);
        Directory.CreateDirectory(content);

        var builds = new List<AssetBundleBuild>();
        var previewNames = new string[originalStages.Length];
        var mapNames = new string[originalStages.Length];
        var musicNames = new string[originalStages.Length];
        var victoryNames = new string[originalStages.Length];
        var enemyNames = new string[originalStages.Length][];
        var objectNames = new string[originalStages.Length];
        var objectAddresses = new string[originalStages.Length][];
        var hazardAddresses = new string[originalStages.Length][];
        var musicByPath = new Dictionary<string, string>();
        var enemyByPair = new Dictionary<string, string>();
        for (int i = 0; i < originalStages.Length; i++)
        {
            var stage = originalStages[i];
            if (stage == null) throw new InvalidOperationException("Fase " + i + " não atribuída na cena.");
            if (stage.background != null)
            {
                string name = "stage-" + (i + 1).ToString("D2") + "-preview";
                string previewPath = stage.paintedLayout != null ?
                    WebGLStageAtlasGenerator.CreatePreview(stage, i) : RequireAssetPath(stage.background);
                builds.Add(new AssetBundleBuild { assetBundleName = name,
                    assetNames = new[] { previewPath },
                    addressableNames = new[] { "background" } });
                previewNames[i] = name;
            }
            var mapAssets = new List<string>();
            var mapAddresses = new List<string>();
            if (stage.paintedLayout != null)
            {
                mapAssets.Add(WebGLStageAtlasGenerator.Create(stage, i));
                mapAddresses.Add("layout");
            }
            if (stage.background == null && stage.terrainAtlas != null)
            { mapAssets.Add(RequireAssetPath(stage.terrainAtlas)); mapAddresses.Add("terrain"); }
            if (mapAssets.Count > 0)
            {
                string bundleName = "stage-" + (i + 1).ToString("D2") + "-map";
                builds.Add(new AssetBundleBuild {
                    assetBundleName = bundleName,
                    assetNames = mapAssets.ToArray(),
                    addressableNames = mapAddresses.ToArray()
                });
                mapNames[i] = bundleName;
            }
            musicNames[i] = AddMusicBundle(stage.stageMusic, builds, musicByPath);
            victoryNames[i] = AddMusicBundle(stage.victoryMusic, builds, musicByPath);
            enemyNames[i] = new string[stage.enemies.Length];
            for (int e = 0; e < stage.enemies.Length; e++)
                enemyNames[i][e] = AddEnemyBundle(stage.enemies[e], builds, enemyByPair);
            objectNames[i] = AddStageObjectBundle(stage, i, builds,
                out objectAddresses[i], out hazardAddresses[i]);
        }

        var portraitNames = new Dictionary<string, string>();
        var portraitByPath = new Dictionary<string, string>();
        foreach (string path in AssetDatabase.GetDependencies(SourceScene, true))
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (character != null && !portraitNames.ContainsKey(path) &&
                !path.StartsWith(GeneratedFolder + "/", StringComparison.Ordinal))
                portraitNames.Add(path, AddPortraitBundle(character.hudPortrait, builds, portraitByPath));
        }

        EnsureResourcesStayInPlayer(builds);
        var manifest = BuildPipeline.BuildAssetBundles(content, builds.ToArray(),
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.WebGL);
        if (manifest == null) throw new InvalidOperationException("A criação dos AssetBundles WebGL falhou.");

        var files = new Dictionary<string, StageBundleFile>();
        foreach (string name in manifest.GetAllAssetBundles())
        {
            if (manifest.GetAllDependencies(name).Length != 0)
                throw new InvalidOperationException("Bundle com dependências externas não suportadas: " + name);
            string hash = manifest.GetAssetBundleHash(name).ToString();
            string fileName = name + "-" + hash + ".bundle";
            File.Move(Path.Combine(content, name), Path.Combine(content, fileName));
            files.Add(name, new StageBundleFile { fileName = fileName, hash = hash });
        }

        // Building bundles can reload imported assets. Resolve fresh references before cloning them.
        originalStages = ReadStageList();
        GenerateLightweightScene(originalStages, previewNames, mapNames, musicNames, victoryNames,
            enemyNames, objectNames, objectAddresses, hazardAddresses, portraitNames, files);
        var options = new BuildPlayerOptions {
            scenes = new[] { GeneratedScene },
            locationPathName = candidate,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
            assetBundleManifestPath = Path.Combine(content, "WebGLStageContentTemp.manifest")
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("O build WebGL falhou. Consulte o Console da Unity.");

        foreach (string manifestFile in Directory.GetFiles(content, "*.manifest")) File.Delete(manifestFile);
        string rootBundle = Path.Combine(content, "WebGLStageContentTemp");
        if (File.Exists(rootBundle)) File.Delete(rootBundle);
        Directory.Move(content, Path.Combine(candidate, "Content"));
        if (Directory.Exists(output)) Directory.Delete(output, true);
        Directory.Move(candidate, output);
        Debug.Log("WebGL com fases sob demanda pronto em " + output + ". Publique o diretório inteiro, incluindo Content.");
    }

    static void EnsureGeneratedPath(string projectRoot, string path)
    {
        if (!path.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A saída precisa ficar dentro do projeto Unity: " + path);
    }

    static bool IsResourcesAsset(string path)
    {
        return path.Replace('\\', '/').IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void EnsureResourcesStayInPlayer(List<AssetBundleBuild> builds)
    {
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || !IsResourcesAsset(path)) continue;
            var importer = AssetImporter.GetAtPath(path);
            if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName))
                throw new InvalidOperationException("Remova o nome de AssetBundle do asset em Resources: " + path);
        }
        foreach (var bundle in builds)
            foreach (string asset in bundle.assetNames)
                foreach (string dependency in AssetDatabase.GetDependencies(asset, true))
                    if (IsResourcesAsset(dependency))
                        throw new InvalidOperationException("O bundle " + bundle.assetBundleName +
                            " depende de um asset em Resources: " + dependency + " (origem: " + asset + ")");
    }

    static StageDefinition[] ReadStageList()
    {
        string sceneText = File.ReadAllText(Path.Combine(Application.dataPath, "..", SourceScene));
        var stageGuids = new List<string>();
        bool inStages = false;
        foreach (string raw in sceneText.Split('\n'))
        {
            string line = raw.Trim();
            if (line == "stages:") { inStages = true; continue; }
            if (!inStages) continue;
            if (!line.StartsWith("- ", StringComparison.Ordinal)) break;
            int at = line.IndexOf("guid: ", StringComparison.Ordinal);
            if (at < 0) throw new InvalidOperationException("Referência de fase inválida na cena.");
            stageGuids.Add(line.Substring(at + 6, 32));
        }
        if (stageGuids.Count == 0) throw new InvalidOperationException("A cena não contém fases.");
        var result = new StageDefinition[stageGuids.Count];
        for (int i = 0; i < result.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(stageGuids[i]);
            result[i] = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
            if (result[i] == null) throw new FileNotFoundException("Fase não encontrada: " + stageGuids[i]);
        }
        return result;
    }

    static string RequireAssetPath(UnityEngine.Object asset)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Asset externo sem arquivo: " + asset.name);
        return path;
    }

    static string AddMusicBundle(AudioClip clip, List<AssetBundleBuild> builds, Dictionary<string, string> byPath)
    {
        if (clip == null) return null;
        string path = RequireAssetPath(clip);
        if (byPath.TryGetValue(path, out string existing)) return existing;
        string name = "music-" + AssetDatabase.AssetPathToGUID(path).Substring(0, 16);
        builds.Add(new AssetBundleBuild { assetBundleName = name,
            assetNames = new[] { path }, addressableNames = new[] { "music" } });
        byPath.Add(path, name);
        return name;
    }

    static string AddPortraitBundle(Texture2D portrait, List<AssetBundleBuild> builds,
        Dictionary<string, string> byPath)
    {
        if (portrait == null) return null;
        string path = RequireAssetPath(portrait);
        if (byPath.TryGetValue(path, out string existing)) return existing;
        string name = "portrait-" + AssetDatabase.AssetPathToGUID(path).Substring(0, 16);
        builds.Add(new AssetBundleBuild { assetBundleName = name,
            assetNames = new[] { path }, addressableNames = new[] { "portrait" } });
        byPath.Add(path, name);
        return name;
    }

    static string AddEnemyBundle(EnemySpawn spawn, List<AssetBundleBuild> builds,
        Dictionary<string, string> byPair)
    {
        if (spawn == null || (spawn.sprite == null && spawn.modelPrefab == null)) return null;
        string spritePath = spawn.sprite != null ? RequireAssetPath(spawn.sprite) : "";
        string modelPath = spawn.modelPrefab != null ? RequireAssetPath(spawn.modelPrefab) : "";
        string pair = spritePath + "|" + modelPath;
        if (byPair.TryGetValue(pair, out string existing)) return existing;
        var assetNames = new List<string>();
        var aliases = new List<string>();
        if (spawn.sprite != null) { assetNames.Add(spritePath); aliases.Add("sprite"); }
        if (spawn.modelPrefab != null) { assetNames.Add(modelPath); aliases.Add("model"); }
        string name = "enemy-" + byPair.Count.ToString("D2");
        builds.Add(new AssetBundleBuild { assetBundleName = name,
            assetNames = assetNames.ToArray(), addressableNames = aliases.ToArray() });
        byPair.Add(pair, name);
        return name;
    }

    static string AddStageObjectBundle(StageDefinition stage, int index, List<AssetBundleBuild> builds,
        out string[] objectAddresses, out string[] hazardAddresses)
    {
        var paths = new List<string>();
        var aliases = new List<string>();
        var byPath = new Dictionary<string, string>();
        objectAddresses = new string[stage.objects == null ? 0 : stage.objects.Length];
        hazardAddresses = new string[stage.hazards == null ? 0 : stage.hazards.Length];
        for (int i = 0; i < objectAddresses.Length; i++)
            objectAddresses[i] = AddStagePrefab(stage.objects[i]?.prefab, paths, aliases, byPath);
        for (int i = 0; i < hazardAddresses.Length; i++)
            hazardAddresses[i] = AddStagePrefab(stage.hazards[i]?.prefab, paths, aliases, byPath);
        if (paths.Count == 0) return null;
        string name = "stage-" + (index + 1).ToString("D2") + "-objects";
        builds.Add(new AssetBundleBuild { assetBundleName = name,
            assetNames = paths.ToArray(), addressableNames = aliases.ToArray() });
        return name;
    }

    static string AddStagePrefab(GameObject prefab, List<string> paths, List<string> aliases,
        Dictionary<string, string> byPath)
    {
        if (prefab == null) return null;
        string path = RequireAssetPath(prefab);
        if (byPath.TryGetValue(path, out string alias)) return alias;
        alias = "visual-" + paths.Count;
        paths.Add(path); aliases.Add(alias); byPath.Add(path, alias);
        return alias;
    }

    static void GenerateLightweightScene(StageDefinition[] originals, string[] previews, string[] maps, string[] music,
        string[] victory, string[][] enemies, string[] objects, string[][] objectAddresses,
        string[][] hazardAddresses, Dictionary<string, string> portraits,
        Dictionary<string, StageBundleFile> files)
    {
        var characterCopies = new Dictionary<string, CharacterDefinition>();
        var portraitFiles = new List<CharacterPortraitFiles>();
        foreach (var entry in portraits)
        {
            var original = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(entry.Key);
            if (original == null) throw new InvalidOperationException("Personagem de origem ausente: " + entry.Key);
            var copy = UnityEngine.Object.Instantiate(original);
            copy.name = original.name;
            copy.hudPortrait = null;
            AssetDatabase.CreateAsset(copy, GeneratedFolder + "/Character_" + characterCopies.Count.ToString("D2") + ".asset");
            characterCopies.Add(entry.Key, copy);
            portraitFiles.Add(new CharacterPortraitFiles { character = copy, portrait = Resolve(entry.Value, files) });
        }
        var clones = new StageDefinition[originals.Length];
        var media = new StageMediaFiles[originals.Length];
        for (int i = 0; i < originals.Length; i++)
        {
            var copy = UnityEngine.Object.Instantiate(originals[i]);
            copy.name = originals[i].name;
            copy.background = null;
            copy.paintedLayout = null;
            copy.terrainAtlas = null;
            copy.stageMusic = null;
            copy.victoryMusic = null;
            copy.rescue = Remap(copy.rescue, characterCopies);
            for (int c = 0; c < copy.additionalCaptives.Length; c++)
                copy.additionalCaptives[c].character = Remap(copy.additionalCaptives[c].character, characterCopies);
            var enemyFiles = new EnemyVisualFiles[copy.enemies.Length];
            for (int e = 0; e < copy.enemies.Length; e++)
            {
                enemyFiles[e] = new EnemyVisualFiles {
                    bundle = Resolve(enemies[i][e], files),
                    hasSprite = originals[i].enemies[e].sprite != null,
                    hasModel = originals[i].enemies[e].modelPrefab != null
                };
                copy.enemies[e].sprite = null;
                copy.enemies[e].modelPrefab = null;
            }
            if (copy.objects != null)
                foreach (var item in copy.objects)
                    if (item != null) item.prefab = null;
            if (copy.hazards != null)
                foreach (var item in copy.hazards)
                    if (item != null) { item.prefab = null; item.sprite = null; }
            AssetDatabase.CreateAsset(copy, GeneratedFolder + "/Stage_" + (i + 1).ToString("D2") + ".asset");
            clones[i] = copy;
            media[i] = new StageMediaFiles {
                preview = Resolve(previews[i], files),
                map = Resolve(maps[i], files),
                objects = Resolve(objects[i], files),
                music = Resolve(music[i], files),
                victoryMusic = Resolve(victory[i], files),
                hasBackground = originals[i].background != null,
                hasPaintedLayout = originals[i].paintedLayout != null,
                hasTerrainAtlas = originals[i].background == null && originals[i].terrainAtlas != null,
                enemyVisuals = enemyFiles,
                objectAddresses = objectAddresses[i],
                hazardAddresses = hazardAddresses[i]
            };
        }
        AssetDatabase.SaveAssets();
        if (!AssetDatabase.CopyAsset(SourceScene, GeneratedScene))
            throw new IOException("Não foi possível copiar a cena WebGL.");
        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.OpenScene(GeneratedScene, OpenSceneMode.Additive);
        try
        {
            GainGroundGame game = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                game = root.GetComponentInChildren<GainGroundGame>(true);
                if (game != null) break;
            }
            if (game == null) throw new InvalidOperationException("GainGroundGame não encontrado na cena WebGL.");
            game.stages = clones;
            game.startingParty = Remap(game.startingParty, characterCopies);
            game.secondStartingParty = Remap(game.secondStartingParty, characterCopies);
            game.allCharacters = Remap(game.allCharacters, characterCopies);
            game.webStageMedia = media;
            game.webCharacterPortraits = portraitFiles.ToArray();
            game.streamStageMedia = true;
            EditorUtility.SetDirty(game);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Falha ao salvar a cena WebGL.");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    static StageBundleFile Resolve(string name, Dictionary<string, StageBundleFile> files)
    {
        return string.IsNullOrEmpty(name) ? default : files[name];
    }

    static CharacterDefinition Remap(CharacterDefinition original,
        Dictionary<string, CharacterDefinition> copies)
    {
        if (original == null) return null;
        string path = AssetDatabase.GetAssetPath(original);
        if (!copies.TryGetValue(path, out var copy))
            throw new InvalidOperationException("Personagem sem cópia WebGL: " + original.name);
        return copy;
    }

    static CharacterDefinition[] Remap(CharacterDefinition[] originals,
        Dictionary<string, CharacterDefinition> copies)
    {
        if (originals == null) return null;
        var result = new CharacterDefinition[originals.Length];
        for (int i = 0; i < result.Length; i++) result[i] = Remap(originals[i], copies);
        return result;
    }
}
