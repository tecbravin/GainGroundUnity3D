using System.Collections.Generic;
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
        int mobileMouseAction = -1;
        Vector2 mobileScroll;
        Vector2 mobileScreenSize;
        GUIStyle mobileButton, mobileLabel, mobileHeading, mobileSlider, mobileThumb;
        GUIStyle browserControlButton, browserControlLabel, mobileHudNumber;
        public bool UsesMobileControls => mobilePlatform || previewMobileControls;
        bool HideDuoOption => UsesMobileControls || Application.platform == RuntimePlatform.WebGLPlayer;
        bool CanMapControls => !UsesMobileControls || Application.platform == RuntimePlatform.WebGLPlayer;
        bool BrowserTouchLayout => Application.platform == RuntimePlatform.WebGLPlayer;
        void InitializeMobileControls()
        {
            mobilePlatform = Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer;
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
        float BrowserControlWidth
        {
            get
            {
                if (stage == null || gameCamera == null) return 80f;
                var safe = MobileSafeArea();
                float mapLeft = (gameCamera.WorldToScreenPoint(new Vector3(-stage.size.x / 2f, 0, 0)).x - safe.x) /
                    MobileScale(safe.size);
                return Mathf.Clamp(mapLeft - 12f, 72f, 96f);
            }
        }
        Rect MobilePad
        {
            get
            {
                if (!BrowserTouchLayout) return new Rect(12, MobileSize.y - 158, 144, 144);
                float width = Mathf.Min(BrowserControlWidth, Mathf.Max(64f, MobileSize.y - 280f));
                return new Rect(8, 98, width, width);
            }
        }
        Rect MobileAction(int action)
        {
            if (!BrowserTouchLayout) return new Rect(168 + ((action - 1) % 2) * 96,
                MobileSize.y - 150 + ((action - 1) / 2) * 70, 88, 62);
            var pad = MobilePad;
            const float gap = 4f;
            float height = Mathf.Min(40f, (MobileSize.y - pad.yMax - 12f - 3f * gap) / 4f);
            return new Rect(pad.x, pad.yMax + 6f + (action - 1) * (height + gap), pad.width, height);
        }
        public static Vector2 MobileDirection(Vector2 point, Rect pad)
        {
            var direction = (point - pad.center) / (pad.width * .35f);
            direction.y = -direction.y;
            return direction.sqrMagnitude < .12f ? Vector2.zero : Vector2.ClampMagnitude(direction, 1);
        }
        void ResetMobileInput() { mobileFingers.Clear(); mobileMouseAction = -1; mobileInput = default; }
        int MobileControlAt(Vector2 point)
        {
            if (MobilePad.Contains(point)) return 0;
            for (int action = 1; action <= (ClassicMode ? 3 : 4); action++)
                if (MobileAction(action).Contains(point)) return action;
            return -1;
        }
        void ApplyMobileControl(int action, Vector2 point, bool pressed)
        {
            switch (action)
            {
                case 0: mobileInput.movement = MobileDirection(point, MobilePad); break;
                case 1: mobileInput.fire = true; break;
                case 2: mobileInput.special = true; break;
                case 3: mobileInput.dash |= pressed; break;
                case 4: mobileInput.switchMember |= pressed; break;
            }
        }
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
            else mobileMouseAction = -1;
            var safe = MobileSafeArea(); float scale = MobileScale(safe.size);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                { mobileFingers.Remove(touch.fingerId); continue; }
                var point = (new Vector2(touch.position.x, Screen.height - touch.position.y) - safe.position) / scale;
                if (touch.phase == TouchPhase.Began)
                {
                    int action = MobileControlAt(point);
                    if (action >= 0 && (action != 0 || !mobileFingers.ContainsValue(0)))
                        mobileFingers[touch.fingerId] = action;
                }
                if (!mobileFingers.TryGetValue(touch.fingerId, out int captured)) continue;
                ApplyMobileControl(captured, point, touch.phase == TouchPhase.Began);
            }
            // simulateMouseWithTouches only converts touches into mouse events, not the reverse.
            if (Input.touchCount == 0)
            {
                var point = (new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) - safe.position) / scale;
                if (Input.GetMouseButtonDown(0)) mobileMouseAction = MobileControlAt(point);
                if (Input.GetMouseButton(0) && mobileMouseAction >= 0)
                    ApplyMobileControl(mobileMouseAction, point, Input.GetMouseButtonDown(0));
                if (Input.GetMouseButtonUp(0)) mobileMouseAction = -1;
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
                mobileThumb = SwordSliderThumb;
                browserControlButton = new GUIStyle(mobileButton) { fontSize = 13,
                    padding = new RectOffset(2, 2, 0, 0) };
                browserControlLabel = new GUIStyle(mobileLabel) { fontSize = 11 };
                mobileHudNumber = new GUIStyle(mobileLabel) { fontSize = 16, fontStyle = FontStyle.Bold };
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
            if (state == ScreenState.Loading)
            {
                DrawStageLoading(new Rect(12, 20, size.x - 24, size.y - 40));
                GUI.matrix = old;
                GUI.skin.verticalScrollbar.fixedWidth = oldScrollWidth;
                GUI.skin.verticalScrollbarThumb.fixedWidth = oldThumbWidth;
                return;
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
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    draftSettings.BGM = Mathf.RoundToInt(GUILayout.HorizontalSlider(draftSettings.BGM, 0, 100, mobileSlider, mobileThumb, GUILayout.Height(44)));
                    GUILayout.EndHorizontal();
                    MobileText("SFX  " + draftSettings.SFX);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    draftSettings.SFX = Mathf.RoundToInt(GUILayout.HorizontalSlider(draftSettings.SFX, 0, 100, mobileSlider, mobileThumb, GUILayout.Height(44)));
                    GUILayout.EndHorizontal();
                    ApplyAudioVolumes();
                    if (MobileButton("Classic Mode: " + (draftSettings.ClassicMode ? "On" : "Off"))) draftSettings.ClassicMode = !draftSettings.ClassicMode;
                    if (MobileButton("Difficulty: " + draftSettings.Difficulty)) CycleDifficulty();
                    MobileText("Easy: 5 personagens, chefes -20% HP. Hard: 1 personagem, chefes +20% HP.");
                    if (CanMapControls && MobileButton("Mapear controles — P1 / P2")) bindingsOpen = true;
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
            GUI.Label(new Rect(4, 0, size.x - 110, 25), stage.displayName, mobileLabel);
            if (hudScore != null && hudTimeEnemy != null)
            {
                HUDTexture(new Rect(4, 25, 130, 38), hudScore, new Rect(0, 0, .5f, 1));
                GUI.Label(new Rect(18, 32, 105, 27), score.ToString("D6"), mobileHudNumber);
                HUDTexture(new Rect(138, 25, 130, 38), hudTimeEnemy, new Rect(0, 0, 1, 1));
                GUI.Label(new Rect(144, 32, 58, 27), Mathf.CeilToInt(remainingTime).ToString("D3"), mobileHudNumber);
                GUI.Label(new Rect(208, 32, 54, 27), enemies.Count.ToString("D2"), mobileHudNumber);
                GUI.Label(new Rect(270, 31, Mathf.Max(0, size.x - 380), 28),
                    "P1: " + (players.Count > 0 ? players[0].score : 0), browserControlLabel);
            }
            else
                GUI.Label(new Rect(4, 31, size.x - 110, 30),
                    score + " pts  |  " + Mathf.CeilToInt(remainingTime) + "s  |  Inimigos " + enemies.Count, mobileLabel);
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
            var heroRect = BrowserTouchLayout
                ? new Rect(8, 65, MobilePad.width, 31)
                : new Rect(12, size.y - 194, 350, 32);
            var heroStyle = BrowserTouchLayout ? browserControlLabel : mobileLabel;
            GUI.Label(heroRect, "P" + (p.id + 1) + " - " + hero, heroStyle);
            GUI.Box(MobilePad, "");
            var pad = MobilePad;
            var padLabel = BrowserTouchLayout ? mobileLabel : mobileHeading;
            GUI.Label(new Rect(pad.x, pad.y, pad.width, pad.height * .3f), "^", padLabel);
            GUI.Label(new Rect(pad.x, pad.yMax - pad.height * .3f, pad.width, pad.height * .3f), "v", padLabel);
            GUI.Label(new Rect(pad.x, pad.center.y - 15, pad.width * .35f, 30), "<", padLabel);
            GUI.Label(new Rect(pad.xMax - pad.width * .35f, pad.center.y - 15, pad.width * .35f, 30), ">", padLabel);
            float knobRadius = pad.width * .28f;
            float knobSize = BrowserTouchLayout ? 22f : 32f;
            var knob = pad.center + new Vector2(mobileInput.movement.x, -mobileInput.movement.y) * knobRadius;
            GUI.Box(new Rect(knob.x - knobSize / 2, knob.y - knobSize / 2, knobSize, knobSize), "");
            bool wasEnabled = GUI.enabled; GUI.enabled = p.HasActive;
            // Gameplay touches are polled separately, so moving and firing work simultaneously.
            var actionStyle = BrowserTouchLayout ? browserControlButton : mobileButton;
            IronBox(MobileAction(1), "Atacar", actionStyle);
            IronBox(MobileAction(2), "Especial", actionStyle);
            float cooldown = p.HasActive ? p.party[p.active].dashCooldown : 0;
            IronBox(MobileAction(3), cooldown > 0 ? "Esquiva\n" + cooldown.ToString("F1") + "s" : "Esquiva", actionStyle);
            if (!ClassicMode) IronBox(MobileAction(4), "Trocar", actionStyle);
            GUI.enabled = wasEnabled;
        }
    }
}
