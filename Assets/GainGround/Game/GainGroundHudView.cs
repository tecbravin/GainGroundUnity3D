using System;
using UnityEngine;
using Screen = UnityEngine.Device.Screen;

namespace GainGround.Game
{
    public sealed class GainGroundHudView : MonoBehaviour
    {
        [Serializable]
        public sealed class PlayerPanel
        {
            public RectTransform rail;
            public GameObject rosterPanel;
            public UnityEngine.UI.Image selectionBrightness;
            public UnityEngine.UI.Outline selectionOutline;
            public UnityEngine.UI.Text nameText;
            public UnityEngine.UI.Text selectedNameText;
            public UnityEngine.UI.Text rosterText;
            public UnityEngine.UI.Text exitedText;
            public UnityEngine.UI.Image portrait;
            [NonSerialized] public Texture2D displayedPortrait;
            [NonSerialized] public Sprite runtimePortrait;
            [NonSerialized] public Vector2 portraitFrameSize;
            public UnityEngine.UI.Image spritePortrait;
            public GameObject portraitPlaceholder;
            public UnityEngine.UI.Image dashFill;
            public GameObject selectionButtons;
            public UnityEngine.UI.Button previousButton;
            public UnityEngine.UI.Button portraitButton;
            public UnityEngine.UI.Button nextButton;
            public GameObject touchControls;
            public GameObject touchActions;
            public HudTouchStick stick;
            public HudTouchButton fireButton;
            public HudTouchButton specialButton;
            public HudTouchButton dashButton;
            public HudTouchButton switchButton;
        }

        public struct TouchState
        {
            public Vector2 movement;
            public bool fire, special, dash, switchMember;
        }

        [SerializeField] Canvas canvas;
        [SerializeField] PlayerPanel player1;
        [SerializeField] PlayerPanel player2;
        [SerializeField] UnityEngine.UI.Text timerText;
        [SerializeField] UnityEngine.UI.Text enemiesText;
        [SerializeField] UnityEngine.UI.Text scoreText;
        [SerializeField] UnityEngine.UI.Text stageText;
        [SerializeField] UnityEngine.UI.Button pauseButton;
        int layoutWidth, layoutHeight;
        float layoutScale;
        Vector2 layoutStage;
        struct RectLayout
        {
            public RectTransform rect;
            public Transform parent;
            public Vector2 min, max, position, size;

            public RectLayout(RectTransform value)
            {
                rect = value; parent = value.parent;
                min = value.anchorMin; max = value.anchorMax;
                position = value.anchoredPosition; size = value.sizeDelta;
            }

            public void Restore(Transform newParent = null)
            {
                rect.SetParent(newParent != null ? newParent : parent, false);
                rect.anchorMin = min; rect.anchorMax = max;
                rect.sizeDelta = size; rect.anchoredPosition = position;
            }
        }
        RectLayout rosterLayout, touchLayout, padLayout, actionsLayout;
        RectLayout rosterTextLayout, exitedTextLayout, dashBarLayout;
        RectLayout stageLayout, pauseLayout, switchLayout;
        static readonly Color DisabledDashColor = new Color(.18f, .18f, .18f, 1f);
        static readonly Color Player1DashColor = new Color(.36f, .8f, 1f, 1f);
        static readonly Color Player2DashColor = new Color(1f, .66f, .28f, 1f);
        RectLayout nameLayout, selectedNameLayout, portraitButtonLayout, spritePortraitLayout;
        RectLayout previousButtonLayout, nextButtonLayout;
        bool mobileLayoutCached, mobileLayoutApplied;
        bool player2Present;
        bool originalPadPreserveAspect;
        Vector3 originalPortraitScale, originalSpritePortraitScale;
        int originalSelectedNameFontSize, originalSelectedNameMaxSize;
        int originalStageFontSize, originalStageMaxSize;
        int originalRosterFontSize, originalRosterMaxSize, originalExitedFontSize, originalExitedMaxSize;
        TextAnchor originalStageAlignment;
        int displayedSeconds = int.MinValue, displayedEnemies = int.MinValue, displayedScore = int.MinValue;
        string displayedStage;

        public void Configure(Canvas root, PlayerPanel left, PlayerPanel right,
            UnityEngine.UI.Text timer, UnityEngine.UI.Text enemies,
            UnityEngine.UI.Text score, UnityEngine.UI.Text stage,
            UnityEngine.UI.Button pause)
        {
            canvas = root; player1 = left; player2 = right;
            timerText = timer; enemiesText = enemies; scoreText = score;
            stageText = stage; pauseButton = pause;
        }

        public void Bind(Action<int, int> selectionStep, Action<int> confirm, Action pause)
        {
            player1.previousButton.onClick.AddListener(() => selectionStep(0, -1));
            player1.nextButton.onClick.AddListener(() => selectionStep(0, 1));
            player1.portraitButton.onClick.AddListener(() => confirm(0));
            player2.previousButton.onClick.AddListener(() => selectionStep(1, -1));
            player2.nextButton.onClick.AddListener(() => selectionStep(1, 1));
            player2.portraitButton.onClick.AddListener(() => confirm(1));
            pauseButton.onClick.AddListener(() => pause());
        }

        public void UpdateLayout(Camera gameCamera, Vector2 stageSize)
        {
            int width = Screen.width, height = Screen.height;
            if (width <= 0 || height <= 0 || canvas == null) return;
            float canvasScale = Mathf.Max(.01f, canvas.scaleFactor);
            if (width == layoutWidth && height == layoutHeight && stageSize == layoutStage &&
                Mathf.Approximately(layoutScale, canvasScale)) return;
            layoutWidth = width; layoutHeight = height; layoutStage = stageSize;
            layoutScale = canvasScale;
            // Keep a half-unit border around the PS2 terrain while showing the entire map.
            const float mapPaddingX = 1f;
            const float mapPaddingY = 1f;
            float mapWidthAtFullHeight = height * (stageSize.x + mapPaddingX) /
                (stageSize.y + mapPaddingY);
            float sidePixels = Mathf.Clamp(Mathf.Max(width * .17f,
                (width - mapWidthAtFullHeight) * .5f), width * .16f, width * .3f);
            player1.rail.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sidePixels / canvasScale);
            player2.rail.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sidePixels / canvasScale);
            SetHeaderStatusMargins(10f / canvasScale);
            SetPlayerStatusMargins(player1, 10f / canvasScale);
            SetPlayerStatusMargins(player2, 10f / canvasScale);
            SetActionButtonMargins(player1, 10f / canvasScale);
            SetActionButtonMargins(player2, 10f / canvasScale);
            if (gameCamera == null) return;
            float gameWidth = Mathf.Max(1, width - sidePixels * 2);
            gameCamera.rect = new Rect(sidePixels / width, 0, gameWidth / width, 1);
            float aspect = gameWidth / height;
            gameCamera.orthographicSize = Mathf.Max((stageSize.y + mapPaddingY) * .5f,
                (stageSize.x + mapPaddingX) / (2 * aspect));
        }

        public void InvalidateLayout() { layoutWidth = 0; }

        void SetHeaderStatusMargins(float margin)
        {
            var timeGroup = timerText.rectTransform.parent as RectTransform;
            var enemyGroup = enemiesText.rectTransform.parent as RectTransform;
            if (timeGroup == null || enemyGroup == null ||
                timeGroup == player1.rail || enemyGroup == player1.rail) return;
            SetHeaderStatusRect(timeGroup, 0, margin);
            SetHeaderStatusRect(enemyGroup, 1, margin);
        }

        static void SetHeaderStatusRect(RectTransform rect, int column, float margin)
        {
            rect.anchorMin = new Vector2(column * .5f, .89f);
            rect.anchorMax = new Vector2((column + 1) * .5f, .975f);
            rect.offsetMin = new Vector2(column == 0 ? margin : margin * .5f, 0);
            rect.offsetMax = new Vector2(column == 0 ? -margin * .5f : -margin, 0);
        }

        void SetPlayerStatusMargins(PlayerPanel panel, float margin)
        {
            var teamGroup = panel.rosterText.rectTransform.parent as RectTransform;
            var exitedGroup = panel.exitedText.rectTransform.parent as RectTransform;
            if (teamGroup == null || exitedGroup == null || teamGroup == exitedGroup) return;
            float teamInset = margin;
            SetPlayerStatusRect(teamGroup, 0, margin, teamInset);
            SetPlayerStatusRect(exitedGroup, 1, margin, teamInset);
        }

        static void SetPlayerStatusRect(RectTransform rect, int column, float margin, float teamInset)
        {
            rect.anchorMin = new Vector2(column * .5f, rect.anchorMin.y);
            rect.anchorMax = new Vector2((column + 1) * .5f, rect.anchorMax.y);
            rect.offsetMin = new Vector2(column == 0 ? teamInset : margin * .5f, rect.offsetMin.y);
            rect.offsetMax = new Vector2(column == 0 ? -margin * .5f : -margin, rect.offsetMax.y);
        }

        static void SetActionButtonMargins(PlayerPanel panel, float margin)
        {
            SetActionButtonRect(panel.fireButton, 0, 1, margin);
            SetActionButtonRect(panel.specialButton, 1, 1, margin);
            SetActionButtonRect(panel.dashButton, 0, 0, margin);
            SetActionButtonRect(panel.switchButton, 1, 0, margin);
        }

        static void SetActionButtonRect(HudTouchButton button, int column, int row, float margin)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(column * .5f, row * .5f);
            rect.anchorMax = new Vector2((column + 1) * .5f, (row + 1) * .5f);
            rect.offsetMin = new Vector2(column == 0 ? margin : margin * .5f,
                row == 0 ? margin : margin * .5f);
            rect.offsetMax = new Vector2(column == 0 ? -margin * .5f : -margin,
                row == 0 ? -margin * .5f : -margin);
        }

        public void SetMobilePresentation(bool mobile)
        {
            if (!mobileLayoutCached)
            {
                rosterLayout = new RectLayout((RectTransform)player1.rosterPanel.transform);
                touchLayout = new RectLayout((RectTransform)player1.touchControls.transform);
                padLayout = new RectLayout((RectTransform)player1.stick.transform);
                actionsLayout = new RectLayout((RectTransform)player1.touchActions.transform);
                rosterTextLayout = new RectLayout((RectTransform)player1.rosterText.transform.parent);
                exitedTextLayout = new RectLayout((RectTransform)player1.exitedText.transform.parent);
                dashBarLayout = new RectLayout((RectTransform)player1.dashFill.transform.parent);
                stageLayout = new RectLayout(stageText.rectTransform);
                pauseLayout = new RectLayout((RectTransform)pauseButton.transform);
                switchLayout = new RectLayout((RectTransform)player1.switchButton.transform);
                nameLayout = new RectLayout(player1.nameText.rectTransform);
                selectedNameLayout = new RectLayout(player1.selectedNameText.rectTransform);
                portraitButtonLayout = new RectLayout((RectTransform)player1.portraitButton.transform);
                spritePortraitLayout = new RectLayout(player1.spritePortrait.rectTransform);
                previousButtonLayout = new RectLayout((RectTransform)player1.previousButton.transform);
                nextButtonLayout = new RectLayout((RectTransform)player1.nextButton.transform);
                originalSelectedNameFontSize = player1.selectedNameText.fontSize;
                originalSelectedNameMaxSize = player1.selectedNameText.resizeTextMaxSize;
                originalPortraitScale = player1.portrait.rectTransform.localScale;
                originalSpritePortraitScale = player1.spritePortrait.rectTransform.localScale;
                originalStageFontSize = stageText.fontSize;
                originalStageMaxSize = stageText.resizeTextMaxSize;
                originalStageAlignment = stageText.alignment;
                originalRosterFontSize = player1.rosterText.fontSize;
                originalRosterMaxSize = player1.rosterText.resizeTextMaxSize;
                originalExitedFontSize = player1.exitedText.fontSize;
                originalExitedMaxSize = player1.exitedText.resizeTextMaxSize;
                originalPadPreserveAspect = player1.stick.GetComponent<UnityEngine.UI.Image>().preserveAspect;
                mobileLayoutCached = true;
            }
            if (mobileLayoutApplied != mobile)
            {
                if (mobile)
                {
                    rosterLayout.Restore(player2.rail);
                    rosterLayout.rect.anchorMin = new Vector2(0, .43f);
                    rosterLayout.rect.anchorMax = new Vector2(1, .79f);
                    rosterLayout.rect.sizeDelta = Vector2.zero;
                    rosterTextLayout.Restore(player1.rail);
                    SetStretch(rosterTextLayout.rect, new Vector2(0, .79f), new Vector2(.5f, .875f));
                    exitedTextLayout.Restore(player1.rail);
                    SetStretch(exitedTextLayout.rect, new Vector2(.5f, .79f), new Vector2(1, .875f));
                    player1.rosterText.fontSize = timerText.fontSize;
                    player1.rosterText.resizeTextMaxSize = timerText.resizeTextMaxSize;
                    player1.exitedText.fontSize = enemiesText.fontSize;
                    player1.exitedText.resizeTextMaxSize = enemiesText.resizeTextMaxSize;
                    SetStretch(stageLayout.rect, new Vector2(.04f, .715f), new Vector2(.96f, .785f));
                    stageText.fontSize = originalStageFontSize;
                    stageText.resizeTextMaxSize = originalStageMaxSize;
                    stageText.alignment = TextAnchor.MiddleCenter;
                    pauseLayout.Restore(player2.rail);
                    switchLayout.Restore(player1.rail);
                    SetStretch(switchLayout.rect, new Vector2(.2f, .655f), new Vector2(.8f, .71f));
                    dashBarLayout.Restore(player2.rail);
                    SetStretch(dashBarLayout.rect, new Vector2(.07f, .75f), new Vector2(.93f, .77f));
                    dashBarLayout.rect.gameObject.SetActive(false);
                    SetStretch(portraitButtonLayout.rect, Vector2.zero, Vector2.one, new Vector2(-20, -40));
                    SetStretch(nameLayout.rect, new Vector2(.04f, 1), new Vector2(.96f, 1),
                        new Vector2(0, 20), new Vector2(0, -10));
                    SetStretch(selectedNameLayout.rect, new Vector2(.04f, 0), new Vector2(.96f, 0),
                        new Vector2(0, 84), new Vector2(0, 42));
                    player1.selectedNameText.fontSize = (originalSelectedNameFontSize - 4) * 2;
                    player1.selectedNameText.resizeTextMaxSize = (originalSelectedNameMaxSize - 4) * 2;
                    player1.portrait.rectTransform.localScale = Vector3.one * 1.2f;
                    player1.spritePortrait.rectTransform.localScale = Vector3.one * 1.2f;
                    player1.previousButton.GetComponent<UnityEngine.UI.Image>().color = Color.white;
                    player1.nextButton.GetComponent<UnityEngine.UI.Image>().color = Color.white;
                    SetStretch(previousButtonLayout.rect, new Vector2(.02f, .36f), new Vector2(.25f, .64f));
                    SetStretch(nextButtonLayout.rect, new Vector2(.75f, .36f), new Vector2(.98f, .64f));
                    SetStretch(spritePortraitLayout.rect, new Vector2(.01f, .01f), new Vector2(.99f, .99f));
                    touchLayout.rect.anchorMin = new Vector2(.02f, .04f);
                    touchLayout.rect.anchorMax = new Vector2(.98f, .65f);
                    padLayout.rect.anchorMin = new Vector2(.04f, .04f);
                    padLayout.rect.anchorMax = new Vector2(.96f, .96f);
                    player1.stick.GetComponent<UnityEngine.UI.Image>().preserveAspect = true;
                }
                else
                {
                    rosterTextLayout.Restore(); exitedTextLayout.Restore();
                    player1.rosterText.fontSize = originalRosterFontSize;
                    player1.rosterText.resizeTextMaxSize = originalRosterMaxSize;
                    player1.exitedText.fontSize = originalExitedFontSize;
                    player1.exitedText.resizeTextMaxSize = originalExitedMaxSize;
                    stageLayout.Restore(); pauseLayout.Restore(); switchLayout.Restore();
                    stageText.fontSize = originalStageFontSize;
                    stageText.resizeTextMaxSize = originalStageMaxSize;
                    stageText.alignment = originalStageAlignment;
                    dashBarLayout.Restore();
                    dashBarLayout.rect.gameObject.SetActive(true);
                    nameLayout.Restore(); selectedNameLayout.Restore();
                    previousButtonLayout.Restore(); nextButtonLayout.Restore();
                    player1.selectedNameText.fontSize = originalSelectedNameFontSize;
                    player1.selectedNameText.resizeTextMaxSize = originalSelectedNameMaxSize;
                    player1.portrait.rectTransform.localScale = originalPortraitScale;
                    player1.spritePortrait.rectTransform.localScale = originalSpritePortraitScale;
                    player1.previousButton.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
                    player1.nextButton.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
                    portraitButtonLayout.Restore(); spritePortraitLayout.Restore();
                    rosterLayout.Restore(); touchLayout.Restore(); padLayout.Restore(); actionsLayout.Restore();
                    player1.stick.GetComponent<UnityEngine.UI.Image>().preserveAspect = originalPadPreserveAspect;
                }
                player1.portraitFrameSize = Vector2.zero;
                mobileLayoutApplied = mobile;
                float statusMargin = 10f / Mathf.Max(.01f, canvas.scaleFactor);
                SetPlayerStatusMargins(player1, statusMargin);
                SetPlayerStatusMargins(player2, statusMargin);
            }
            if (mobile) SizeMobileActionButtons();
            bool showPlayer2Roster = !mobile;
            if (player2.rosterPanel != null && player2.rosterPanel.activeSelf != showPlayer2Roster)
                player2.rosterPanel.SetActive(showPlayer2Roster);
            SetActiveIfChanged(player2.rosterText.transform.parent.gameObject, !mobile && player2Present);
            SetActiveIfChanged(player2.exitedText.transform.parent.gameObject, !mobile && player2Present);
        }

        void SizeMobileActionButtons()
        {
            float height = player2.rail.rect.height;
            if (height <= 0 || canvas == null) return;
            float margin = 10f / Mathf.Max(.01f, canvas.scaleFactor);
            // Keep the two action rows below the character selector, with the pause button
            // occupying the fourth cell beside dash.
            float targetHeight = .54f * height - 3f * margin;
            float top = .43f * height + margin;
            float lowestBottom = .02f * height - margin;
            float actualHeight = Mathf.Max(0, Mathf.Min(targetHeight, top - lowestBottom));
            var min = new Vector2(0, (top - actualHeight) / height);
            var max = new Vector2(1, top / height);
            if (actionsLayout.rect.anchorMin != min) actionsLayout.rect.anchorMin = min;
            if (actionsLayout.rect.anchorMax != max) actionsLayout.rect.anchorMax = max;
            // Match the dash button's bottom row dimensions and 10 px spacing.
            var pauseRect = pauseLayout.rect;
            var pauseMin = new Vector2(.5f, min.y);
            var pauseMax = new Vector2(1f, (min.y + max.y) * .5f);
            if (pauseRect.anchorMin != pauseMin) pauseRect.anchorMin = pauseMin;
            if (pauseRect.anchorMax != pauseMax) pauseRect.anchorMax = pauseMax;
            var pauseOffsetMin = new Vector2(margin * .5f, margin);
            var pauseOffsetMax = new Vector2(-margin, -margin * .5f);
            if (pauseRect.offsetMin != pauseOffsetMin) pauseRect.offsetMin = pauseOffsetMin;
            if (pauseRect.offsetMax != pauseOffsetMax) pauseRect.offsetMax = pauseOffsetMax;
        }

        static void SetStretch(RectTransform rect, Vector2 min, Vector2 max,
            Vector2? size = null, Vector2? position = null)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = size ?? Vector2.zero;
            rect.anchoredPosition = position ?? Vector2.zero;
        }

        public void SetHeader(int seconds, int enemies, int score, string stage)
        {
            if (displayedSeconds != seconds)
            {
                SetText(timerText, Mathf.Max(0, seconds).ToString("D3"));
                timerText.color = seconds <= 30 ? new Color(1, .18f, .18f) : new Color(.97f, .82f, .5f);
                displayedSeconds = seconds;
            }
            if (displayedEnemies != enemies)
            { SetText(enemiesText, enemies.ToString("D2")); displayedEnemies = enemies; }
            if (displayedScore != score)
            { SetText(scoreText, score.ToString("D6")); displayedScore = score; }
            if (displayedStage != stage)
            { SetText(stageText, stage); displayedStage = stage; }
        }

        static void SetText(UnityEngine.UI.Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }

        public void SetPlayer(int id, string name, string selectedName,
            int teamCount, int exitedCount,
            Texture2D portrait, Sprite sprite, float dashReady, float specialReady,
            bool canDash, bool selecting,
            bool mobile, bool canSwitch, bool selectionReady)
        {
            var panel = id == 0 ? player1 : player2;
            if (id == 1)
            {
                player2Present = teamCount >= 0;
                SetActiveIfChanged(panel.rosterText.transform.parent.gameObject,
                    !mobileLayoutApplied && player2Present);
                SetActiveIfChanged(panel.exitedText.transform.parent.gameObject,
                    !mobileLayoutApplied && player2Present);
            }
            SetText(panel.nameText, name);
            SetText(panel.selectedNameText, selectedName);
            if (id == 0 && mobileLayoutApplied)
            {
                float bottomMargin = string.IsNullOrEmpty(selectedName) ? 20f : 84f;
                var portraitFrame = portraitButtonLayout.rect;
                var frameSize = new Vector2(-20, -20 - bottomMargin);
                if (portraitFrame.sizeDelta != frameSize)
                {
                    portraitFrame.sizeDelta = frameSize;
                    portraitFrame.anchoredPosition = new Vector2(0, (bottomMargin - 20) * .5f);
                    panel.portraitFrameSize = Vector2.zero;
                }
            }
            if (panel.selectedNameText != null)
                SetActiveIfChanged(panel.selectedNameText.gameObject, !string.IsNullOrEmpty(selectedName));
            if (panel.selectionBrightness != null)
                SetActiveIfChanged(panel.selectionBrightness.gameObject, selecting);
            if (panel.selectionOutline != null)
            {
                if (panel.selectionOutline.enabled != selecting)
                    panel.selectionOutline.enabled = selecting;
                if (selecting)
                {
                    float glow = .45f + .18f * Mathf.Sin(Time.unscaledTime * 5f);
                    panel.selectionOutline.effectColor = new Color(1f, .75f, .25f, glow);
                }
            }
            SetText(panel.rosterText, teamCount >= 0 ? teamCount.ToString() : "–");
            SetText(panel.exitedText, exitedCount >= 0 ? exitedCount.ToString() : "–");
            bool portraitChanged = panel.displayedPortrait != portrait;
            if (portraitChanged)
            {
                if (panel.runtimePortrait != null) Destroy(panel.runtimePortrait);
                panel.displayedPortrait = portrait;
                panel.runtimePortrait = portrait != null ? Sprite.Create(portrait,
                    new Rect(0, 0, portrait.width, portrait.height),
                    new Vector2(.5f, .5f), 100) : null;
                panel.portrait.sprite = panel.runtimePortrait;
            }
            if (panel.spritePortrait.sprite != sprite) panel.spritePortrait.sprite = sprite;
            if (portrait != null)
            {
                var frame = (RectTransform)panel.portrait.transform.parent;
                if (portraitChanged || panel.portraitFrameSize != frame.rect.size)
                {
                    panel.portraitFrameSize = frame.rect.size;
                    var image = panel.portrait.rectTransform;
                    float portraitFill = 1f;
                    float fit = Mathf.Min(frame.rect.width * portraitFill / portrait.width,
                        frame.rect.height * portraitFill / portrait.height);
                    image.anchorMin = image.anchorMax = new Vector2(.5f, .5f);
                    image.sizeDelta = new Vector2(portrait.width * fit, portrait.height * fit);
                    image.anchoredPosition = Vector2.zero;
                }
            }
            SetActiveIfChanged(panel.portrait.gameObject, portrait != null);
            SetActiveIfChanged(panel.spritePortrait.gameObject, portrait == null && sprite != null);
            SetActiveIfChanged(panel.portraitPlaceholder, portrait == null && sprite == null &&
                (selecting || string.IsNullOrEmpty(selectedName)));
            var fill = panel.dashFill.rectTransform;
            var fillEnd = fill.anchorMax;
            fillEnd.x = canDash ? Mathf.Clamp01(dashReady) : 1f;
            if (fill.anchorMax != fillEnd) fill.anchorMax = fillEnd;
            var dashColor = canDash ? (id == 0 ? Player1DashColor : Player2DashColor) : DisabledDashColor;
            if (panel.dashFill.color != dashColor) panel.dashFill.color = dashColor;
            panel.dashButton.SetAvailable(canDash);
            panel.dashButton.SetCharge(dashReady);
            panel.specialButton.SetCharge(specialReady);
            SetActiveIfChanged(panel.selectionButtons, selecting);
            bool canConfirm = selecting && selectionReady;
            if (panel.portraitButton.interactable != canConfirm) panel.portraitButton.interactable = canConfirm;
            SetActiveIfChanged(panel.touchControls, mobile);
            if (panel.touchActions != null)
                SetActiveIfChanged(panel.touchActions, mobile && !selecting);
            SetActiveIfChanged(panel.switchButton.gameObject, mobile && canSwitch && !selecting);
        }

        static void SetActiveIfChanged(GameObject target, bool active)
        {
            if (target.activeSelf != active) target.SetActive(active);
        }

        public TouchState ReadTouch(int id)
        {
            var panel = id == 0 ? player1 : player2;
            if (!panel.touchControls.activeInHierarchy) return default;
            bool firePressed = panel.fireButton.ConsumePress();
            bool specialPressed = panel.specialButton.ConsumePress();
            return new TouchState {
                movement = panel.stick.Direction,
                fire = panel.fireButton.Held || firePressed,
                special = panel.specialButton.Held || specialPressed,
                dash = panel.dashButton.ConsumePress(),
                switchMember = panel.switchButton.ConsumePress()
            };
        }

        public void ResetTouch()
        {
            ResetTouch(player1);
            ResetTouch(player2);
        }
        static void ResetTouch(PlayerPanel panel)
        {
            if (panel == null) return;
            panel.stick.ResetInput();
            panel.fireButton.ResetInput();
            panel.specialButton.ResetInput();
            panel.dashButton.ResetInput();
            panel.switchButton.ResetInput();
        }

        void OnDestroy()
        {
            if (player1 != null && player1.runtimePortrait != null) Destroy(player1.runtimePortrait);
            if (player2 != null && player2.runtimePortrait != null) Destroy(player2.runtimePortrait);
        }
    }
}
