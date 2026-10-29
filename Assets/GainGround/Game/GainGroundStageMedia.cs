using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        Coroutine stageMediaRoutine, stagePreviewRoutine, victoryMusicRoutine, selectionPortraitRoutine, unloadMediaRoutine;
        int previewStageIndex = -1;
        int pendingPreviewStageIndex = -1;
        float previewChangeTime;
        bool unloadMediaPending;
        float lastMediaReleaseTime;
        const float MediaUnloadIdleSeconds = .8f;
        const float StagePreviewDelay = .18f;
        string mediaError = "", loadingStep = "";
        bool selectionPortraitsLoading;

        void BeginStageMediaLoad()
        {
            CancelStageMediaRequests();
            if (webStageMedia == null || webStageMedia.Length != stages.Length ||
                stageIndex < 0 || stageIndex >= webStageMedia.Length)
            {
                mediaError = "Catálogo de fases incompleto. Gere novamente o WebGL com o menu Gain Ground.";
                state = ScreenState.Loading;
                return;
            }
            if (arena != null) arena.gameObject.SetActive(false);
            stage = null;
            restartMusic = true;
            mediaError = "";
            loadingStep = "Preparando fase " + (stageIndex + 1) + "...";
            state = ScreenState.Loading;
            stageMediaRoutine = StartCoroutine(LoadStageMedia());
        }

        IEnumerator LoadStageMedia()
        {
            var files = webStageMedia[stageIndex];
            var target = stages[stageIndex];
            if (target == null) { mediaError = "Dados da fase não encontrados."; yield break; }
            if (files.hasBackground && target.background == null)
            {
                loadingStep = "Baixando prévia do mapa...";
                yield return DownloadStagePreview(stageIndex);
                if (mediaError.Length > 0) yield break;
            }
            if ((files.hasPaintedLayout && target.paintedLayout == null) ||
                (files.hasTerrainAtlas && target.terrainAtlas == null))
            {
                loadingStep = "Baixando mapa...";
                yield return DownloadStageMap(stageIndex);
                if (mediaError.Length > 0) yield break;
            }
            if (files.music.IsAssigned && target.stageMusic == null)
            {
                loadingStep = "Baixando música da fase...";
                var reused = FindLoadedMusic(files.music);
                if (reused != null) target.stageMusic = reused;
                else
                {
                    yield return DownloadStageMusic(files.music, clip => target.stageMusic = clip);
                    if (mediaError.Length > 0) yield break;
                }
            }
            loadingStep = "Preparando inimigos da fase...";
            yield return DownloadStageEnemies(stageIndex);
            if (mediaError.Length > 0) yield break;
            loadingStep = "Carregando objetos da fase...";
            yield return DownloadStageObjects(stageIndex);
            if (mediaError.Length > 0) yield break;
            if (campaignClassicMode && !pendingRestoredVictory)
            {
                loadingStep = "Carregando retratos do grupo...";
                yield return DownloadSelectionPortraits();
                if (mediaError.Length > 0) yield break;
            }
            stageMediaRoutine = null;
            BuildStage();
            ReleaseStageMediaExcept(stageIndex);
        }

        IEnumerator DownloadStagePreview(int index)
        {
            var files = webStageMedia[index];
            // Older catalogs stored the preview and layout in the same bundle.
            if (!files.preview.IsAssigned) { yield return DownloadStageMap(index); yield break; }
            using (var request = UnityWebRequestAssetBundle.GetAssetBundle(
                StageBundleUrl(files.preview), Hash128.Parse(files.preview.hash), 0))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                { mediaError = "Falha ao baixar a prévia: " + request.error; yield break; }
                var bundle = DownloadHandlerAssetBundle.GetContent(request);
                if (bundle == null) { mediaError = "Bundle da prévia inválido."; yield break; }
                try
                {
                    string address = string.IsNullOrEmpty(files.previewAddress) ? "background" : files.previewAddress;
                    var load = bundle.LoadAssetAsync<Sprite>(address);
                    yield return load;
                    stages[index].background = load.asset as Sprite;
                    if (stages[index].background == null) mediaError = "Prévia " + address + " ausente no bundle.";
                }
                finally { bundle.Unload(false); }
            }
        }

        IEnumerator DownloadStageMap(int index)
        {
            var files = webStageMedia[index];
            if (!files.map.IsAssigned) { mediaError = "Bundle do mapa não configurado para a fase " + (index + 1); yield break; }
            using (var request = UnityWebRequestAssetBundle.GetAssetBundle(StageBundleUrl(files.map), Hash128.Parse(files.map.hash), 0))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                { mediaError = "Falha ao baixar o mapa: " + request.error; yield break; }
                var bundle = DownloadHandlerAssetBundle.GetContent(request);
                if (bundle == null) { mediaError = "O bundle do mapa está inválido."; yield break; }
                try
                {
                    bool rasterMap = files.hasBackground && !files.hasPaintedLayout;
                    if (rasterMap)
                    {
                        string address = string.IsNullOrEmpty(files.mapAddress) ? "background" : files.mapAddress;
                        var load = bundle.LoadAssetAsync<Sprite>(address);
                        yield return load;
                        stages[index].background = load.asset as Sprite;
                        if (stages[index].background == null) mediaError = "Imagem do mapa " + address + " ausente no bundle.";
                    }
                    else if (files.hasBackground && !files.preview.IsAssigned)
                    {
                        var load = bundle.LoadAssetAsync<Sprite>("background");
                        yield return load;
                        stages[index].background = load.asset as Sprite;
                        if (stages[index].background == null) mediaError = "Imagem do mapa ausente no bundle.";
                    }
                    if (files.hasPaintedLayout && mediaError.Length == 0)
                    {
                        var load = bundle.LoadAssetAsync<GameObject>("layout");
                        yield return load;
                        var layout = load.asset as GameObject;
                        stages[index].paintedLayout = layout != null ? layout.GetComponent<StagePaintLayout>() : null;
                        if (stages[index].paintedLayout == null) mediaError = "Layout pintável ausente no bundle.";
                    }
                    if (files.hasTerrainAtlas && mediaError.Length == 0)
                    {
                        var load = bundle.LoadAssetAsync<Texture2D>("terrain");
                        yield return load;
                        stages[index].terrainAtlas = load.asset as Texture2D;
                        if (stages[index].terrainAtlas == null) mediaError = "Textura do terreno ausente no bundle.";
                    }
                    if (mediaError.Length == 0 && files.objects.IsAssigned &&
                        files.objects.fileName == files.map.fileName &&
                        files.objectAddresses != null && files.hazardAddresses != null)
                    {
                        var target = stages[index];
                        if (target.objects == null || target.hazards == null ||
                            files.objectAddresses.Length != target.objects.Length ||
                            files.hazardAddresses.Length != target.hazards.Length)
                        { mediaError = "Catálogo de objetos incompleto para a fase " + (index + 1); yield break; }
                        for (int i = 0; i < target.objects.Length; i++)
                        {
                            string address = files.objectAddresses[i];
                            if (string.IsNullOrEmpty(address)) continue;
                            var load = bundle.LoadAssetAsync<GameObject>(address);
                            yield return load;
                            target.objects[i].prefab = load.asset as GameObject;
                            if (target.objects[i].prefab == null)
                            { mediaError = "Objeto " + address + " ausente no bundle da fase."; yield break; }
                        }
                        for (int i = 0; i < target.hazards.Length; i++)
                        {
                            string address = files.hazardAddresses[i];
                            if (string.IsNullOrEmpty(address)) continue;
                            var load = bundle.LoadAssetAsync<GameObject>(address);
                            yield return load;
                            target.hazards[i].prefab = load.asset as GameObject;
                            if (target.hazards[i].prefab == null)
                            { mediaError = "Perigo " + address + " ausente no bundle da fase."; yield break; }
                        }
                    }
                }
                finally { bundle.Unload(false); }
            }
        }

        IEnumerator DownloadStageMusic(StageBundleFile file, Action<AudioClip> assign)
        {
            using (var request = UnityWebRequestAssetBundle.GetAssetBundle(StageBundleUrl(file), Hash128.Parse(file.hash), 0))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                { mediaError = "Falha ao baixar a música: " + request.error; yield break; }
                var bundle = DownloadHandlerAssetBundle.GetContent(request);
                if (bundle == null) { mediaError = "O bundle de música está inválido."; yield break; }
                try
                {
                    var load = bundle.LoadAssetAsync<AudioClip>("music");
                    yield return load;
                    var clip = load.asset as AudioClip;
                    if (clip == null) mediaError = "Faixa de música ausente no bundle.";
                    else assign(clip);
                }
                finally { bundle.Unload(false); }
            }
        }

        IEnumerator DownloadStageObjects(int index)
        {
            var files = webStageMedia[index];
            if (!files.objects.IsAssigned) yield break;
            if (files.map.IsAssigned && files.objects.fileName == files.map.fileName &&
                files.hasBackground && !files.hasPaintedLayout) yield break;
            var target = stages[index];
            if (files.objectAddresses == null || files.hazardAddresses == null ||
                target.objects == null || files.objectAddresses.Length != target.objects.Length ||
                files.hazardAddresses.Length != target.hazards.Length)
            { mediaError = "Catálogo de objetos incompleto para a fase " + (index + 1); yield break; }
            bool missing = false;
            for (int i = 0; i < target.objects.Length; i++)
                missing |= !string.IsNullOrEmpty(files.objectAddresses[i]) && target.objects[i].prefab == null;
            for (int i = 0; i < target.hazards.Length; i++)
                missing |= !string.IsNullOrEmpty(files.hazardAddresses[i]) && target.hazards[i].prefab == null;
            if (!missing) yield break;
            using (var request = UnityWebRequestAssetBundle.GetAssetBundle(StageBundleUrl(files.objects),
                Hash128.Parse(files.objects.hash), 0))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                { mediaError = "Falha ao baixar objetos da fase: " + request.error; yield break; }
                var bundle = DownloadHandlerAssetBundle.GetContent(request);
                if (bundle == null) { mediaError = "Bundle de objetos inválido."; yield break; }
                try
                {
                    for (int i = 0; i < target.objects.Length; i++)
                    {
                        string address = files.objectAddresses[i];
                        if (string.IsNullOrEmpty(address) || target.objects[i].prefab != null) continue;
                        var load = bundle.LoadAssetAsync<GameObject>(address);
                        yield return load;
                        target.objects[i].prefab = load.asset as GameObject;
                        if (target.objects[i].prefab == null)
                        { mediaError = "Objeto " + address + " ausente no bundle."; yield break; }
                    }
                    for (int i = 0; i < target.hazards.Length; i++)
                    {
                        string address = files.hazardAddresses[i];
                        if (string.IsNullOrEmpty(address) || target.hazards[i].prefab != null) continue;
                        var load = bundle.LoadAssetAsync<GameObject>(address);
                        yield return load;
                        target.hazards[i].prefab = load.asset as GameObject;
                        if (target.hazards[i].prefab == null)
                        { mediaError = "Perigo " + address + " ausente no bundle."; yield break; }
                    }
                }
                finally { bundle.Unload(false); }
            }
        }

        IEnumerator DownloadStageEnemies(int index)
        {
            var visualFiles = webStageMedia[index].enemyVisuals;
            var spawns = stages[index].enemies;
            if (visualFiles == null || visualFiles.Length != spawns.Length)
            { mediaError = "Catálogo de inimigos incompleto para a fase " + (index + 1); yield break; }
            for (int e = 0; e < spawns.Length; e++)
            {
                var entry = visualFiles[e];
                if (!entry.bundle.IsAssigned && !entry.modelBundle.IsAssigned)
                {
                    if (entry.hasSprite || entry.hasModel)
                    { mediaError = "Bundle ausente para inimigo da fase " + (index + 1); yield break; }
                    continue;
                }
                if (entry.hasSprite && spawns[e].sprite == null)
                    spawns[e].sprite = FindLoadedEnemySprite(entry.bundle);
                if (entry.hasModel && spawns[e].modelPrefab == null)
                    spawns[e].modelPrefab = FindLoadedEnemyModel(entry.modelBundle.IsAssigned ? entry.modelBundle : entry.bundle,
                        entry.modelAddress);
                if ((!entry.hasSprite || spawns[e].sprite != null) &&
                    (!entry.hasModel || spawns[e].modelPrefab != null)) continue;
                yield return DownloadEnemyVisual(entry, spawns[e]);
                if (mediaError.Length > 0) yield break;
            }
        }

        Sprite FindLoadedEnemySprite(StageBundleFile file)
        {
            if (!file.IsAssigned) return null;
            for (int s = 0; s < stages.Length; s++)
            {
                var entries = webStageMedia[s].enemyVisuals;
                if (entries == null) continue;
                for (int e = 0; e < entries.Length; e++)
                {
                    var spawn = stages[s].enemies[e];
                    if (entries[e].hasSprite && entries[e].bundle.fileName == file.fileName && spawn.sprite != null)
                        return spawn.sprite;
                }
            }
            return null;
        }

        PS2EnemyVisual FindLoadedEnemyModel(StageBundleFile file, string address)
        {
            if (!file.IsAssigned) return null;
            for (int s = 0; s < stages.Length; s++)
            {
                var entries = webStageMedia[s].enemyVisuals;
                if (entries == null) continue;
                for (int e = 0; e < entries.Length; e++)
                {
                    var entry = entries[e];
                    var spawn = stages[s].enemies[e];
                    string entryAddress = string.IsNullOrEmpty(entry.modelAddress) ? "model" : entry.modelAddress;
                    if (entry.hasModel && (entry.modelBundle.IsAssigned ? entry.modelBundle.fileName : entry.bundle.fileName) == file.fileName &&
                        entryAddress == address && spawn.modelPrefab != null) return spawn.modelPrefab;
                }
            }
            return null;
        }

        IEnumerator DownloadEnemyVisual(EnemyVisualFiles files, EnemySpawn spawn)
        {
            var modelFile = files.modelBundle.IsAssigned ? files.modelBundle : files.bundle;
            bool modelInPrimaryBundle = files.modelBundle.IsAssigned && files.bundle.IsAssigned &&
                files.modelBundle.fileName == files.bundle.fileName;
            if (files.bundle.IsAssigned && ((files.hasSprite && spawn.sprite == null) ||
                (files.hasModel && modelInPrimaryBundle && spawn.modelPrefab == null)))
            {
                using (var request = UnityWebRequestAssetBundle.GetAssetBundle(StageBundleUrl(files.bundle),
                    Hash128.Parse(files.bundle.hash), 0))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    { mediaError = "Falha ao baixar inimigo: " + request.error; yield break; }
                    var bundle = DownloadHandlerAssetBundle.GetContent(request);
                    if (bundle == null) { mediaError = "O bundle de inimigo está inválido."; yield break; }
                    try
                    {
                        if (files.hasSprite && spawn.sprite == null)
                        {
                            var load = bundle.LoadAssetAsync<Sprite>("sprite");
                            yield return load;
                            spawn.sprite = load.asset as Sprite;
                            if (spawn.sprite == null) mediaError = "Sprite de inimigo ausente no bundle.";
                        }
                        if (files.hasModel && modelInPrimaryBundle && spawn.modelPrefab == null && mediaError.Length == 0)
                            yield return LoadEnemyModel(bundle, spawn, files.modelAddress);
                    }
                    finally { bundle.Unload(false); }
                }
            }
            if (files.hasModel && spawn.modelPrefab == null && mediaError.Length == 0)
            {
                using (var request = UnityWebRequestAssetBundle.GetAssetBundle(StageBundleUrl(modelFile),
                    Hash128.Parse(modelFile.hash), 0))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    { mediaError = "Falha ao baixar modelo de inimigo: " + request.error; yield break; }
                    var bundle = DownloadHandlerAssetBundle.GetContent(request);
                    if (bundle == null) { mediaError = "O bundle do modelo de inimigo está inválido."; yield break; }
                    try
                    {
                        yield return LoadEnemyModel(bundle, spawn, files.modelAddress);
                    }
                    finally { bundle.Unload(false); }
                }
            }
        }

        IEnumerator LoadEnemyModel(AssetBundle bundle, EnemySpawn spawn, string address)
        {
            address = string.IsNullOrEmpty(address) ? "model" : address;
            var load = bundle.LoadAssetAsync<GameObject>(address);
            yield return load;
            var prefab = load.asset as GameObject;
            spawn.modelPrefab = prefab != null ? prefab.GetComponentInChildren<PS2EnemyVisual>(true) : null;
            if (spawn.modelPrefab == null) mediaError = "Modelo de inimigo " + address + " ausente no bundle.";
        }

        IEnumerator DownloadSelectionPortraits(bool onlyAwaiting = false)
        {
            if (webCharacterPortraits == null) yield break;
            var seen = new HashSet<CharacterDefinition>();
            foreach (var player in players)
            {
                if (onlyAwaiting && !player.awaitingSelection) continue;
                foreach (var member in player.party)
                {
                    var hero = member.definition;
                    if (hero == null || member.lost || (onlyAwaiting && member.escaped) || !seen.Add(hero)) continue;
                    var file = PortraitFile(hero);
                    if (!file.IsAssigned || hero.hudPortrait != null) continue;
                    yield return DownloadPortrait(file, hero, PortraitAddress(hero));
                    if (mediaError.Length > 0) yield break;
                }
            }
        }

        StageBundleFile PortraitFile(CharacterDefinition hero)
        {
            if (webCharacterPortraits == null) return default;
            foreach (var entry in webCharacterPortraits)
                if (entry.character == hero) return entry.portrait;
            return default;
        }

        string PortraitAddress(CharacterDefinition hero)
        {
            if (webCharacterPortraits == null) return "portrait";
            foreach (var entry in webCharacterPortraits)
                if (entry.character == hero) return string.IsNullOrEmpty(entry.portraitAddress) ? "portrait" : entry.portraitAddress;
            return "portrait";
        }

        IEnumerator DownloadPortrait(StageBundleFile file, CharacterDefinition hero, string address)
        {
            using (var request = UnityWebRequestAssetBundle.GetAssetBundle(StageBundleUrl(file), Hash128.Parse(file.hash), 0))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                { mediaError = "Falha ao baixar retrato: " + request.error; yield break; }
                var bundle = DownloadHandlerAssetBundle.GetContent(request);
                if (bundle == null) { mediaError = "O bundle de retrato está inválido."; yield break; }
                try
                {
                    var load = bundle.LoadAssetAsync<Texture2D>(address);
                    yield return load;
                    hero.hudPortrait = load.asset as Texture2D;
                    if (hero.hudPortrait == null)
                    {
                        var spriteLoad = bundle.LoadAssetAsync<Sprite>(address);
                        yield return spriteLoad;
                        var portraitSprite = spriteLoad.asset as Sprite;
                        if (portraitSprite != null) hero.hudPortrait = portraitSprite.texture;
                    }
                    if (hero.hudPortrait == null) mediaError = "Retrato ausente no bundle.";
                }
                finally { bundle.Unload(false); }
            }
        }

        void BeginSelectionPortraitLoad()
        {
            if (!streamStageMedia || selectionPortraitsLoading) return;
            mediaError = "";
            selectionPortraitsLoading = true;
            selectionPortraitRoutine = StartCoroutine(LoadSelectionPortraitsWhilePlaying());
        }

        IEnumerator LoadSelectionPortraitsWhilePlaying()
        {
            yield return DownloadSelectionPortraits(true);
            if (mediaError.Length > 0) Debug.LogWarning(mediaError, this);
            selectionPortraitsLoading = false;
            selectionPortraitRoutine = null;
        }

        void ReleaseSelectionPortraits()
        {
            if (!streamStageMedia || webCharacterPortraits == null) return;
            bool released = false;
            foreach (var entry in webCharacterPortraits)
                if (entry.character != null && entry.character.hudPortrait != null)
                { entry.character.hudPortrait = null; released = true; }
            if (released)
            {
                RequestUnloadReleasedMedia();
            }
        }

        void ReleasePortraitsOutsideSelection()
        {
            if (!streamStageMedia || webCharacterPortraits == null) return;
            var needed = new HashSet<CharacterDefinition>();
            foreach (var player in players)
            {
                if (player.HasActive) needed.Add(player.party[player.active].definition);
                if (player.awaitingSelection)
                    foreach (var member in player.party)
                        if (!member.lost && !member.escaped) needed.Add(member.definition);
            }
            bool released = false;
            foreach (var entry in webCharacterPortraits)
                if (entry.character != null && entry.character.hudPortrait != null && !needed.Contains(entry.character))
                { entry.character.hudPortrait = null; released = true; }
            if (released)
            {
                RequestUnloadReleasedMedia();
            }
        }

        AudioClip FindLoadedMusic(StageBundleFile file)
        {
            for (int i = 0; i < stages.Length; i++)
            {
                if (webStageMedia[i].music.fileName == file.fileName && stages[i].stageMusic != null)
                    return stages[i].stageMusic;
                if (webStageMedia[i].victoryMusic.fileName == file.fileName && stages[i].victoryMusic != null)
                    return stages[i].victoryMusic;
            }
            return null;
        }

        void BeginVictoryMusicDownload()
        {
            if (!streamStageMedia || stage == null || stage.victoryMusic != null ||
                webStageMedia == null || stageIndex >= webStageMedia.Length) return;
            var file = webStageMedia[stageIndex].victoryMusic;
            if (!file.IsAssigned) return;
            var reused = FindLoadedMusic(file);
            if (reused != null) { stage.victoryMusic = reused; return; }
            if (victoryMusicRoutine != null) StopCoroutine(victoryMusicRoutine);
            victoryMusicRoutine = StartCoroutine(DownloadVictoryMusic(stage, file));
        }

        IEnumerator DownloadVictoryMusic(StageDefinition target, StageBundleFile file)
        {
            mediaError = "";
            yield return DownloadStageMusic(file, clip => target.victoryMusic = clip);
            if (mediaError.Length > 0) Debug.LogWarning(mediaError, this);
            victoryMusicRoutine = null;
        }

        string StageBundleUrl(StageBundleFile file)
        {
#if UNITY_EDITOR
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/WebGLStreaming/Content", file.fileName));
            return new Uri(path).AbsoluteUri;
#else
            return new Uri(new Uri(Application.absoluteURL), "Content/" + file.fileName).AbsoluteUri;
#endif
        }

        void UpdateStagePreview()
        {
            if (!streamStageMedia) return;
            if (state != ScreenState.StageSelect)
            {
                if (previewStageIndex >= 0 || pendingPreviewStageIndex >= 0)
                { CancelStageMediaRequests(); ReleaseStageMediaExcept(-1); }
                return;
            }
            if (webStageMedia == null || webStageMedia.Length != stages.Length) return;
            if (pendingPreviewStageIndex != stageSelectIndex)
            {
                if (stagePreviewRoutine != null) { StopCoroutine(stagePreviewRoutine); stagePreviewRoutine = null; }
                pendingPreviewStageIndex = stageSelectIndex;
                previewChangeTime = Time.unscaledTime;
            }
            if (previewStageIndex == stageSelectIndex || stagePreviewRoutine != null ||
                Time.unscaledTime - previewChangeTime < StagePreviewDelay) return;
            previewStageIndex = stageSelectIndex;
            mediaError = "";
            stagePreviewRoutine = StartCoroutine(LoadStagePreview(stageSelectIndex));
        }

        IEnumerator LoadStagePreview(int index)
        {
            // Keep this routine alive until the next frame so rapid carousel changes can cancel it.
            yield return null;
            var files = webStageMedia[index];
            if (files.hasBackground && stages[index].background == null)
                yield return DownloadStagePreview(index);
            stagePreviewRoutine = null;
            if (state == ScreenState.StageSelect && stageSelectIndex == index)
                ReleaseStageMediaExcept(index);
        }

        void CancelStageMediaRequests()
        {
            if (stageMediaRoutine != null) { StopCoroutine(stageMediaRoutine); stageMediaRoutine = null; }
            if (stagePreviewRoutine != null) { StopCoroutine(stagePreviewRoutine); stagePreviewRoutine = null; }
            if (victoryMusicRoutine != null) { StopCoroutine(victoryMusicRoutine); victoryMusicRoutine = null; }
            if (selectionPortraitRoutine != null) { StopCoroutine(selectionPortraitRoutine); selectionPortraitRoutine = null; }
            selectionPortraitsLoading = false;
            previewStageIndex = -1;
            pendingPreviewStageIndex = -1;
        }

        void ReleaseStageMediaExcept(int keepIndex)
        {
            if (!streamStageMedia || webStageMedia == null) return;
            bool released = false;
            for (int i = 0; i < stages.Length; i++)
            {
                if (i == keepIndex || stages[i] == null) continue;
                var item = stages[i];
                bool hasMedia = item.background != null || item.paintedLayout != null || item.terrainAtlas != null ||
                    item.stageMusic != null || item.victoryMusic != null;
                if (item.objects != null)
                    foreach (var visual in item.objects)
                        if (visual != null) { hasMedia |= visual.prefab != null; visual.prefab = null; }
                if (item.hazards != null)
                    foreach (var visual in item.hazards)
                        if (visual != null) { hasMedia |= visual.prefab != null; visual.prefab = null; }
                foreach (var enemy in item.enemies)
                {
                    hasMedia |= enemy.sprite != null || enemy.modelPrefab != null;
                    enemy.sprite = null; enemy.modelPrefab = null;
                }
                if (!hasMedia) continue;
                item.background = null; item.paintedLayout = null; item.terrainAtlas = null; item.stageMusic = null; item.victoryMusic = null;
                released = true;
            }
            if (released)
            {
                effectPrefabSprites.Clear();
                RequestUnloadReleasedMedia();
            }
        }

        void RequestUnloadReleasedMedia()
        {
            unloadMediaPending = true;
            lastMediaReleaseTime = Time.unscaledTime;
            ResumePendingMediaUnload();
        }

        void ResumePendingMediaUnload()
        {
            if (unloadMediaPending && isActiveAndEnabled && unloadMediaRoutine == null)
                unloadMediaRoutine = StartCoroutine(UnloadReleasedMedia());
        }

        void SuspendMediaUnload()
        {
            if (unloadMediaRoutine == null) return;
            StopCoroutine(unloadMediaRoutine);
            unloadMediaRoutine = null;
        }

        IEnumerator UnloadReleasedMedia()
        {
            do
            {
                while (Time.unscaledTime - lastMediaReleaseTime < MediaUnloadIdleSeconds ||
                    stageMediaRoutine != null)
                    yield return null;
                unloadMediaPending = false;
                yield return Resources.UnloadUnusedAssets();
            } while (unloadMediaPending);
            unloadMediaRoutine = null;
        }

        void DrawStageLoading(Rect bounds)
        {
            DrawSelectionPanel(bounds);
            GUI.Label(new Rect(bounds.x + 25, bounds.y + 30, bounds.width - 50, 55), "CARREGANDO FASE", SelectionHeading);
            GUI.Label(new Rect(bounds.x + 30, bounds.y + 95, bounds.width - 60, 95),
                string.IsNullOrEmpty(mediaError) ? loadingStep : mediaError, SelectionHeading);
            if (string.IsNullOrEmpty(mediaError)) return;
            if (IronButton(new Rect(bounds.x + 75, bounds.yMax - 105, bounds.width - 150, 43), "TENTAR NOVAMENTE")) LoadStage();
            if (IronButton(new Rect(bounds.x + 75, bounds.yMax - 55, bounds.width - 150, 43), "MENU PRINCIPAL")) ReturnToMenu();
        }
    }
}
