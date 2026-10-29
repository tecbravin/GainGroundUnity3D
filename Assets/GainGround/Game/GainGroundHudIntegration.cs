using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        GainGroundHudView hudView;
        Coroutine hudPortraitRoutine;
        readonly HashSet<CharacterDefinition> requestedHudPortraits = new HashSet<CharacterDefinition>();
        float nextHudRefreshTime;
        float nextEventSystemScanTime;
        StageDefinition lastHudStage;

        void EnsureInputSystemEventSystem(UnityEngine.EventSystems.EventSystem eventSystem)
        {
            if (Time.unscaledTime < nextEventSystemScanTime) return;
            nextEventSystemScanTime = Time.unscaledTime + .5f;
            foreach (var system in Resources.FindObjectsOfTypeAll<UnityEngine.EventSystems.EventSystem>())
            {
                if (system == null || !system.gameObject.scene.IsValid()) continue;
                var legacyModule = system.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                var inputModule = system.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                bool inputSystemDevicesAvailable = UnityEngine.InputSystem.Keyboard.current != null || UnityEngine.InputSystem.Mouse.current != null;
                if (!inputSystemDevicesAvailable)
                {
                    if (inputModule != null) inputModule.enabled = false;
                    if (!system.gameObject.activeInHierarchy) continue;
                    if (legacyModule == null) legacyModule = system.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                    legacyModule.enabled = true;
                    continue;
                }
                if (legacyModule != null)
                {
                    legacyModule.enabled = false;
                    Destroy(legacyModule);
                }

                if (!system.gameObject.activeInHierarchy) continue;

                inputModule = system.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (inputModule == null)
                    inputModule = system.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                inputModule.enabled = true;
            }
        }

        void RestoreInputSystemUIActions(UnityEngine.EventSystems.EventSystem eventSystem)
        {
            var inputModule = eventSystem != null
                ? eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>()
                : null;
            if (inputModule != null) inputModule.AssignDefaultActions();
        }
        void InitializeHud()
        {
            var existingEventSystem = FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            EnsureInputSystemEventSystem(existingEventSystem);
            var prefab = Resources.Load<GameObject>("HUD/GainGroundHUD");
            if (prefab == null || prefab.GetComponent<GainGroundHudView>() == null)
            {
                Debug.LogError("HUD/GainGroundHUD.prefab não encontrado em Resources.", this);
                return;
            }
            var instance = Instantiate(prefab);
            if (existingEventSystem != null)
            {
                var ownEventSystem = instance.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>();
                if (ownEventSystem != null) ownEventSystem.gameObject.SetActive(false);
                RestoreInputSystemUIActions(existingEventSystem);
            }
            hudView = instance.GetComponent<GainGroundHudView>();
            instance.name = "Gain Ground HUD";
            hudView.Bind(StepHudSelection, ConfirmHudSelection, ToggleHudPause);
            hudView.gameObject.SetActive(false);
        }

        void StepHudSelection(int playerId, int direction)
        {
            if (state != ScreenState.Playing || paused || stageIntroPlaying || playerId >= players.Count) return;
            var player = players[playerId];
            if (!player.awaitingSelection || selectionPortraitsLoading || player.party.Count == 0) return;
            player.selectionIndex = CarouselNeighbor(player, player.selectionIndex, direction);
        }

        void ConfirmHudSelection(int playerId)
        {
            if (stageIntroPlaying || playerId >= players.Count) return;
            var player = players[playerId];
            ChooseCharacter(player, player.selectionIndex);
        }

        void ToggleHudPause()
        {
            if (state != ScreenState.Playing || stageIntroPlaying) return;
            paused = !paused;
            ResetMobileInput();
        }

        void RefreshHud()
        {
            if (hudView == null) return;
            bool visible = stage != null && state != ScreenState.Title &&
                state != ScreenState.Options && state != ScreenState.StageSelect &&
                state != ScreenState.Loading && !endingAnimationPlaying &&
                !(state == ScreenState.Victory && !pendingEndingScore);
            if (hudView.gameObject.activeSelf != visible) hudView.gameObject.SetActive(visible);
            if (!visible) { lastHudStage = null; return; }

            if (lastHudStage == stage && Time.unscaledTime < nextHudRefreshTime) return;
            lastHudStage = stage;
            nextHudRefreshTime = Time.unscaledTime + .1f;

            hudView.UpdateLayout(gameCamera, stage.size);
            hudView.SetMobilePresentation(UsesMobileControls);
            LoadCurrentHudPortraits();
            hudView.SetHeader(Mathf.CeilToInt(remainingTime), enemies.Count, score, stage.displayName);
            for (int id = 0; id < 2; id++)
            {
                if (id >= players.Count)
                {
                    hudView.SetPlayer(id, "P" + (id + 1) + "  AGUARDANDO", "",
                        -1, -1, null, null, 0, 0, false,
                        false, UsesMobileControls && id == 0, false, false);
                    continue;
                }
                var player = players[id];
                int selected = player.awaitingSelection ? player.selectionIndex : player.active;
                if (player.awaitingSelection && (selected < 0 || selected >= player.party.Count ||
                    player.party[selected].lost || player.party[selected].escaped))
                {
                    selected = CarouselNeighbor(player, selected, 1);
                    player.selectionIndex = selected;
                }
                var hero = selected >= 0 && selected < player.party.Count ?
                    player.party[selected].definition : null;
                int available = 0, escaped = 0;
                foreach (var member in player.party)
                {
                    if (member.escaped) escaped++;
                    else if (!member.lost) available++;
                }
                float selectionWait = Mathf.Max(0, player.selectionReadyAt - Time.unscaledTime);
                string name = player.awaitingSelection && selectionWait > 0 ?
                    "P" + (id + 1) + "  AGUARDE " + selectionWait.ToString("F1") + "s" :
                    hero != null ? (player.awaitingSelection ? "P" + (id + 1) : "") :
                    "P" + (id + 1) + "  SEM PERSONAGEM";
                float cooldown = player.HasActive ? player.party[player.active].dashCooldown : 0;
                bool canDash = hero != null && hero.Distancia > 0;
                float ready = !canDash ? 0 : hero.TempodeCooldown <= 0 ? 1 :
                    1 - Mathf.Clamp01(cooldown / hero.TempodeCooldown);
                float specialDuration = hero != null && hero.specialWeapon != null &&
                    !string.IsNullOrEmpty(hero.specialWeapon.label) ? hero.specialWeapon.cooldown : 1.2f;
                float specialReady = !player.HasActive ? 0 : specialDuration <= 0 ? 1 :
                    1 - Mathf.Clamp01(player.specialCooldown / specialDuration);
                hudView.SetPlayer(id, name, hero != null ? hero.displayName : "",
                    available, escaped,
                    hero != null ? hero.hudPortrait : null,
                    hero != null ? hero.sprite : null, ready, specialReady, canDash,
                    player.awaitingSelection && state == ScreenState.Playing && !paused && !stageIntroPlaying,
                    UsesMobileControls && id == 0 && state == ScreenState.Playing && !paused && !stageIntroPlaying,
                    !ClassicMode, selectionWait <= 0 && !selectionPortraitsLoading);
            }
        }

        void LoadCurrentHudPortraits()
        {
            if (!streamStageMedia || hudPortraitRoutine != null || selectionPortraitsLoading) return;
            foreach (var player in players)
            {
                if (!player.HasActive) continue;
                var hero = player.party[player.active].definition;
                if (hero == null || hero.hudPortrait != null || requestedHudPortraits.Contains(hero)) continue;
                var file = PortraitFile(hero);
                if (!file.IsAssigned) continue;
                requestedHudPortraits.Add(hero);
                hudPortraitRoutine = StartCoroutine(LoadCurrentHudPortrait(hero, file));
                return;
            }
        }

        IEnumerator LoadCurrentHudPortrait(CharacterDefinition hero, StageBundleFile file)
        {
            yield return DownloadPortrait(file, hero, PortraitAddress(hero));
            if (hero != null && hero.hudPortrait != null) requestedHudPortraits.Remove(hero);
            hudPortraitRoutine = null;
        }

        void CancelHudPortraitLoad()
        {
            if (hudPortraitRoutine != null) StopCoroutine(hudPortraitRoutine);
            hudPortraitRoutine = null;
            requestedHudPortraits.Clear();
        }
    }
}
