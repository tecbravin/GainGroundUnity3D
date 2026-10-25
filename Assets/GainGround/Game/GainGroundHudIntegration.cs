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
        StageDefinition lastHudStage;

        void InitializeHud()
        {
            var existingEventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
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
            }
            hudView = instance.GetComponent<GainGroundHudView>();
            instance.name = "Gain Ground HUD";
            hudView.Bind(StepHudSelection, ConfirmHudSelection, ToggleHudPause);
            hudView.gameObject.SetActive(false);
        }

        void StepHudSelection(int playerId, int direction)
        {
            if (state != ScreenState.Playing || paused || playerId >= players.Count) return;
            var player = players[playerId];
            if (!player.awaitingSelection || selectionPortraitsLoading || player.party.Count == 0) return;
            player.selectionIndex = CarouselNeighbor(player, player.selectionIndex, direction);
        }

        void ConfirmHudSelection(int playerId)
        {
            if (playerId >= players.Count) return;
            var player = players[playerId];
            ChooseCharacter(player, player.selectionIndex);
        }

        void ToggleHudPause()
        {
            if (state != ScreenState.Playing) return;
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
                        "ESPECIAL PARA ENTRAR", "DASH", null, null, 0,
                        false, UsesMobileControls && id == 0, false);
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
                string name = player.awaitingSelection ? "P" + (id + 1) + "  ESCOLHA" :
                    hero != null ? "P" + (id + 1) + "  " + hero.displayName :
                    "P" + (id + 1) + "  SEM PERSONAGEM";
                string roster = "EQUIPE " + available + "  |  SALVOS " + escaped;
                float cooldown = player.HasActive ? player.party[player.active].dashCooldown : 0;
                float ready = hero == null ? 0 : hero.TempodeCooldown <= 0 ? 1 :
                    1 - Mathf.Clamp01(cooldown / hero.TempodeCooldown);
                string dash = cooldown > 0 ? "DASH  " + cooldown.ToString("F1") + "s" : "DASH PRONTO";
                hudView.SetPlayer(id, name, hero != null ? hero.displayName : "", roster, dash,
                    hero != null ? hero.hudPortrait : null,
                    hero != null ? hero.sprite : null, ready,
                    player.awaitingSelection && state == ScreenState.Playing && !paused,
                    UsesMobileControls && id == 0 && state == ScreenState.Playing && !paused,
                    !ClassicMode);
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
            yield return DownloadPortrait(file, hero);
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
