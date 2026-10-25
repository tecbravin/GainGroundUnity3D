using System;
using UnityEngine;

namespace GainGround.Game
{
    public sealed class GainGroundStageSelectView : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField] UnityEngine.UI.GraphicRaycaster raycaster;
        [SerializeField] RectTransform safeArea;
        [SerializeField] RectTransform window;
        [SerializeField] RectTransform mainPreviewFrame;
        UnityEngine.UI.Button mainPreviewButton;
        [SerializeField] UnityEngine.UI.Image mainPreview;
        [SerializeField] UnityEngine.UI.Text previewStatus;
        [SerializeField] UnityEngine.UI.Text stageName;
        [SerializeField] UnityEngine.UI.Text stageCount;
        [SerializeField] RectTransform previousCard;
        [SerializeField] RectTransform nextCard;
        [SerializeField] UnityEngine.UI.Image previousPreview;
        [SerializeField] UnityEngine.UI.Image nextPreview;
        [SerializeField] UnityEngine.UI.Text previousName;
        [SerializeField] UnityEngine.UI.Text nextName;
        [SerializeField] UnityEngine.UI.Button previousButton;
        [SerializeField] UnityEngine.UI.Button nextButton;
        [SerializeField] UnityEngine.UI.Button previousCardButton;
        [SerializeField] UnityEngine.UI.Button nextCardButton;
        [SerializeField] UnityEngine.UI.Button startButton;
        [SerializeField] UnityEngine.UI.Button backButton;

        int layoutWidth, layoutHeight;
        Rect layoutSafeArea;
        float layoutCanvasScale;
        Vector2 prefabWindowSize;
        Vector3 prefabWindowScale;
        bool layoutDefaultsCaptured;

        bool ShowNeighborCards => window.rect.width * window.localScale.x >= 790 &&
            window.rect.height * window.localScale.y >= 410;

        public void Configure(Canvas root, UnityEngine.UI.GraphicRaycaster graphicRaycaster,
            RectTransform safe, RectTransform panel, RectTransform previewFrame,
            UnityEngine.UI.Image preview, UnityEngine.UI.Text status,
            UnityEngine.UI.Text name, UnityEngine.UI.Text count,
            RectTransform leftCard, RectTransform rightCard,
            UnityEngine.UI.Image leftPreview, UnityEngine.UI.Image rightPreview,
            UnityEngine.UI.Text leftName, UnityEngine.UI.Text rightName,
            UnityEngine.UI.Button left, UnityEngine.UI.Button right,
            UnityEngine.UI.Button leftCardButton, UnityEngine.UI.Button rightCardButton,
            UnityEngine.UI.Button start, UnityEngine.UI.Button back)
        {
            canvas = root; raycaster = graphicRaycaster; safeArea = safe; window = panel;
            mainPreviewFrame = previewFrame; mainPreview = preview; previewStatus = status;
            stageName = name; stageCount = count;
            previousCard = leftCard; nextCard = rightCard;
            previousPreview = leftPreview; nextPreview = rightPreview;
            previousName = leftName; nextName = rightName;
            previousButton = left; nextButton = right;
            previousCardButton = leftCardButton; nextCardButton = rightCardButton;
            startButton = start; backButton = back;
        }

        public void Bind(Action<int> changeStage, Action start, Action back)
        {
            previousButton.onClick.AddListener(() => changeStage(-1));
            nextButton.onClick.AddListener(() => changeStage(1));
            previousCardButton.onClick.AddListener(() => changeStage(-1));
            nextCardButton.onClick.AddListener(() => changeStage(1));
            mainPreviewButton = mainPreviewFrame.GetComponent<UnityEngine.UI.Button>();
            if (mainPreviewButton == null)
                mainPreviewButton = mainPreviewFrame.gameObject.AddComponent<UnityEngine.UI.Button>();
            var frameImage = mainPreviewFrame.GetComponent<UnityEngine.UI.Image>();
            frameImage.raycastTarget = true;
            mainPreviewButton.targetGraphic = frameImage;
            mainPreviewButton.transition = UnityEngine.UI.Selectable.Transition.None;
            mainPreviewButton.onClick.AddListener(() => start());
            startButton.onClick.AddListener(() => start());
            backButton.onClick.AddListener(() => back());
        }

        public void Refresh(bool visible, StageDefinition[] stages, int index,
            bool streaming, string error)
        {
            if (canvas.enabled != visible) canvas.enabled = visible;
            if (raycaster.enabled != visible) raycaster.enabled = visible;
            if (!visible) return;
            UpdateLayout();

            int count = stages == null ? 0 : stages.Length;
            bool hasStages = count > 0;
            bool showNeighbors = count > 1 && ShowNeighborCards;
            if (previousCard.gameObject.activeSelf != showNeighbors)
                previousCard.gameObject.SetActive(showNeighbors);
            if (nextCard.gameObject.activeSelf != showNeighbors)
                nextCard.gameObject.SetActive(showNeighbors);
            startButton.interactable = hasStages;
            mainPreviewButton.interactable = hasStages;
            previousButton.interactable = nextButton.interactable = count > 1;
            previousCardButton.interactable = nextCardButton.interactable = count > 1;
            if (!hasStages)
            {
                stageName.text = "Nenhum estágio disponível";
                stageCount.text = "";
                SetPreview(mainPreview, previewStatus, null, "Nenhuma prévia");
                FitMainPreviewFrame(null);
                return;
            }

            index = (index % count + count) % count;
            var current = stages[index];
            SetText(stageName, current != null && !string.IsNullOrEmpty(current.displayName) ?
                current.displayName : "Estágio " + (index + 1));
            SetText(stageCount, (index + 1) + " / " + count);
            string status = !string.IsNullOrEmpty(error) ? "Prévia indisponível" :
                streaming ? "Carregando prévia..." : "Prévia indisponível";
            SetPreview(mainPreview, previewStatus, current != null ? current.background : null, status);
            FitMainPreviewFrame(mainPreview.sprite);

            if (count > 1 && previousCard.gameObject.activeSelf)
            {
                int previous = (index + count - 1) % count;
                int next = (index + 1) % count;
                SetNeighbor(previousPreview, previousName, stages[previous], previous);
                SetNeighbor(nextPreview, nextName, stages[next], next);
            }
        }

        static void SetNeighbor(UnityEngine.UI.Image image, UnityEngine.UI.Text label,
            StageDefinition stage, int index)
        {
            Sprite sprite = stage != null ? stage.background : null;
            if (image.sprite != sprite) image.sprite = sprite;
            if (image.enabled != (sprite != null)) image.enabled = sprite != null;
            SetText(label, stage != null && !string.IsNullOrEmpty(stage.displayName) ?
                stage.displayName : "Estágio " + (index + 1));
        }

        static void SetPreview(UnityEngine.UI.Image image, UnityEngine.UI.Text status,
            Sprite sprite, string message)
        {
            if (image.sprite != sprite) image.sprite = sprite;
            if (image.enabled != (sprite != null)) image.enabled = sprite != null;
            if (status.gameObject.activeSelf != (sprite == null)) status.gameObject.SetActive(sprite == null);
            if (sprite == null) SetText(status, message);
        }

        static void SetText(UnityEngine.UI.Text text, string value)
        {
            if (text.text != value) text.text = value;
        }

        void UpdateLayout()
        {
            int width = Screen.width, height = Screen.height;
            Rect safe = Screen.safeArea;
            if (width <= 0 || height <= 0) return;
            float canvasScale = Mathf.Max(.01f, canvas.scaleFactor);
            if (layoutWidth == width && layoutHeight == height && layoutSafeArea == safe &&
                Mathf.Approximately(layoutCanvasScale, canvasScale)) return;
            layoutWidth = width; layoutHeight = height; layoutSafeArea = safe;
            layoutCanvasScale = canvasScale;
            if (!layoutDefaultsCaptured)
            {
                prefabWindowSize = window.sizeDelta;
                prefabWindowScale = window.localScale;
                layoutDefaultsCaptured = true;
            }
            safeArea.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            safeArea.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;

            float availableWidth = safe.width / canvasScale;
            float availableHeight = safe.height / canvasScale;
            float fit = Mathf.Min(1, (availableWidth - 16) / Mathf.Max(1, prefabWindowSize.x * prefabWindowScale.x),
                (availableHeight - 16) / Mathf.Max(1, prefabWindowSize.y * prefabWindowScale.y));
            fit = Mathf.Max(.01f, fit);
            window.localScale = prefabWindowScale * fit;
        }

        void FitMainPreviewFrame(Sprite sprite)
        {
            float panelWidth = window.sizeDelta.x, panelHeight = window.sizeDelta.y;
            bool showNeighbors = ShowNeighborCards;
            float maxWidth = showNeighbors ? panelWidth * .57f : panelWidth * .78f;
            float maxHeight = Mathf.Min(panelHeight * .52f, maxWidth * .69f);
            float aspect = sprite != null && sprite.rect.height > 0 ?
                sprite.rect.width / sprite.rect.height : 6f / 7f;
            const float borderFraction = .06f;
            const float verticalInset = 42f;
            float innerWidthFraction = 1 - 2 * borderFraction;
            float sideGap = Mathf.Min(20f / Mathf.Max(.01f, canvas.scaleFactor * window.localScale.x),
                Mathf.Max(0, (maxWidth * innerWidthFraction - 1) * .5f));
            float mapHeight = Mathf.Max(1, Mathf.Min(maxHeight - verticalInset,
                (maxWidth * innerWidthFraction - 2 * sideGap) / aspect));
            float mapWidth = mapHeight * aspect;
            float frameWidth = (mapWidth + 2 * sideGap) / innerWidthFraction;
            var frameSize = new Vector2(frameWidth, mapHeight + verticalInset);
            if (mainPreviewFrame.sizeDelta != frameSize) mainPreviewFrame.sizeDelta = frameSize;
            var previewSize = new Vector2(mapWidth - frameWidth, -verticalInset);
            if (mainPreview.rectTransform.sizeDelta != previewSize)
                mainPreview.rectTransform.sizeDelta = previewSize;
        }
    }
}
