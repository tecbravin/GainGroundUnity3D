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
        if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder("Assets/GainGround", "GeneratedWebGL");
        CleanupLegacyGeneratedAssets();
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string output = Path.GetFullPath(Path.Combine(projectRoot, OutputFolder));
        string candidate = Path.GetFullPath(Path.Combine(projectRoot, "Build", "WebGLStreamingNext"));
        string content = Path.GetFullPath(Path.Combine(projectRoot, "Build", "WebGLStageContentTemp"));
        string bundleCache = Path.GetFullPath(Path.Combine(projectRoot, "Library", "WebGLStageAssetBundleCache"));
        EnsureGeneratedPath(projectRoot, output);
        EnsureGeneratedPath(projectRoot, candidate);
        EnsureGeneratedPath(projectRoot, content);
        EnsureGeneratedPath(projectRoot, bundleCache);
        if (Directory.Exists(candidate)) Directory.Delete(candidate, true);
        if (Directory.Exists(content)) Directory.Delete(content, true);
        Directory.CreateDirectory(candidate);
        Directory.CreateDirectory(content);
        Directory.CreateDirectory(bundleCache);

        var builds = new List<AssetBundleBuild>();
        var previewNames = new string[originalStages.Length];
        var previewAddresses = new string[originalStages.Length];
        var mapNames = new string[originalStages.Length];
        var mapAddresses = new string[originalStages.Length];
        var musicNames = new string[originalStages.Length];
        var victoryNames = new string[originalStages.Length];
        var enemyNames = new string[originalStages.Length][];
        var enemyModelNames = new string[originalStages.Length][];
        var enemyModelAddresses = new string[originalStages.Length][];
        var objectNames = new string[originalStages.Length];
        var objectAddresses = new string[originalStages.Length][];
        var hazardAddresses = new string[originalStages.Length][];
        var musicByPath = new Dictionary<string, string>();
        var enemyBundleByGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var enemyModelOwners = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.OrdinalIgnoreCase);
        var enemySpriteBundles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < originalStages.Length; i++)
        {
            var stage = originalStages[i];
            if (stage == null) throw new InvalidOperationException("Fase " + i + " não atribuída na cena.");
            if (stage.paintedLayout == null && stage.name != "Stage_1_7")
                throw new InvalidOperationException(stage.name + " não possui layout para gerar o mapa compactado.");

            string bundleName = "stage-" + (i + 1).ToString("D2") + "-map";
            mapNames[i] = bundleName;
            mapAddresses[i] = "background";
            var packageAssets = new List<string>();
            var packageAddresses = new List<string>();
            // Stage 1-7 keeps its ISO map directly on the lightweight stage definition,
            // so its object bundle does not need a duplicate raster background.
            if (stage.name != "Stage_1_7")
            {
                packageAssets.Add(WebGLStageAtlasGenerator.CreateRuntimeMap(stage, i));
                packageAddresses.Add(mapAddresses[i]);
            }
            objectAddresses[i] = new string[stage.objects == null ? 0 : stage.objects.Length];
            hazardAddresses[i] = new string[stage.hazards == null ? 0 : stage.hazards.Length];
            var packagePathAliases = new Dictionary<string, string>();
            for (int o = 0; o < objectAddresses[i].Length; o++)
                objectAddresses[i][o] = AddStagePrefab(stage.objects[o]?.prefab,
                    packageAssets, packageAddresses, packagePathAliases);
            for (int h = 0; h < hazardAddresses[i].Length; h++)
                hazardAddresses[i][h] = AddStagePrefab(stage.hazards[h]?.prefab,
                    packageAssets, packageAddresses, packagePathAliases);
            builds.Add(new AssetBundleBuild {
                assetBundleName = bundleName,
                assetNames = packageAssets.ToArray(),
                addressableNames = packageAddresses.ToArray()
            });

            musicNames[i] = AddMusicBundle(stage.stageMusic, builds, musicByPath);
            victoryNames[i] = AddMusicBundle(stage.victoryMusic, builds, musicByPath);
            enemyNames[i] = new string[stage.enemies.Length];
            enemyModelNames[i] = new string[stage.enemies.Length];
            enemyModelAddresses[i] = new string[stage.enemies.Length];
            for (int e = 0; e < stage.enemies.Length; e++)
                enemyNames[i][e] = AddEnemyBundle(stage.enemies[e], builds, enemyBundleByGroup,
                    enemyModelOwners, enemySpriteBundles, out enemyModelNames[i][e], out enemyModelAddresses[i][e]);
            objectNames[i] = objectAddresses[i].Length > 0 || hazardAddresses[i].Length > 0 ? bundleName : null;
        }

        for (int world = 0; world < Mathf.CeilToInt(originalStages.Length / 10f); world++)
        {
            string previewPath = WebGLStageAtlasGenerator.CreatePreviewAtlas(originalStages, world);
            string bundleName = "stage-previews-" + (world + 1).ToString("D2");
            builds.Add(new AssetBundleBuild { assetBundleName = bundleName,
                assetNames = new[] { previewPath }, addressableNames = new[] { "preview-atlas" } });
            int first = world * 10, end = Mathf.Min(first + 10, originalStages.Length);
            for (int i = first; i < end; i++)
            {
                previewNames[i] = bundleName;
                previewAddresses[i] = "stage-" + (i + 1).ToString("D2") + "-preview";
            }
        }

        var portraitNames = new Dictionary<string, string>();
        var portraitAddresses = new Dictionary<string, string>();
        var portraitByPath = new Dictionary<string, string>();
        foreach (string path in AssetDatabase.GetDependencies(SourceScene, true))
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (character != null && !portraitNames.ContainsKey(path) &&
                !path.StartsWith(GeneratedFolder + "/", StringComparison.Ordinal))
            {
                string portraitPath = character.hudPortrait != null ? RequireAssetPath(character.hudPortrait) : null;
                if (!string.IsNullOrEmpty(portraitPath) && enemySpriteBundles.TryGetValue(portraitPath, out string enemyBundle))
                {
                    portraitNames.Add(path, enemyBundle);
                    portraitAddresses.Add(path, "sprite");
                }
                else
                {
                    portraitNames.Add(path, AddPortraitBundle(character.hudPortrait, builds, portraitByPath));
                    portraitAddresses.Add(path, "portrait");
                }
            }
        }

        EnsureResourcesStayInPlayer(builds);
        // Keep the raw-name bundles and manifests in Library so Unity can skip unchanged bundles next time.
        var manifest = BuildPipeline.BuildAssetBundles(bundleCache, builds.ToArray(),
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.WebGL);
        if (manifest == null) throw new InvalidOperationException("A criação dos AssetBundles WebGL falhou.");

        var files = new Dictionary<string, StageBundleFile>();
        foreach (string name in manifest.GetAllAssetBundles())
        {
            if (manifest.GetAllDependencies(name).Length != 0)
                throw new InvalidOperationException("Bundle com dependências externas não suportadas: " + name);
            string hash = manifest.GetAssetBundleHash(name).ToString();
            string fileName = name + "-" + hash + ".bundle";
            File.Copy(Path.Combine(bundleCache, name), Path.Combine(content, fileName), true);
            files.Add(name, new StageBundleFile { fileName = fileName, hash = hash });
        }
        string cacheRootName = Path.GetFileName(bundleCache);
        File.Copy(Path.Combine(bundleCache, cacheRootName), Path.Combine(content, "WebGLStageContentTemp"), true);
        File.Copy(Path.Combine(bundleCache, cacheRootName + ".manifest"),
            Path.Combine(content, "WebGLStageContentTemp.manifest"), true);
        foreach (string bundleManifest in Directory.GetFiles(bundleCache, "*.manifest"))
        {
            if (Path.GetFileName(bundleManifest) == cacheRootName + ".manifest") continue;
            File.Copy(bundleManifest, Path.Combine(content, Path.GetFileName(bundleManifest)), true);
        }

        // Building bundles can reload imported assets. Resolve fresh references before cloning them.
        originalStages = ReadStageList();
        GenerateLightweightScene(originalStages, previewNames, previewAddresses, mapNames, mapAddresses,
            musicNames, victoryNames, enemyNames, enemyModelNames, enemyModelAddresses,
            objectNames, objectAddresses, hazardAddresses,
            portraitNames, portraitAddresses, files);
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
        Dictionary<string, int> bundleByGroup,
        Dictionary<string, KeyValuePair<string, string>> modelOwners,
        Dictionary<string, string> spriteBundles,
        out string modelBundle, out string modelAddress)
    {
        modelBundle = null;
        modelAddress = null;
        if (spawn == null || (spawn.sprite == null && spawn.modelPrefab == null)) return null;
        string spritePath = spawn.sprite != null ? RequireAssetPath(spawn.sprite) : "";
        string modelPath = spawn.modelPrefab != null ? RequireAssetPath(spawn.modelPrefab) : "";
        string group = !string.IsNullOrEmpty(spritePath) ? "sprite:" + spritePath : "model:" + modelPath;
        if (!bundleByGroup.TryGetValue(group, out int buildIndex))
        {
            string name = "enemy-" + bundleByGroup.Count.ToString("D2");
            var assetNames = new List<string>();
            var aliases = new List<string>();
            if (!string.IsNullOrEmpty(spritePath)) { assetNames.Add(spritePath); aliases.Add("sprite"); }
            builds.Add(new AssetBundleBuild { assetBundleName = name,
                assetNames = assetNames.ToArray(), addressableNames = aliases.ToArray() });
            buildIndex = builds.Count - 1;
            bundleByGroup.Add(group, buildIndex);
        }

        string bundleName = builds[buildIndex].assetBundleName;
        if (!string.IsNullOrEmpty(spritePath)) spriteBundles[spritePath] = bundleName;
        if (!string.IsNullOrEmpty(modelPath))
        {
            if (!modelOwners.TryGetValue(modelPath, out var owner))
            {
                var bundle = builds[buildIndex];
                var paths = new List<string>(bundle.assetNames);
                var aliases = new List<string>(bundle.addressableNames);
                modelAddress = "model-" + paths.Count;
                paths.Add(modelPath);
                aliases.Add(modelAddress);
                bundle.assetNames = paths.ToArray();
                bundle.addressableNames = aliases.ToArray();
                builds[buildIndex] = bundle;
                modelBundle = bundleName;
                modelOwners.Add(modelPath, new KeyValuePair<string, string>(modelBundle, modelAddress));
            }
            else
            {
                modelBundle = owner.Key;
                modelAddress = owner.Value;
            }
        }
        return bundleName;
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

    static void GenerateLightweightScene(StageDefinition[] originals, string[] previews,
        string[] previewAddresses, string[] maps, string[] mapAddresses, string[] music,
        string[] victory, string[][] enemies, string[][] enemyModelBundles, string[][] enemyModelAddresses,
        string[] objects, string[][] objectAddresses, string[][] hazardAddresses, Dictionary<string, string> portraits,
        Dictionary<string, string> portraitAddresses, Dictionary<string, StageBundleFile> files)
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
            copy = CreateOrUpdateAsset(copy, GeneratedFolder + "/Character_" + characterCopies.Count.ToString("D2") + ".asset");
            characterCopies.Add(entry.Key, copy);
            portraitFiles.Add(new CharacterPortraitFiles {
                character = copy, portrait = Resolve(entry.Value, files), portraitAddress = portraitAddresses[entry.Key]
            });
        }
        var clones = new StageDefinition[originals.Length];
        var media = new StageMediaFiles[originals.Length];
        for (int i = 0; i < originals.Length; i++)
        {
            var copy = UnityEngine.Object.Instantiate(originals[i]);
            copy.name = originals[i].name;
            // Stage 1-7 keeps its small source-map texture in the lightweight stage asset.
            // Its painted tilemap data is not included in this build, so clearing the
            // background leaves the Web scene showing only the generic fallback floor
            // until a streamed preview happens to be available.
            copy.background = originals[i].name == "Stage_1_7"
                ? originals[i].background ?? AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/GainGround/Editable/OriginalCampaign/Complete/Maps/1-07.png")
                : null;
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
                    modelBundle = Resolve(enemyModelBundles[i][e], files),
                    modelAddress = enemyModelAddresses[i][e],
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
            copy = CreateOrUpdateAsset(copy, GeneratedFolder + "/Stage_" + (i + 1).ToString("D2") + ".asset");
            clones[i] = copy;
            media[i] = new StageMediaFiles {
                preview = Resolve(previews[i], files),
                previewAddress = previewAddresses[i],
                map = Resolve(maps[i], files),
                mapAddress = mapAddresses[i],
                objects = Resolve(objects[i], files),
                music = Resolve(music[i], files),
                victoryMusic = Resolve(victory[i], files),
                hasBackground = true,
                hasPaintedLayout = false,
                hasTerrainAtlas = false,
                enemyVisuals = enemyFiles,
                objectAddresses = objectAddresses[i],
                hazardAddresses = hazardAddresses[i]
            };
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.DeleteAsset(GeneratedScene);
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

    static T CreateOrUpdateAsset<T>(T generated, string path) where T : UnityEngine.Object
    {
        var existing = AssetDatabase.LoadMainAssetAtPath(path) as T;
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }
        else
        {
            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }

    static void CleanupLegacyGeneratedAssets()
    {
        AssetDatabase.DeleteAsset(GeneratedFolder + "/StageAtlases");
        string previewFolder = GeneratedFolder + "/StagePreviews";
        if (!AssetDatabase.IsValidFolder(previewFolder)) return;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { previewFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path).StartsWith("Stage_", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(path);
        }
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

// Installed PWA builds can honor this before Unity initializes; browser tabs
// still use the runtime Screen Orientation API when the browser supports it.
public sealed class GainGroundWebGLLandscapeManifest : UnityEditor.Build.IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        string path = Path.Combine(report.summary.outputPath, "manifest.webmanifest");
        if (!File.Exists(path)) return;
        string manifest = File.ReadAllText(path);
        if (manifest.Contains("\"orientation\"")) return;
        int openingBrace = manifest.IndexOf('{');
        if (openingBrace < 0) throw new InvalidDataException("Manifesto PWA inválido: " + path);
        manifest = manifest.Insert(openingBrace + 1, "\n  \"orientation\": \"landscape\",");
        File.WriteAllText(path, manifest);
    }
}
