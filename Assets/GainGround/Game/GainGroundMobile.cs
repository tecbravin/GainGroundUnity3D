using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        [Header("Mobile controls")]
        [SerializeField] bool previewMobileControls;
        bool mobilePlatform;
        int mobilePlayer;
        PlayerInput mobileInput;
        readonly Dictionary<int, int> mobileFingers = new Dictionary<int, int>();
        Vector2 mobileScroll;
        Vector2 mobileScreenSize;
        GUIStyle mobileButton, mobileLabel, mobileHeading, mobileSlider, mobileThumb;
        public bool UsesMobileControls => mobilePlatform || previewMobileControls;
        bool HideDuoOption => UsesMobileControls || Application.platform == RuntimePlatform.WebGLPlayer;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int GainGroundIsMobile();
#endif
        void InitializeMobileControls()
        {
            mobilePlatform = Application.isMobilePlatform;
#if UNITY_WEBGL && !UNITY_EDITOR
            mobilePlatform |= GainGroundIsMobile() != 0 || Input.touchSupported;
#endif
            if (UsesMobileControls) Input.simulateMouseWithTouches = true;
        }

        // GUI coordinates are top-left based; touch positions are bottom-left based.
        static Rect MobileSafeArea()
        {
            var safe = Screen.safeArea;
            return new Rect(safe.x, Screen.height - safe.yMax, safe.width, safe.height);
        }
        public static float MobileScale(Vector2 size) => Mathf.Max(.01f, Mathf.Min(size.x / 480f, size.y / 360f));
        Vector2 MobileSize => MobileSafeArea().size / MobileScale(MobileSafeArea().size);
        Rect MobilePad => new Rect(12, MobileSize.y - 158, 144, 144);
        Rect MobileAction(int action) => new Rect(168 + ((action - 1) % 2) * 96,
            MobileSize.y - 150 + ((action - 1) / 2) * 70, 88, 62);
        public static Vector2 MobileDirection(Vector2 point, Rect pad)
        {
            var direction = (point - pad.center) / (pad.width * .35f);
            direction.y = -direction.y;
            return direction.sqrMagnitude < .12f ? Vector2.zero : Vector2.ClampMagnitude(direction, 1);
        }
        void ResetMobileInput() { mobileFingers.Clear(); mobileInput = default; }
        void OnApplicationFocus(bool focus) { if (!focus) { ResetMobileInput(); if (IsPlaying) paused = true; } }
        void OnApplicationPause(bool suspended) { if (suspended) { ResetMobileInput(); if (IsPlaying) paused = true; } }
        void UpdateMobileControls()
        {
            mobileInput = default;
            if (UsesMobileControls && stage != null && gameCamera != null && mobileScreenSize != new Vector2(Screen.width, Screen.height))
            {
                mobileScreenSize = new Vector2(Screen.width, Screen.height);
                gameCamera.orthographicSize = Mathf.Max(stage.size.y / 2 + 3.7f, (stage.size.x / 2 + .7f) / gameCamera.aspect);
                ResetMobileInput();
            }
            if (!UsesMobileControls || !IsPlaying || paused || mobilePlayer >= players.Count ||
                !players[mobilePlayer].HasActive || players.Exists(p => p.awaitingSelection))
            { ResetMobileInput(); return; }
            if (Input.touchCount == 0) mobileFingers.Clear();
            var safe = MobileSafeArea(); float scale = MobileScale(safe.size);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                { mobileFingers.Remove(touch.fingerId); continue; }
                var point = (new Vector2(touch.position.x, Screen.height - touch.position.y) - safe.position) / scale;
                if (touch.phase == TouchPhase.Began)
                {
                    if (MobilePad.Contains(point) && !mobileFingers.ContainsValue(0)) mobileFingers[touch.fingerId] = 0;
                    for (int action = 1; action <= (ClassicMode ? 3 : 4); action++)
                        if (MobileAction(action).Contains(point)) mobileFingers[touch.fingerId] = action;
                }
                if (!mobileFingers.TryGetValue(touch.fingerId, out int captured)) continue;
                switch (captured)
                {
                    case 0: mobileInput.movement = MobileDirection(point, MobilePad); break;
                    case 1: mobileInput.fire = true; break;
                    case 2: mobileInput.special = true; break;
                    case 3: mobileInput.dash |= touch.phase == TouchPhase.Began; break;
                    case 4: mobileInput.switchMember |= touch.phase == TouchPhase.Began; break;
                }
            }
        }
        void MergeMobileInput(ref PlayerInput input)
        {
            if (!UsesMobileControls) return;
            if (mobileInput.movement != Vector2.zero) input.movement = mobileInput.movement;
            input.fire |= mobileInput.fire; input.special |= mobileInput.special;
            input.dash |= mobileInput.dash; input.switchMember |= mobileInput.switchMember;
        }
        void MobileText(string text, bool heading = false) => GUILayout.Label(text, GoldMenuText(heading ? mobileHeading : mobileLabel));
        bool MobileButton(string text) => IronLayoutButton(text, mobileButton, GUILayout.MinHeight(48));
        void DrawMobileGUI()
        {
            if (mobileButton == null)
            {
                mobileButton = new GUIStyle(IronButtonStyle) { fontSize = 18, wordWrap = true };
                mobileLabel = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, alignment = TextAnchor.MiddleCenter };
                mobileHeading = new GUIStyle(mobileLabel) { fontSize = 26, fontStyle = FontStyle.Bold };
                mobileSlider = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 32 };
                mobileThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 36, fixedHeight = 36 };
            }
            var old = GUI.matrix; var safe = MobileSafeArea(); float scale = MobileScale(safe.size);
            float oldScrollWidth = GUI.skin.verticalScrollbar.fixedWidth;
            float oldThumbWidth = GUI.skin.verticalScrollbarThumb.fixedWidth;
            GUI.skin.verticalScrollbar.fixedWidth = 32;
            GUI.skin.verticalScrollbarThumb.fixedWidth = 32;
            GUI.matrix = Matrix4x4.TRS(safe.position, Quaternion.identity, Vector3.one * scale);
            var size = safe.size / scale;
            if(bindingsOpen){DrawBindings(size);GUI.matrix=old;GUI.skin.verticalScrollbar.fixedWidth=oldScrollWidth;GUI.skin.verticalScrollbarThumb.fixedWidth=oldThumbWidth;return;}
            float titleWidth=Mathf.Min(size.x,size.y/ .7f);
            float titleHeight=titleWidth*.7f;
            if(saveBrowserOpen)
            {
                DrawSaveSlots(size,true);GUI.matrix=old;GUI.skin.verticalScrollbar.fixedWidth=oldScrollWidth;GUI.skin.verticalScrollbarThumb.fixedWidth=oldThumbWidth;return;
            }
            if(state==ScreenState.Title)DrawTitleArtwork(new Rect((size.x-titleWidth)/2,0,titleWidth,titleHeight));
            if(state==ScreenState.Title&&!titleMenuVisible)
                GUI.Label(new Rect(12,Mathf.Min(size.y-70,titleHeight*.82f),size.x-24,60),"Toque ou aperte um botão para continuar",GoldMenuText(mobileLabel));
            else if (state == ScreenState.StageSelect)
            {
                DrawStageCarousel(new Rect(8, 8, size.x - 16, size.y - 16), true);
            }
            else if (state == ScreenState.Title || state == ScreenState.Options)
            {
                bool previousEnabled=GUI.enabled;
                if(state==ScreenState.Title)GUI.enabled=CanUseTitleMenu;
                else { DrawSelectionPanel(new Rect(0, 0, size.x, size.y)); DrawMenuBackground(new Rect(0, 0, size.x, size.y)); }
                float top=state==ScreenState.Title?Mathf.Min(titleHeight*.52f,size.y-120):20;
                if(state==ScreenState.Title)DrawSelectionPanel(new Rect(0,top-12,size.x,size.y-top+12));
                GUILayout.BeginArea(new Rect((size.x - 440) / 2, top, 440, size.y - top - 8));
                mobileScroll = GUILayout.BeginScrollView(mobileScroll);
                if(state!=ScreenState.Title)MobileText("GAIN GROUND", true);
                if (state == ScreenState.Title)
                {
                    GUILayout.Space(20);
                    MobileText("RECORDE: " + HighScore);
                    if (MobileButton("Solo")) StartNewGameFromMenu(1);
                    if (!HideDuoOption && MobileButton("Duo")) StartNewGameFromMenu(2);
                    if (HasSaves && MobileButton("Continue")) OpenSaveSlots(false);
                    if (MobileButton("Menu")) { OpenOptions(); mobileScroll = Vector2.zero; }
                    if (MobileButton("Sair")) ExitGame();
                    if (hasFinished && settings.Difficulty == GameDifficulty.Normal && MobileButton(useFullRoster ? "Elenco: todos os 20" : "Elenco: trio inicial + resgates")) useFullRoster = !useFullRoster;
                }
                else
                {
                    MobileText("Options", true);
                    MobileText("BGM  " + draftSettings.BGM);
                    draftSettings.BGM = Mathf.RoundToInt(GUILayout.HorizontalSlider(draftSettings.BGM, 0, 100, mobileSlider, mobileThumb, GUILayout.Height(44)));
                    MobileText("SFX  " + draftSettings.SFX);
                    draftSettings.SFX = Mathf.RoundToInt(GUILayout.HorizontalSlider(draftSettings.SFX, 0, 100, mobileSlider, mobileThumb, GUILayout.Height(44)));
                    ApplyAudioVolumes();
                    if (MobileButton("Classic Mode: " + (draftSettings.ClassicMode ? "On" : "Off"))) draftSettings.ClassicMode = !draftSettings.ClassicMode;
                    if (MobileButton("Difficulty: " + draftSettings.Difficulty)) CycleDifficulty();
                    MobileText("Easy: 5 personagens, chefes -20% HP. Hard: 1 personagem, chefes +20% HP.");
                    if (MobileButton("Save changes and return")) SaveOptions();
                    if (MobileButton("Don't save and return")) DiscardOptions();
                    if (!string.IsNullOrEmpty(settingsError)) MobileText(settingsError);
                }
                GUILayout.EndScrollView(); GUILayout.EndArea();GUI.enabled=previousEnabled;
            }
            else if (stage != null) DrawMobileGame(size);
            GUI.matrix = old;
            GUI.skin.verticalScrollbar.fixedWidth = oldScrollWidth;
            GUI.skin.verticalScrollbarThumb.fixedWidth = oldThumbWidth;
        }
        void DrawMobileGame(Vector2 size)
        {
            if (endingAnimationPlaying || (state == ScreenState.Victory && !pendingEndingScore))
            { DrawEndingScreen(size); return; }
            if(state==ScreenState.Defeat||state==ScreenState.GameOver)
            {DrawContinueScreen(new Rect(12,20,size.x-24,size.y-40));return;}
            GUI.Box(new Rect(0, 0, size.x, 64), "");
            GUI.Label(new Rect(4, 0, size.x - 110, 32), stage.displayName + "  |  " + Mathf.CeilToInt(remainingTime) + "s", mobileLabel);
            GUI.Label(new Rect(4, 32, size.x - 110, 30), "P1: " + (players.Count>0?players[0].score:0) + (players.Count>1?"  P2: "+players[1].score:"") + " | Inimigos " + enemies.Count, mobileLabel);
            if (IsPlaying && IronButton(new Rect(size.x - 104, 8, 98, 48), paused ? "Voltar" : "Pausa", mobileButton))
            { paused = !paused; ResetMobileInput(); }
            if (!IsPlaying || paused)
            {
                DrawSelectionPanel(new Rect((size.x - 448) / 2, 68, 448, size.y - 72));
                GUILayout.BeginArea(new Rect((size.x - 420) / 2, 84, 420, size.y - 100));
                mobileScroll = GUILayout.BeginScrollView(mobileScroll);
                MobileText(paused ? "PAUSA" : state == ScreenState.Cleared ? "FASE CONCLUIDA" : state == ScreenState.Victory ? "VITORIA" : "FIM DE JOGO", true);
                MobileText(message);
                if (!paused && (state == ScreenState.Cleared || pendingEndingScore)) DrawMobileStageScoreSummary();
                if (pendingEndingScore) { GUILayout.EndScrollView(); GUILayout.EndArea(); return; }
                if (MobileButton(paused ? "Continuar" : state == ScreenState.Cleared ? "Continuar" : "Jogar novamente"))
                { if (paused) paused = false; else Continue(); ResetMobileInput(); }
                if(state==ScreenState.Cleared||state==ScreenState.Victory)
                {if(MobileButton("Salvar e voltar para o menu"))OpenSaveSlots(true);}
                else if (MobileButton("Menu Principal")) { ReturnToMenu(); ResetMobileInput(); }
                GUILayout.EndScrollView(); GUILayout.EndArea(); return;
            }
            mobilePlayer = Mathf.Clamp(mobilePlayer, 0, players.Count - 1);
            var p = players[mobilePlayer];
            if (players.Count > 1 && IronButton(new Rect(size.x - 104, 70, 98, 48), "P" + (mobilePlayer + 1) + " / trocar", mobileButton))
            { mobilePlayer = 1 - mobilePlayer; ResetMobileInput(); p = players[mobilePlayer]; }
            // In classic mode every team's spawn selector remains reachable by touch.
            foreach (var waiting in players)
                if (waiting.awaitingSelection && !p.awaitingSelection) { p = waiting; break; }
            if (p.awaitingSelection)
            {
                DrawCharacterCarousel(p, new Rect(12, 122, Mathf.Min(480, size.x - 24), 150), true);
                return;
            }
            string hero = p.HasActive ? p.party[p.active].definition.displayName : "Equipe sem personagem ativo";
            GUI.Label(new Rect(12, size.y - 194, 350, 32), "P" + (p.id + 1) + " - " + hero, mobileLabel);
            GUI.Box(MobilePad, "");
            GUI.Label(MobilePad, "Ã¢â€“Â²\nÃ¢â€”â‚¬        Ã¢â€“Â¶\nÃ¢â€“Â¼", mobileHeading);
            var knob = MobilePad.center + new Vector2(mobileInput.movement.x, -mobileInput.movement.y) * 40;
            GUI.Box(new Rect(knob.x - 16, knob.y - 16, 32, 32), "");
            bool wasEnabled = GUI.enabled; GUI.enabled = p.HasActive;
            // Gameplay touches are polled separately, so moving and firing work simultaneously.
            IronBox(MobileAction(1), "Tiro", mobileButton);
            IronBox(MobileAction(2), "Especial", mobileButton);
            float cooldown = p.HasActive ? p.party[p.active].dashCooldown : 0;
            IronBox(MobileAction(3), cooldown > 0 ? "Dash\n" + cooldown.ToString("F1") + "s" : "Dash", mobileButton);
            if (!ClassicMode) IronBox(MobileAction(4), "Trocar", mobileButton);
            GUI.enabled = wasEnabled;
        }
    }
}
