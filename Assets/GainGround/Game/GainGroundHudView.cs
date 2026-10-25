using System;
using UnityEngine;

namespace GainGround.Game
{
    public sealed class GainGroundHudView : MonoBehaviour
    {
        [Serializable]
        public sealed class PlayerPanel
        {
            public RectTransform rail;
            public GameObject rosterPanel;
            public UnityEngine.UI.Text nameText;
            public UnityEngine.UI.Text selectedNameText;
            public UnityEngine.UI.Text rosterText;
            public UnityEngine.UI.Text dashText;
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
            const float mapPaddingX = 1.4f;
            const float mapPaddingY = 1.6f;
            float mapWidthAtFullHeight = height * (stageSize.x + mapPaddingX) /
                (stageSize.y + mapPaddingY);
            float sidePixels = Mathf.Clamp(Mathf.Max(width * .17f,
                (width - mapWidthAtFullHeight) * .5f), width * .16f, width * .3f);
            player1.rail.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sidePixels / canvasScale);
            player2.rail.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sidePixels / canvasScale);
            if (gameCamera == null) return;
            float gameWidth = Mathf.Max(1, width - sidePixels * 2);
            gameCamera.rect = new Rect(sidePixels / width, 0, gameWidth / width, 1);
            float aspect = gameWidth / height;
            gameCamera.orthographicSize = Mathf.Max((stageSize.y + mapPaddingY) * .5f,
                (stageSize.x + mapPaddingX) / (2 * aspect));
        }

        public void InvalidateLayout() { layoutWidth = 0; }

        public void SetMobilePresentation(bool mobile)
        {
            bool showPlayer2Roster = !mobile;
            if (player2.rosterPanel != null && player2.rosterPanel.activeSelf != showPlayer2Roster)
                player2.rosterPanel.SetActive(showPlayer2Roster);
        }

        public void SetHeader(int seconds, int enemies, int score, string stage)
        {
            if (displayedSeconds != seconds)
            {
                SetText(timerText, "TEMPO  " + Mathf.Max(0, seconds).ToString("D3"));
                timerText.color = seconds <= 20 ? new Color(1, .38f, .28f) : new Color(.97f, .82f, .5f);
                displayedSeconds = seconds;
            }
            if (displayedEnemies != enemies)
            { SetText(enemiesText, "INIMIGOS  " + enemies.ToString("D2")); displayedEnemies = enemies; }
            if (displayedScore != score)
            { SetText(scoreText, "PONTOS  " + score.ToString("D6")); displayedScore = score; }
            if (displayedStage != stage)
            { SetText(stageText, stage); displayedStage = stage; }
        }

        static void SetText(UnityEngine.UI.Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }

        public void SetPlayer(int id, string name, string selectedName, string roster, string dash,
            Texture2D portrait, Sprite sprite, float dashReady, bool selecting,
            bool mobile, bool canSwitch)
        {
            var panel = id == 0 ? player1 : player2;
            SetText(panel.nameText, name);
            SetText(panel.selectedNameText, selectedName);
            if (panel.selectedNameText != null)
                SetActiveIfChanged(panel.selectedNameText.gameObject, selecting && !string.IsNullOrEmpty(selectedName));
            SetText(panel.rosterText, roster);
            SetText(panel.dashText, dash);
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
                    float fit = Mathf.Min(frame.rect.width * .86f / portrait.width,
                        frame.rect.height * .86f / portrait.height);
                    image.anchorMin = image.anchorMax = new Vector2(.5f, .5f);
                    image.sizeDelta = new Vector2(portrait.width * fit, portrait.height * fit);
                    image.anchoredPosition = Vector2.zero;
                }
            }
            SetActiveIfChanged(panel.portrait.gameObject, portrait != null);
            SetActiveIfChanged(panel.spritePortrait.gameObject, portrait == null && sprite != null);
            SetActiveIfChanged(panel.portraitPlaceholder, portrait == null && sprite == null);
            var fill = panel.dashFill.rectTransform;
            var fillEnd = fill.anchorMax;
            fillEnd.x = Mathf.Clamp01(dashReady);
            if (fill.anchorMax != fillEnd) fill.anchorMax = fillEnd;
            SetActiveIfChanged(panel.selectionButtons, selecting);
            if (panel.portraitButton.interactable != selecting) panel.portraitButton.interactable = selecting;
            SetActiveIfChanged(panel.touchControls, mobile && !selecting);
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
