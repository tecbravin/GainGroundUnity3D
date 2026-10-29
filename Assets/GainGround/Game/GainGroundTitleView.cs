using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GainGround.Game
{
    public sealed class GainGroundTitleView : MonoBehaviour
    {
        static readonly string[] Entries = { "Solo", "Duo", "Continue", "Menu", "Sair" };

        [SerializeField] Canvas canvas;
        [SerializeField] UnityEngine.UI.GraphicRaycaster raycaster;
        [SerializeField] RectTransform safeArea;
        [SerializeField] UnityEngine.UI.Image titleLeft;
        [SerializeField] UnityEngine.UI.Image titleRight;
        [SerializeField] UnityEngine.UI.Image logo;
        [SerializeField] Sprite[] logoFrames;
        [SerializeField] UnityEngine.UI.Image reforge;
        [SerializeField] RectTransform menuPanel;
        [SerializeField] UnityEngine.UI.ScrollRect menuScroll;
        [SerializeField] UnityEngine.UI.VerticalLayoutGroup menuLayout;
        [SerializeField] UnityEngine.UI.LayoutElement recordLayout;
        [SerializeField] UnityEngine.UI.Text recordText;
        [SerializeField] UnityEngine.UI.Button[] buttons;
        [SerializeField] UnityEngine.UI.Image[] selectionMarkers;
        [SerializeField] UnityEngine.UI.LayoutElement[] buttonLayouts;
        [SerializeField] UnityEngine.UI.Button rosterButton;
        [SerializeField] UnityEngine.UI.LayoutElement rosterLayout;
        [SerializeField] UnityEngine.UI.Text rosterText;
        [SerializeField] UnityEngine.UI.Text introPrompt;
        [SerializeField] UnityEngine.UI.Button gainButton;

        int layoutWidth, layoutHeight, layoutCount = -1;
        int lastSelectedIndex = -1;
        Rect layoutSafeArea;
        Action gainClickAction;
        Material logoShineMaterial;

        void Awake() => InitializeLogoShine();

        void OnDestroy()
        {
            if (logoShineMaterial != null) Destroy(logoShineMaterial);
        }

        void InitializeLogoShine()
        {
            if (logo == null || logoShineMaterial != null) return;
            Shader shader = Resources.Load<Shader>("PS2Title/TitleLogoShine");
            if (shader == null) return;
            logoShineMaterial = new Material(shader) { name = "Title Logo Shine (Instance)" };
            logo.material = logoShineMaterial;
        }

        public void Configure(Canvas root, UnityEngine.UI.GraphicRaycaster graphicRaycaster,
            RectTransform safe, UnityEngine.UI.Image left, UnityEngine.UI.Image right,
            UnityEngine.UI.Image titleLogo, Sprite[] frames, UnityEngine.UI.Image reforgeImage,
            RectTransform panel,
            UnityEngine.UI.ScrollRect scroll, UnityEngine.UI.VerticalLayoutGroup layout,
            UnityEngine.UI.Text record, UnityEngine.UI.LayoutElement recordHeight,
            UnityEngine.UI.Button[] entries, UnityEngine.UI.Image[] markers,
            UnityEngine.UI.LayoutElement[] entryHeights, UnityEngine.UI.Button roster,
            UnityEngine.UI.LayoutElement rosterHeight, UnityEngine.UI.Text rosterLabel,
            UnityEngine.UI.Text prompt, UnityEngine.UI.Button gain)
        {
            canvas = root;
            raycaster = graphicRaycaster;
            safeArea = safe;
            titleLeft = left;
            titleRight = right;
            logo = titleLogo;
            logoFrames = frames;
            reforge = reforgeImage;
            menuPanel = panel;
            menuScroll = scroll;
            menuLayout = layout;
            recordText = record;
            recordLayout = recordHeight;
            buttons = entries;
            selectionMarkers = markers;
            buttonLayouts = entryHeights;
            rosterButton = roster;
            rosterLayout = rosterHeight;
            rosterText = rosterLabel;
            introPrompt = prompt;
            gainButton = gain;
            InitializeLogoShine();
        }

        public void Bind(Action<string> choose, Action toggleRoster, Action gainClick)
        {
            for (int i = 0; i < buttons.Length && i < Entries.Length; i++)
            {
                string entry = Entries[i];
                buttons[i].onClick.AddListener(() => choose(entry));
            }
            rosterButton.onClick.AddListener(() => toggleRoster());
            gainClickAction = gainClick;
        }

        void Update()
        {
            ScrollMenuFromAnywhere();
            if (canvas == null || !canvas.enabled || gainButton == null ||
                !gainButton.interactable || gainClickAction == null) return;
            var hitArea = gainButton.transform as RectTransform;
            if (Touchscreen.current != null && GainGroundGame.TouchPressed)
            {
                for (int i = 0; i < Touchscreen.current.touches.Count; i++)
                {
                    var touch = Touchscreen.current.touches[i];
                    if (!touch.press.wasPressedThisFrame ||
                        !RectTransformUtility.RectangleContainsScreenPoint(hitArea, touch.position.ReadValue(), null)) continue;
                    gainClickAction();
                    break;
                }
            }
            else if (GainGroundGame.MousePressed(0) &&
                RectTransformUtility.RectangleContainsScreenPoint(hitArea, Mouse.current.position.ReadValue(), null))
                gainClickAction();
        }

        void ScrollMenuFromAnywhere()
        {
            if (canvas == null || !canvas.enabled || menuScroll == null ||
                menuScroll.viewport == null || menuScroll.content == null ||
                !menuPanel.gameObject.activeInHierarchy) return;
            float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0;
            if (Mouse.current == null || Mathf.Approximately(wheel, 0) ||
                RectTransformUtility.RectangleContainsScreenPoint(menuScroll.viewport,
                    Mouse.current.position.ReadValue(), canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera))
                return;
            float overflow = menuScroll.content.rect.height - menuScroll.viewport.rect.height;
            if (overflow > 0)
                menuScroll.verticalNormalizedPosition = Mathf.Clamp01(
                    menuScroll.verticalNormalizedPosition + wheel * menuScroll.scrollSensitivity / overflow);
        }

        public void Refresh(bool visible, float elapsed, bool menuVisible, bool canUse,
            int highScore, bool showDuo, bool hasSaves, bool showRoster,
            bool fullRoster, int selectedIndex)
        {
            if (canvas.enabled != visible) canvas.enabled = visible;
            if (raycaster.enabled != visible) raycaster.enabled = visible;
            if (!visible) { lastSelectedIndex = -1; return; }

            int shown = 0;
            int activeIndex = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool show = i != 1 || showDuo;
                if (i == 2) show = hasSaves;
                if (buttons[i].gameObject.activeSelf != show) buttons[i].gameObject.SetActive(show);
                if (!show) continue;
                buttons[i].interactable = canUse;
                selectionMarkers[i].gameObject.SetActive(activeIndex == selectedIndex && canUse);
                activeIndex++;
                shown++;
            }
            if (rosterButton.gameObject.activeSelf != showRoster)
                rosterButton.gameObject.SetActive(showRoster);
            rosterButton.interactable = canUse;
            string rosterLabel = fullRoster ? "Grupo: todos [F2]" : "Grupo: iniciais + resgates [F2]";
            if (rosterText.text != rosterLabel) rosterText.text = rosterLabel;
            string record = "RECORDE: " + highScore;
            if (recordText.text != record) recordText.text = record;

            if (menuPanel.gameObject.activeSelf != menuVisible)
            {
                menuPanel.gameObject.SetActive(menuVisible);
                menuScroll.verticalNormalizedPosition = 1;
            }
            if (introPrompt.gameObject.activeSelf == menuVisible)
                introPrompt.gameObject.SetActive(!menuVisible);
            gainButton.interactable = canUse;
            AnimateTitle(elapsed, menuVisible);
            UpdateLayout(shown + (showRoster ? 1 : 0));
            if (menuVisible && canUse && selectedIndex != lastSelectedIndex)
            {
                Canvas.ForceUpdateCanvases();
                menuScroll.verticalNormalizedPosition = shown <= 1 ? 1 :
                    1f - (float)selectedIndex / (shown - 1);
                lastSelectedIndex = selectedIndex;
            }
            if (!menuVisible) lastSelectedIndex = -1;
        }

        void AnimateTitle(float elapsed, bool menuVisible)
        {
            float motion = menuVisible ? 1f : Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / 4.8f));
            float halfWidth = canvas.pixelRect.width / Mathf.Max(.01f, canvas.scaleFactor) * .5f;
            titleLeft.rectTransform.anchoredPosition = new Vector2(-halfWidth * .8f * (1 - motion), 0);
            titleRight.rectTransform.anchoredPosition = new Vector2(halfWidth * .8f * (1 - motion), 0);

            float progress = menuVisible ? 1f : Mathf.Clamp01((elapsed - 4.8f) / 1.7f);
            int frame = progress < .3f ? 4 : progress < .55f ? 3 : progress < .7f ? 2 : progress < .85f ? 1 : 0;
            if (logoFrames != null && logoFrames.Length == 5 && logo.sprite != logoFrames[frame])
                logo.sprite = logoFrames[frame];
            var color = logo.color;
            float alpha = Mathf.SmoothStep(0, 1, progress * 3);
            if (!Mathf.Approximately(color.a, alpha))
            {
                color.a = alpha;
                logo.color = color;
            }
            if (logoShineMaterial != null)
            {
                // The glint starts on the finished gold logo, after the halves meet.
                float shineReveal = Mathf.Clamp01((progress - .85f) / .15f);
                float sweep = Mathf.Repeat(Time.unscaledTime / 3.2f, 1f);
                logoShineMaterial.SetFloat("_ShinePhase", Mathf.Lerp(-.3f, 1.3f, sweep));
                logoShineMaterial.SetFloat("_ShineStrength", .68f * shineReveal);
            }
            if (reforge != null)
            {
                float reveal = menuVisible ? 1f : Mathf.Clamp01((elapsed - 4.8f) / .45f);
                float glow = .5f + .5f * Mathf.Sin(elapsed * 4.5f);
                reforge.color = new Color(1f, 1f, 1f, reveal * (.82f + .18f * glow));
                reforge.rectTransform.localScale = Vector3.one * (1f + .012f * glow);
            }
        }

        void UpdateLayout(int visibleButtons)
        {
            int width = Screen.width, height = Screen.height;
            Rect safe = Screen.safeArea;
            if (width <= 0 || height <= 0) return;
            if (width == layoutWidth && height == layoutHeight && safe == layoutSafeArea &&
                visibleButtons == layoutCount) return;
            if (visibleButtons != layoutCount) lastSelectedIndex = -1;
            layoutWidth = width;
            layoutHeight = height;
            layoutSafeArea = safe;
            layoutCount = visibleButtons;

            safeArea.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            safeArea.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;

            float scale = Mathf.Max(.01f, canvas.scaleFactor);
            float availableWidth = safe.width / scale;
            float availableHeight = safe.height / scale;
            float logoWidth = Mathf.Min(800, availableWidth * .9f);
            logo.rectTransform.sizeDelta = new Vector2(logoWidth,
                Mathf.Min(logoWidth / 1.75f, availableHeight * .55f));
            logo.rectTransform.anchoredPosition = new Vector2(0, -Mathf.Min(8, availableHeight * .02f));
            if (reforge != null)
            {
                float reforgeWidth = Mathf.Min(400, availableWidth * .68f);
                reforge.rectTransform.sizeDelta = new Vector2(reforgeWidth, reforgeWidth / 3f);
                reforge.rectTransform.anchoredPosition =
                    new Vector2(0, -Mathf.Min(8, availableHeight * .02f));
            }
            float buttonHeight = Mathf.Clamp(availableHeight * .073f, 38, 54);
            float spacing = Mathf.Clamp(buttonHeight * .12f, 4, 9);
            menuLayout.spacing = spacing;
            menuLayout.padding = new RectOffset(20, 20, 14, 14);
            recordLayout.preferredHeight = Mathf.Clamp(buttonHeight * .8f, 30, 44);
            foreach (var entry in buttonLayouts) entry.preferredHeight = buttonHeight;
            rosterLayout.preferredHeight = Mathf.Max(36, buttonHeight * .82f);

            float desired = 80 + recordLayout.preferredHeight + buttonHeight * visibleButtons +
                spacing * visibleButtons;
            if (rosterButton.gameObject.activeSelf)
                desired += rosterLayout.preferredHeight - buttonHeight;
            float bottomOffset = Mathf.Min(16, availableHeight * .025f);
            float maxHeight = availableHeight * .5f - bottomOffset;
            float panelWidth = Mathf.Max(120, Mathf.Min(600, availableWidth - 24));
            float panelHeight = Mathf.Min(desired, maxHeight);
            menuPanel.sizeDelta = new Vector2(panelWidth, panelHeight);
            menuPanel.anchoredPosition = new Vector2(0, bottomOffset);
            float borderX = Mathf.Clamp(panelWidth * .085f, 16, 55);
            float borderY = Mathf.Clamp(panelHeight * .085f, 12, 36);
            menuScroll.viewport.offsetMin = new Vector2(borderX, borderY);
            menuScroll.viewport.offsetMax = new Vector2(-borderX, -borderY);
        }
    }
}
