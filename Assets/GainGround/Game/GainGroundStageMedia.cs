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
        Coroutine stageMediaRoutine, stagePreviewRoutine, victoryMusicRoutine, selectionPortraitRoutine;
        int previewStageIndex = -1;
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
            if ((files.hasBackground && target.background == null) ||
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
                    if (files.hasBackground)
                    {
                        var load = bundle.LoadAssetAsync<Sprite>("background");
                        yield return load;
                        stages[index].background = load.asset as Sprite;
                        if (stages[index].background == null) mediaError = "Imagem do mapa ausente no bundle.";
                    }
                    if (files.hasTerrainAtlas && mediaError.Length == 0)
                    {
                        var load = bundle.LoadAssetAsync<Texture2D>("terrain");
                        yield return load;
                        stages[index].terrainAtlas = load.asset as Texture2D;
                        if (stages[index].terrainAtlas == null) mediaError = "Textura do terreno ausente no bundle.";
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

        IEnumerator DownloadStageEnemies(int index)
        {
            var visualFiles = webStageMedia[index].enemyVisuals;
            var spawns = stages[index].enemies;
            if (visualFiles == null || visualFiles.Length != spawns.Length)
            { mediaError = "Catálogo de inimigos incompleto para a fase " + (index + 1); yield break; }
            for (int e = 0; e < spawns.Length; e++)
            {
                var entry = visualFiles[e];
                if (!entry.bundle.IsAssigned)
                {
                    if (entry.hasSprite || entry.hasModel)
                    { mediaError = "Bundle ausente para inimigo da fase " + (index + 1); yield break; }
                    continue;
                }
                if ((!entry.hasSprite || spawns[e].sprite != null) &&
                    (!entry.hasModel || spawns[e].modelPrefab != null)) continue;
                var reused = FindLoadedEnemy(entry.bundle);
                if (reused != null)
                {
                    spawns[e].sprite = reused.sprite;
                    spawns[e].modelPrefab = reused.modelPrefab;
                    continue;
                }
                yield return DownloadEnemyVisual(entry, spawns[e]);
                if (mediaError.Length > 0) yield break;
            }
        }

        EnemySpawn FindLoadedEnemy(StageBundleFile file)
        {
            for (int s = 0; s < stages.Length; s++)
            {
                var entries = webStageMedia[s].enemyVisuals;
                if (entries == null) continue;
                for (int e = 0; e < entries.Length; e++)
                {
                    var spawn = stages[s].enemies[e];
                    if (entries[e].bundle.fileName == file.fileName &&
                        (!entries[e].hasSprite || spawn.sprite != null) &&
                        (!entries[e].hasModel || spawn.modelPrefab != null)) return spawn;
                }
            }
            return null;
        }

        IEnumerator DownloadEnemyVisual(EnemyVisualFiles files, EnemySpawn spawn)
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
                    if (files.hasSprite)
                    {
                        var load = bundle.LoadAssetAsync<Sprite>("sprite");
                        yield return load;
                        spawn.sprite = load.asset as Sprite;
                        if (spawn.sprite == null) mediaError = "Sprite de inimigo ausente no bundle.";
                    }
                    if (files.hasModel && mediaError.Length == 0)
                    {
                        var load = bundle.LoadAssetAsync<GameObject>("model");
                        yield return load;
                        var prefab = load.asset as GameObject;
                        spawn.modelPrefab = prefab != null ? prefab.GetComponentInChildren<PS2EnemyVisual>(true) : null;
                        if (spawn.modelPrefab == null) mediaError = "Modelo de inimigo ausente no bundle.";
                    }
                }
                finally { bundle.Unload(false); }
            }
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
                    yield return DownloadPortrait(file, hero);
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

        IEnumerator DownloadPortrait(StageBundleFile file, CharacterDefinition hero)
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
                    var load = bundle.LoadAssetAsync<Texture2D>("portrait");
                    yield return load;
                    hero.hudPortrait = load.asset as Texture2D;
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
            if (released && isActiveAndEnabled) StartCoroutine(UnloadReleasedMedia());
        }

        void ReleasePortraitsOutsideSelection()
        {
            if (!streamStageMedia || webCharacterPortraits == null) return;
            var needed = new HashSet<CharacterDefinition>();
            foreach (var player in players)
                if (player.awaitingSelection)
                    foreach (var member in player.party)
                        if (!member.lost && !member.escaped) needed.Add(member.definition);
            bool released = false;
            foreach (var entry in webCharacterPortraits)
                if (entry.character != null && entry.character.hudPortrait != null && !needed.Contains(entry.character))
                { entry.character.hudPortrait = null; released = true; }
            if (released && isActiveAndEnabled) StartCoroutine(UnloadReleasedMedia());
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
                if (previewStageIndex >= 0) { CancelStageMediaRequests(); ReleaseStageMediaExcept(-1); }
                return;
            }
            if (webStageMedia == null || webStageMedia.Length != stages.Length || previewStageIndex == stageSelectIndex) return;
            if (stagePreviewRoutine != null) { StopCoroutine(stagePreviewRoutine); stagePreviewRoutine = null; }
            previewStageIndex = stageSelectIndex;
            mediaError = "";
            ReleaseStageMediaExcept(stageSelectIndex);
            stagePreviewRoutine = StartCoroutine(LoadStagePreview(stageSelectIndex));
        }

        IEnumerator LoadStagePreview(int index)
        {
            var files = webStageMedia[index];
            if (files.hasBackground && stages[index].background == null)
                yield return DownloadStageMap(index);
            if (mediaError.Length == 0 && files.music.IsAssigned && stages[index].stageMusic == null)
            {
                var reused = FindLoadedMusic(files.music);
                if (reused != null) stages[index].stageMusic = reused;
                else yield return DownloadStageMusic(files.music, clip => stages[index].stageMusic = clip);
            }
            stagePreviewRoutine = null;
        }

        void CancelStageMediaRequests()
        {
            if (stageMediaRoutine != null) { StopCoroutine(stageMediaRoutine); stageMediaRoutine = null; }
            if (stagePreviewRoutine != null) { StopCoroutine(stagePreviewRoutine); stagePreviewRoutine = null; }
            if (victoryMusicRoutine != null) { StopCoroutine(victoryMusicRoutine); victoryMusicRoutine = null; }
            if (selectionPortraitRoutine != null) { StopCoroutine(selectionPortraitRoutine); selectionPortraitRoutine = null; }
            selectionPortraitsLoading = false;
            previewStageIndex = -1;
        }

        void ReleaseStageMediaExcept(int keepIndex)
        {
            if (!streamStageMedia || webStageMedia == null) return;
            bool released = false;
            for (int i = 0; i < stages.Length; i++)
            {
                if (i == keepIndex || stages[i] == null) continue;
                var item = stages[i];
                bool hasMedia = item.background != null || item.terrainAtlas != null ||
                    item.stageMusic != null || item.victoryMusic != null;
                foreach (var enemy in item.enemies)
                {
                    hasMedia |= enemy.sprite != null || enemy.modelPrefab != null;
                    enemy.sprite = null; enemy.modelPrefab = null;
                }
                if (!hasMedia) continue;
                item.background = null; item.terrainAtlas = null; item.stageMusic = null; item.victoryMusic = null;
                released = true;
            }
            if (released && isActiveAndEnabled) StartCoroutine(UnloadReleasedMedia());
        }

        IEnumerator UnloadReleasedMedia()
        {
            yield return null;
            yield return Resources.UnloadUnusedAssets();
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
