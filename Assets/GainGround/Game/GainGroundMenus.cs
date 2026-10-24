using System;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        LocalSettings settings = new LocalSettings();
        LocalSettings draftSettings;
        string settingsPath;
        string settingsError = "";
        int menuIndex, optionsIndex;
        LocalSettings AudioSettings => state == ScreenState.Options && draftSettings != null ? draftSettings : settings;

        void ApplyAudioVolumes()
        {
            if (musicSource != null) musicSource.volume = (stage != null ? stage.musicVolume : 1) * AudioSettings.BGM / 100f;
            if (sfxSource != null) sfxSource.volume = AudioSettings.SFX / 100f;
        }
        AudioSource sfxSource;
        [Header("Default sound effects (used when actor sound is empty)")]
        public AudioClip shotSound, hitSound, rescueSound;
        public AudioClip dashSound, impactSound, explosionSound, switchSound;
        public AudioClip secretUnlockSound;
        [Header("Title screen UI")]
        public AudioClip titlePressSound;
        int lastTitlePressFrame = -1;
        float nextImpactSound;
        int gainLogoClicks, secretF11Presses;
        bool WaitingForSelection => players.Exists(p => p.awaitingSelection) && !players.Exists(p => p.HasActive);

        void LoadSettings()
        {
            settingsPath = LocalSettings.FilePath;
            try { settings = LocalSettings.Load(settingsPath); }
            catch (Exception e)
            {
                settings = new LocalSettings();
                settingsError = "Could not read localSettings.json. Using defaults. " + e.Message;
                Debug.LogWarning(settingsError);
            }
        }
        public void StartGame(int count)
        {
            ResetMobileInput();
            playerCount = Mathf.Clamp(count, 1, 2);
            NewCampaign();
        }
        void StartNewGameFromMenu(int count)
        {
            if (!hasFinished) { StartGame(count); return; }
            ResetMobileInput();
            pendingPlayerCount = playerCount = Mathf.Clamp(count, 1, 2);
            stageSelectIndex = 0;
            state = ScreenState.StageSelect;
        }
        void StartAtSelectedStage(int index)
        {
            playerCount = pendingPlayerCount;
            NewCampaign(index);
        }
        void ReturnToMenu()
        {
            CancelStageMediaRequests();
            CancelHudPortraitLoad();
            pendingRestoredVictory = false;
            DestroyEndingAnimation();
            ResetMobileInput(); saveBrowserOpen=false; RefreshSaves();
            ClearShots();
            if (arena != null) { arena.gameObject.SetActive(false); Destroy(arena.gameObject); }
            if (gameCamera != null) gameCamera.rect = new Rect(0, 0, 1, 1);
            stage = null; players.Clear(); enemies.Clear(); blastVisuals.Clear(); extraCaptives.Clear(); hazardStates.Clear();
            paused = false; state = ScreenState.Title; menuIndex = 0; ShowTitleMenuImmediately();
            if (musicSource != null) musicSource.Stop();
            if (musicSource != null) musicSource.clip = null;
            if (sfxSource != null) sfxSource.Stop();
            ReleaseStageMediaExcept(-1);
            ReleaseSelectionPortraits();
        }
        void OpenOptions()
        {
            bindingsOpen=false;captureAction=-1;
            mobileScroll = Vector2.zero;
            draftSettings = settings.Copy(); settingsError = "";
            optionsIndex = 0; state = ScreenState.Options;
        }
        void SaveOptions()
        {
            try
            {
                draftSettings.Clamp();
                draftSettings.Save(settingsPath ?? LocalSettings.FilePath);
                settings = draftSettings.Copy(); draftSettings = null;
                settingsError = ""; state = ScreenState.Title;
                ApplyAudioVolumes();
            }
            catch (Exception e) { settingsError = "Could not save settings: " + e.Message; }
        }
        void DiscardOptions()
        {
            draftSettings = null; settingsError = ""; state = ScreenState.Title;
            ApplyAudioVolumes();
        }
        void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        void PlaySfx(AudioClip clip)
        {
            if (clip == null) return;
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false; sfxSource.spatialBlend = 0;
            }
            sfxSource.volume = AudioSettings.SFX / 100f;
            sfxSource.PlayOneShot(clip);
        }
        void PlayTitlePressSound()
        {
            if (lastTitlePressFrame == Time.frameCount) return;
            lastTitlePressFrame = Time.frameCount;
            PlaySfx(titlePressSound);
        }
        void ActivateMenuItem()
        {
            var labels=TitleMenuLabels;
            if(menuIndex<0||menuIndex>=labels.Length)return;
            string selected = labels[menuIndex];
            if (selected == "Solo" || selected == "Duo" || selected == "Continue" || selected == "Menu")
                PlayTitlePressSound();
            switch(selected)
            {
                case "Solo": StartNewGameFromMenu(1); break;
                case "Duo": StartNewGameFromMenu(2); break;
                case "Continue": OpenSaveSlots(false); break;
                case "Menu": OpenOptions(); break;
                case "Sair": ExitGame(); break;
            }
        }

        void CountSecretF11Press()
        {
            if (hasFinished) return;
            if (++secretF11Presses < 5) return;
            secretF11Presses = 0;
            UnlockSecretFinish();
        }

        void CountSecretGainClick()
        {
            if (++gainLogoClicks < 5) return;
            gainLogoClicks = 0;
            if (hasFinished) PlaySfx(secretUnlockSound);
            else UnlockSecretFinish();
        }

        void UnlockSecretFinish()
        {
            hasFinished = true;
            PlayerPrefs.SetInt("GainGround.HasFinished", 1);
            PlayerPrefs.Save();
            PlaySfx(secretUnlockSound);
        }

        void CycleDifficulty(int direction=1)
        { draftSettings.Difficulty=(GameDifficulty)(((int)draftSettings.Difficulty+direction+3)%3); }
        static string DifficultyLabel(GameDifficulty difficulty)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy: return "Fácil";
                case GameDifficulty.Hard: return "Difícil";
                default: return "Normal";
            }
        }
        void UpdateMenuInput()
        {
            if ((state == ScreenState.Title || state == ScreenState.Options || state == ScreenState.StageSelect) && Input.GetKeyDown(KeyCode.F11))
                CountSecretF11Press();
            if (state == ScreenState.StageSelect)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { state = ScreenState.Title; return; }
                int delta = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                    MenuDown(GameAction.Right) || MenuDown(GameAction.Down) ? 1 :
                    Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow) ||
                    MenuDown(GameAction.Left) || MenuDown(GameAction.Up) ? -1 : 0;
                if (stages != null && stages.Length > 0) stageSelectIndex = (stageSelectIndex + delta + stages.Length) % stages.Length;
                if ((Input.GetKeyDown(KeyCode.Return) || MenuDown(GameAction.Fire)) && stages != null && stages.Length > 0)
                    StartAtSelectedStage(stageSelectIndex);
                return;
            }
            int vertical = Input.GetKeyDown(KeyCode.DownArrow) || MenuDown(GameAction.Down) ? 1 :
                Input.GetKeyDown(KeyCode.UpArrow) || MenuDown(GameAction.Up) ? -1 : 0;
            bool confirm = Input.GetKeyDown(KeyCode.Return) || MenuDown(GameAction.Fire);
            if (state == ScreenState.Title)
            {
                if (Input.GetKeyDown(KeyCode.F2)) ToggleTitleRoster();
                menuIndex = (menuIndex + vertical + TitleMenuLabels.Length) % TitleMenuLabels.Length;
                if (confirm) ActivateMenuItem();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { DiscardOptions(); return; }
            int optionsCount = CanMapControls ? 7 : 6;
            optionsIndex = (optionsIndex + vertical + optionsCount) % optionsCount;
            int horizontal = Input.GetKeyDown(KeyCode.RightArrow) || MenuDown(GameAction.Right) ? 1 :
                Input.GetKeyDown(KeyCode.LeftArrow) || MenuDown(GameAction.Left) ? -1 : 0;
            if (optionsIndex == 0) draftSettings.BGM = Mathf.Clamp(draftSettings.BGM + horizontal * 5, 0, 100);
            if (optionsIndex == 1) draftSettings.SFX = Mathf.Clamp(draftSettings.SFX + horizontal * 5, 0, 100);
            ApplyAudioVolumes();
            if (optionsIndex == 2 && (horizontal != 0 || confirm)) draftSettings.ClassicMode = !draftSettings.ClassicMode;
            if (optionsIndex == 3 && (horizontal != 0 || confirm)) CycleDifficulty(horizontal != 0 ? horizontal : 1);
            if (CanMapControls && confirm && optionsIndex == 4) bindingsOpen=true;
            else if (confirm && optionsIndex == (CanMapControls ? 5 : 4)) SaveOptions();
            else if (confirm && optionsIndex == (CanMapControls ? 6 : 5)) DiscardOptions();
        }
        void DrawMenu()
        {
            if(bindingsOpen){DrawBindings(new Vector2(1000,700));return;}
            if (state == ScreenState.StageSelect)
            {
                DrawStageCarousel(new Rect(170, 160, 660, 390), false);
                return;
            }
            if(state==ScreenState.Title)DrawTitleArtwork(new Rect(0,0,1000,700));
            else { DrawSelectionPanel(new Rect(0,0,1000,700));DrawMenuBackground(new Rect(0,0,1000,700));GUI.Label(new Rect(180,92,640,65), "GAIN GROUND",GoldMenuText(titleStyle)); }
            if(state==ScreenState.Title&&!titleMenuVisible)
            {GUI.Label(new Rect(200,600,600,45), "Pressione um botão ou toque para continuar",GoldMenuText(labelStyle));return;}
            bool previousEnabled=GUI.enabled;
            if(state==ScreenState.Title)GUI.enabled=CanUseTitleMenu;
            if (state == ScreenState.Title)
            {
                DrawSelectionPanel(new Rect(170, 300, 660, 390));
                GUI.Label(new Rect(200, 330, 600, 38), "RECORDE: " + HighScore, GoldMenuText(labelStyle));
                string[] labels = TitleMenuLabels;
                float step=labels.Length==5?50:56;
                for (int i = 0; i < labels.Length; i++)
                {
                    if (menuIndex == i) GUI.Label(new Rect(258, 372 + i * step, 35, 35), ">", GoldMenuText(labelStyle));
                    if (IronButton(new Rect(300, 365 + i * step, 400, 48), labels[i]))
                    { menuIndex = i; ActivateMenuItem(); break; }
                }
                
                if (hasFinished && settings.Difficulty == GameDifficulty.Normal && allCharacters != null && allCharacters.Length > 0 &&
                    IronButton(new Rect(300, 365 + labels.Length * step, 400, 38), useFullRoster ? "Grupo: todos[F2]" : "Grupo: iniciais + resgates [F2]"))
                    ToggleTitleRoster();
            }
            else
            {
                GUI.Label(new Rect(200, 153, 600, 38), "Menu", GoldMenuText(labelStyle));
                GUI.Label(new Rect(265, 215 + optionsIndex * 56, 30, 30), ">", GoldMenuText(labelStyle));
                GUI.Label(new Rect(310, 214, 120, 30), "BGM  " + draftSettings.BGM, GoldMenuText(labelStyle));
                draftSettings.BGM = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(450, 226, 220, 25), draftSettings.BGM, 0, 100,
                    GUI.skin.horizontalSlider, SwordSliderThumb));
                GUI.Label(new Rect(310, 270, 120, 30), "SFX  " + draftSettings.SFX, GoldMenuText(labelStyle));
                draftSettings.SFX = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(450, 282, 220, 25), draftSettings.SFX, 0, 100,
                    GUI.skin.horizontalSlider, SwordSliderThumb));
                ApplyAudioVolumes();
                if (IronButton(new Rect(300, 326, 400, 45), "Modo Clássico: " + (draftSettings.ClassicMode ? "Sim" : "Não")))
                    draftSettings.ClassicMode = !draftSettings.ClassicMode;
                if (IronButton(new Rect(300, 382, 400, 45), "Dificuldade: " + DifficultyLabel(draftSettings.Difficulty))) CycleDifficulty();
                if(IronButton(new Rect(300,438,400,45),"Mapear controles — P1 / P2"))bindingsOpen=true;
                if (IronButton(new Rect(300, 494, 400, 45), "Salvar e voltar")) SaveOptions();
                if (IronButton(new Rect(300, 550, 400, 45), "Não salvar")) DiscardOptions();
                GUI.Label(new Rect(210, 610, 580, 54), "Fácil: 5 personagens, HP chefe -20%. Difícil: 1 personagem, HP chefe +20%.\nMédio: dificuldade normal.", GoldMenuText(smallStyle));
            }
            GUI.enabled=previousEnabled;
            if (!string.IsNullOrEmpty(settingsError)) GUI.Label(new Rect(110, 598, 780, 85), settingsError, GoldMenuText(smallStyle));
        }
        void ChooseCharacter(PlayerState p, int index)
        {
            if (!ClassicMode || state != ScreenState.Playing || paused || selectionPortraitsLoading ||
                !p.awaitingSelection || index < 0 || index >= p.party.Count) return;
            if (p.party[index].lost || p.party[index].escaped) return;
            var sound = p.party[index].definition.switchSound;
            SetMember(p, index); Respawn(p, 2);
            PlaySfx(sound != null ? sound : switchSound);
            p.fireCooldown = p.specialCooldown = 0; p.facing = Vector2.up;
            ReleasePortraitsOutsideSelection();
        }
        bool UpdateSelectionInput(PlayerState p)
        {
            int delta = ActionDown(p.id,GameAction.Right)||ActionDown(p.id,GameAction.Down)?1:
                ActionDown(p.id,GameAction.Left)||ActionDown(p.id,GameAction.Up)?-1:0;
            bool confirm = ActionDown(p.id,GameAction.Fire);
            if (delta != 0) p.selectionIndex = CarouselNeighbor(p, p.selectionIndex, delta);
            if (confirm) { ChooseCharacter(p, p.selectionIndex); return true; }
            return false;
        }
    }
}
