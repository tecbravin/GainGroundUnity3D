using System.Runtime.InteropServices;
using UnityEngine;
using Application = UnityEngine.Device.Application;
using Screen = UnityEngine.Device.Screen;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int GainGroundIsMobile();
        [DllImport("__Internal")] static extern void GainGroundLockLandscape();
#endif
        [Header("Mobile controls")]
        [SerializeField] bool previewMobileControls;
        bool mobilePlatform;
        readonly PlayerInput[] mobileInputs = new PlayerInput[2];
        Vector2 mobileScroll;
        Vector2 mobileGameScroll;
        GUIStyle mobileButton, mobileLabel, mobileHeading, mobileSlider, mobileThumb;
        public bool UsesMobileControls => mobilePlatform || previewMobileControls || Application.isMobilePlatform;
        bool HideDuoOption => UsesMobileControls || Application.platform == RuntimePlatform.WebGLPlayer;
        bool CanMapControls => !UsesMobileControls || Application.platform == RuntimePlatform.WebGLPlayer;
        void InitializeMobileControls()
        {
            mobilePlatform = UnityEngine.Application.isMobilePlatform;
#if UNITY_WEBGL && !UNITY_EDITOR
            mobilePlatform = mobilePlatform || GainGroundIsMobile() != 0;
#endif
            if (UsesMobileControls) Input.simulateMouseWithTouches = true;
            ApplyMobileOrientation();
        }

        void ApplyMobileOrientation()
        {
            if (!UsesMobileControls) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            GainGroundLockLandscape();
#else
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
#endif
        }

        // GUI coordinates are top-left based; touch positions are bottom-left based.
        static Rect MobileSafeArea()
        {
            var safe = Screen.safeArea;
            return new Rect(safe.x, Screen.height - safe.yMax, safe.width, safe.height);
        }
        public static float MobileScale(Vector2 size) => Mathf.Max(.01f, Mathf.Min(size.x / 480f, size.y / 360f));
        Vector2 MobileSize => MobileSafeArea().size / MobileScale(MobileSafeArea().size);
        public static Vector2 MobileDirection(Vector2 point, Rect pad)
        {
            var direction = (point - pad.center) / (pad.width * .35f);
            direction.y = -direction.y;
            return direction.sqrMagnitude < .12f ? Vector2.zero : Vector2.ClampMagnitude(direction, 1);
        }
        void ResetMobileInput()
        {
            mobileInputs[0] = mobileInputs[1] = default;
            mobileGameScroll = Vector2.zero;
            if (hudView != null) hudView.ResetTouch();
            foreach (var player in players) ResetDirectionalDashTap(player);
        }
        void OnApplicationFocus(bool focus)
        {
            if (focus) ApplyMobileOrientation();
            else { ResetMobileInput(); if (IsPlaying) paused = true; }
        }
        void OnApplicationPause(bool suspended)
        {
            if (!suspended) ApplyMobileOrientation();
            else { ResetMobileInput(); if (IsPlaying) paused = true; }
        }
        void UpdateMobileControls()
        {
            mobileInputs[0] = mobileInputs[1] = default;
            if (UsesMobileControls && !Input.simulateMouseWithTouches)
                Input.simulateMouseWithTouches = true;
            if (!UsesMobileControls || !IsPlaying || paused || hudView == null) return;
            var touch = hudView.ReadTouch(0);
            mobileInputs[0] = new PlayerInput {
                movement = touch.movement,
                analogMovement = true,
                fire = touch.fire,
                special = touch.special,
                dash = touch.dash,
                switchMember = touch.switchMember
            };
        }
        void MergeMobileInput(ref PlayerInput input, int id)
        {
            if (!UsesMobileControls) return;
            var mobile = mobileInputs[id];
            if (mobile.movement != Vector2.zero)
            {
                input.movement = mobile.movement;
                input.analogMovement = true;
            }
            input.fire |= mobile.fire; input.special |= mobile.special;
            input.dash |= mobile.dash;
            input.switchMember |= mobile.switchMember;
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
            if (state == ScreenState.Title && titleView != null)
            {
                GUI.matrix = old;
                GUI.skin.verticalScrollbar.fixedWidth = oldScrollWidth;
                GUI.skin.verticalScrollbarThumb.fixedWidth = oldThumbWidth;
                return;
            }
            if (state == ScreenState.StageSelect && stageSelectView != null)
            {
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
                GUILayout.BeginArea(new Rect((size.x - 440) / 2, top, 460, size.y - top - 8));
                mobileScroll = GUILayout.BeginScrollView(mobileScroll);
                ScrollActiveViewWithMouseWheel(ref mobileScroll);
                GUILayout.BeginVertical(GUILayout.Width(440 - GUI.skin.verticalScrollbar.fixedWidth));
                if(state!=ScreenState.Title)MobileText("GAIN GROUND", true);
                if (state == ScreenState.Title)
                {
                    GUILayout.Space(20);
                    MobileText("RECORDE: " + HighScore);
                    if (MobileButton("Solo")) ChooseTitleButton("Solo");
                    if (!HideDuoOption && MobileButton("Duo")) ChooseTitleButton("Duo");
                    if (HasSaves && MobileButton("Continue")) ChooseTitleButton("Continue");
                    if (MobileButton("Menu")) { ChooseTitleButton("Menu"); mobileScroll = Vector2.zero; }
                    if (MobileButton("Sair")) ChooseTitleButton("Sair");
                    if (hasFinished && settings.Difficulty == GameDifficulty.Normal && MobileButton(useFullRoster ? "Elenco: todos os 20" : "Elenco: trio inicial + resgates")) ToggleTitleRoster();
                }
                else
                {
                    MobileText("Options", true);
                    MobileText("BGM  " + draftSettings.BGM);
                    draftSettings.BGM = Mathf.RoundToInt(GUILayout.HorizontalSlider(draftSettings.BGM, 0, 100, mobileSlider, mobileThumb, GUILayout.Height(44)));
                    MobileText("SFX  " + draftSettings.SFX);
                    draftSettings.SFX = Mathf.RoundToInt(GUILayout.HorizontalSlider(draftSettings.SFX, 0, 100, mobileSlider, mobileThumb, GUILayout.Height(44)));
                    ApplyAudioVolumes();
                    if (MobileButton("Modo Clássico: " + (draftSettings.ClassicMode ? "Sim" : "Não")))
                    {
                        draftSettings.ClassicMode = !draftSettings.ClassicMode;
                        ShowMobileOptionsTooltip(ClassicTooltip);
                    }
                    if (MobileButton("Tiro Automático: " + (settings.AutoFire ? "Ligado" : "Desligado"))) ToggleAutoFire();
                    if (MobileButton("Dificuldade: " + DifficultyLabel(draftSettings.Difficulty)))
                    {
                        CycleDifficulty();
                        ShowMobileOptionsTooltip(DifficultyTooltip);
                    }
                    if (CanMapControls && MobileButton("Mapear controles — P1 / P2")) bindingsOpen = true;
                    if (MobileButton("Save changes and return")) SaveOptions();
                    if (MobileButton("Don't save and return")) DiscardOptions();
                    if (!string.IsNullOrEmpty(settingsError)) MobileText(settingsError);
                }
                GUILayout.EndVertical(); GUILayout.EndScrollView(); GUILayout.EndArea();GUI.enabled=previousEnabled;
                if (state == ScreenState.Options && !string.IsNullOrEmpty(mobileOptionsTooltip) &&
                    Time.unscaledTime < mobileOptionsTooltipUntil)
                {
                    var tooltipStyle = GoldMenuText(mobileLabel);
                    float tooltipWidth = Mathf.Max(200, size.x - 24);
                    float insetX = Mathf.Max(16f, tooltipWidth * .09f);
                    float textHeight = tooltipStyle.CalcHeight(new GUIContent(mobileOptionsTooltip),
                        tooltipWidth - insetX * 2);
                    float tooltipHeight = Mathf.Min(size.y * .44f, Mathf.Max(82, textHeight + 30));
                    DrawOptionsTooltip(new Rect((size.x - tooltipWidth) * .5f,
                        size.y - tooltipHeight - 8, tooltipWidth, tooltipHeight),
                        mobileOptionsTooltip, mobileLabel);
                }
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
            if (!IsPlaying || paused)
            {
                float frameWidth = Mathf.Min(448, size.x - 24);
                var frame = new Rect((size.x - frameWidth) * .5f, 16, frameWidth, size.y - 32);
                DrawSelectionPanel(frame);
                var viewport = new Rect(frame.x + 16, frame.y + 18, frame.width - 32, frame.height - 36);
                float contentWidth = Mathf.Max(100, viewport.width - GUI.skin.verticalScrollbar.fixedWidth - 8);
                const float messageLeftInset = 20f;
                float messageWidth = contentWidth - messageLeftInset;
                var headingStyle = GoldMenuText(mobileHeading);
                var textStyle = GoldMenuText(mobileLabel);
                string heading = paused ? "PAUSA" : state == ScreenState.Cleared ? "FASE CONCLUIDA" :
                    state == ScreenState.Victory ? "VITORIA" : "FIM DE JOGO";
                bool showScore = !paused && (state == ScreenState.Cleared || pendingEndingScore);
                float headingHeight = Mathf.Max(42, headingStyle.CalcHeight(new GUIContent(heading), contentWidth));
                float messageHeight = string.IsNullOrEmpty(message) ? 0 :
                    Mathf.Max(30, textStyle.CalcHeight(new GUIContent(message), messageWidth));
                float contentHeight = headingHeight + 10 + (messageHeight > 0 ? messageHeight + 10 : 0);
                if (showScore) contentHeight += MobileStageScoreSummaryHeight(contentWidth);
                if (paused) contentHeight += 172;
                bool showButtons = !pendingEndingScore;
                string primary = paused ? "Continuar" : state == ScreenState.Cleared ? "Continuar" : "Jogar novamente";
                string secondary = state == ScreenState.Cleared || state == ScreenState.Victory ?
                    "Salvar e voltar para o menu" : "Menu Principal";
                float autoHeight = paused ? MobileModalButtonHeight("Tiro Automático: " +
                    (settings.AutoFire ? "Ligado" : "Desligado"), contentWidth) + 8 : 0;
                float primaryHeight = showButtons ? MobileModalButtonHeight(primary, contentWidth) : 0;
                float secondaryHeight = showButtons ? MobileModalButtonHeight(secondary, contentWidth) : 0;
                if (showButtons) contentHeight += autoHeight + primaryHeight + secondaryHeight + 20;
                float errorHeight = paused && !string.IsNullOrEmpty(settingsError) ?
                    Mathf.Max(30, textStyle.CalcHeight(new GUIContent(settingsError), contentWidth)) + 8 : 0;
                contentHeight += errorHeight;
                mobileGameScroll = GUI.BeginScrollView(viewport, mobileGameScroll,
                    new Rect(0, 0, contentWidth, Mathf.Max(viewport.height, contentHeight)),
                    false, contentHeight > viewport.height, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar);
                if (contentHeight > viewport.height)
                    ScrollActiveViewWithMouseWheel(ref mobileGameScroll);
                float y = 0;
                GUI.Label(new Rect(0, y, contentWidth, headingHeight), heading, headingStyle);
                y += headingHeight + 10;
                if (messageHeight > 0)
                {
                    GUI.Label(new Rect(messageLeftInset, y, messageWidth, messageHeight), message, textStyle);
                    y += messageHeight + 10;
                }
                if (showScore) y = DrawMobileStageScoreSummary(y, contentWidth);
                if (paused)
                {
                    GUI.Label(new Rect(0, y, contentWidth, 30), "BGM  " + settings.BGM, textStyle);
                    y += 30;
                    SetPauseVolume(true, Mathf.RoundToInt(GUI.HorizontalSlider(
                        new Rect(10, y, contentWidth - 20, 44), settings.BGM, 0, 100, mobileSlider, mobileThumb)));
                    y += 52;
                    GUI.Label(new Rect(0, y, contentWidth, 30), "SFX  " + settings.SFX, textStyle);
                    y += 30;
                    SetPauseVolume(false, Mathf.RoundToInt(GUI.HorizontalSlider(
                        new Rect(10, y, contentWidth - 20, 44), settings.SFX, 0, 100, mobileSlider, mobileThumb)));
                    y += 60;
                }
                bool toggleAuto = false, advance = false, leave = false;
                if (showButtons)
                {
                    if (paused)
                    {
                        toggleAuto = IronButton(new Rect(0, y, contentWidth, autoHeight - 8),
                            "Tiro Automático: " + (settings.AutoFire ? "Ligado" : "Desligado"), mobileButton);
                        y += autoHeight;
                    }
                    advance = IronButton(new Rect(0, y, contentWidth, primaryHeight), primary, mobileButton);
                    y += primaryHeight + 8;
                    leave = IronButton(new Rect(0, y, contentWidth, secondaryHeight), secondary, mobileButton);
                    y += secondaryHeight + 8;
                }
                if (errorHeight > 0)
                    GUI.Label(new Rect(0, y, contentWidth, errorHeight - 8), settingsError, textStyle);
                GUI.EndScrollView();
                if (toggleAuto) ToggleAutoFire();
                if (advance) { if (paused) paused = false; else Continue(); ResetMobileInput(); }
                if (leave)
                {
                    if (state == ScreenState.Cleared || state == ScreenState.Victory) OpenSaveSlots(true);
                    else { ReturnToMenu(); ResetMobileInput(); }
                }
                return;
            }
        }

        float MobileModalButtonHeight(string label, float width)
            => Mathf.Max(48, mobileButton.CalcHeight(new GUIContent(label), width) + 8);

        static void ScrollActiveViewWithMouseWheel(ref Vector2 position)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.ScrollWheel) return;
            position.y = Mathf.Max(0, position.y + current.delta.y * 20f);
            current.Use();
        }
    }
}
