using GainGround.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class GainGroundStageSelectPrefabBuilder
{
    const string PrefabPath = "Assets/GainGround/Resources/HUD/GainGroundStageSelectUI.prefab";
    const string Art = "Assets/GainGround/Resources/SelectionUI/";
    const string Hud = "Assets/GainGround/Resources/HUD/";
    static readonly Color Gold = new Color(.97f, .82f, .5f);

    [InitializeOnLoadMethod]
    static void CreateMissingPrefab()
    {
        EditorApplication.delayCall += () => {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) Generate();
        };
    }

    [MenuItem("Gain Ground/UI/Rebuild stage selection UI prefab")]
    public static void Generate()
    {
        var root = new GameObject("Gain Ground Stage Selection UI", typeof(RectTransform),
            typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster), typeof(GainGroundStageSelectView));
        var rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(1280, 720);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystem.transform.SetParent(rootRect, false);

        var backdrop = Image(rootRect, "Black backdrop", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, null, Color.black);
        backdrop.raycastTarget = false;
        var menu = Image(rootRect, "Menu artwork 50%", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            Sprite("Assets/GainGround/Resources/Menu/Menu.jpg"), new Color(1, 1, 1, .5f));
        menu.raycastTarget = false;

        var safe = Rect(rootRect, "Safe area", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        var panel = Image(safe, "Stage selection window", new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(1080, 680), Vector2.zero,
            Sprite(Art + "Frame.png"), Color.white);
        panel.raycastTarget = false;
        var panelRect = panel.rectTransform;

        var heading = Label(panelRect, "Heading", "ESCOLHA O ESTÁGIO", 34);
        SetRect(heading.rectTransform, new Vector2(.08f, 1), new Vector2(.92f, 1),
            new Vector2(0, 54), new Vector2(0, -50));
        heading.rectTransform.pivot = new Vector2(.5f, 1);

        var previewFrame = Image(panelRect, "Selected stage frame", new Vector2(.5f, .59f),
            new Vector2(.5f, .59f), new Vector2(350, 354), new Vector2(0, -11),
            Sprite(Art + "Frame.png"), Color.white);
        previewFrame.raycastTarget = true;
        var previewButton = previewFrame.gameObject.AddComponent<UnityEngine.UI.Button>();
        previewButton.targetGraphic = previewFrame;
        previewButton.transition = UnityEngine.UI.Selectable.Transition.None;
        previewButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        var preview = Image(previewFrame.rectTransform, "Selected map preview", Vector2.zero,
            Vector2.one, new Vector2(-82, -42), Vector2.zero, null, Color.white);
        preview.preserveAspect = true;
        preview.enabled = false;
        preview.raycastTarget = false;
        var status = Label(previewFrame.rectTransform, "Preview status", "Carregando prévia...", 24);
        SetRect(status.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(-64, -64), Vector2.zero);

        var name = Label(panelRect, "Stage name", "Estágio", 30);
        SetRect(name.rectTransform, new Vector2(.1f, .22f), new Vector2(.9f, .31f),
            Vector2.zero, new Vector2(0, -10));
        var count = Label(panelRect, "Stage number", "1 / 1", 22);
        SetRect(count.rectTransform, new Vector2(.35f, .15f), new Vector2(.65f, .21f),
            Vector2.zero, new Vector2(0, 9));

        var previousCard = Card(panelRect, "Previous stage", -333,
            out var previousPreview, out var previousName, out var previousCardButton);
        var nextCard = Card(panelRect, "Next stage", 330,
            out var nextPreview, out var nextName, out var nextCardButton);

        var previous = Arrow(panelRect, "Previous arrow", true);
        var next = Arrow(panelRect, "Next arrow", false);
        var start = Button(panelRect, "Start stage", "INICIAR ESTÁGIO", .53f, 20);
        var back = Button(panelRect, "Back", "VOLTAR", -.53f, 20);

        root.GetComponent<GainGroundStageSelectView>().Configure(canvas,
            root.GetComponent<UnityEngine.UI.GraphicRaycaster>(), safe, panelRect,
            previewFrame.rectTransform, preview, status, name, count,
            previousCard, nextCard, previousPreview, nextPreview,
            previousName, nextName, previous, next,
            previousCardButton, nextCardButton, start, back);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("Stage selection UI prefab criado: " + PrefabPath);
    }

    static RectTransform Card(RectTransform parent, string name, float x,
        out UnityEngine.UI.Image preview, out UnityEngine.UI.Text label,
        out UnityEngine.UI.Button button)
    {
        var frame = Image(parent, name, new Vector2(.5f, .59f), new Vector2(.5f, .59f),
            new Vector2(155, 230), new Vector2(x, 0),
            Sprite(Art + "Frame.png"), Color.white);
        button = frame.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = frame;
        button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        frame.raycastTarget = true;
        preview = Image(frame.rectTransform, "Map preview", new Vector2(.06f, .25f),
            new Vector2(.94f, .94f), Vector2.zero, Vector2.zero, null, Color.white);
        preview.preserveAspect = true;
        preview.enabled = false;
        preview.raycastTarget = false;
        label = Label(frame.rectTransform, "Stage name", "Estágio", 15);
        SetRect(label.rectTransform, new Vector2(.06f, .05f), new Vector2(.94f, .24f),
            Vector2.zero, Vector2.zero);
        return frame.rectTransform;
    }

    static UnityEngine.UI.Button Arrow(RectTransform parent, string name, bool left)
    {
        var image = Image(parent, name, new Vector2(.5f, .59f), new Vector2(.5f, .59f),
            new Vector2(44, 48), new Vector2(left ? -335 : 335, 0),
            Sprite(Hud + (left ? "ArrowLeft.png" : "ArrowRight.png")), Color.white);
        image.preserveAspect = true;
        var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        image.raycastTarget = true;
        return button;
    }

    static UnityEngine.UI.Button Button(RectTransform parent, string name,
        string text, float x, int size)
    {
        var image = Image(parent, name, new Vector2(.5f, 0), new Vector2(.5f, 0),
            new Vector2(245, 46), new Vector2(x * 245, 82),
            Sprite(Art + "IronButton.png"), Color.white);
        var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        image.raycastTarget = true;
        var label = Label(image.rectTransform, "Label", text, size);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(-24, -6), Vector2.zero);
        return button;
    }

    static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    static UnityEngine.UI.Image Image(RectTransform parent, string name,
        Vector2 min, Vector2 max, Vector2 size, Vector2 position,
        Sprite sprite, Color color)
    {
        var rect = Rect(parent, name, min, max, size, position);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static UnityEngine.UI.Text Label(RectTransform parent, string name, string value, int size)
    {
        var rect = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
        label.font = Resources.Load<Font>("SelectionUI/UncialAntiqua-Regular");
        label.text = value;
        label.fontSize = size;
        label.color = Gold;
        label.alignment = TextAnchor.MiddleCenter;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 11;
        label.resizeTextMaxSize = size;
        label.raycastTarget = false;
        return label;
    }

    static RectTransform Rect(RectTransform parent, string name, Vector2 min,
        Vector2 max, Vector2 size, Vector2 position)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        SetRect(rect, min, max, size, position);
        return rect;
    }

    static void SetRect(RectTransform rect, Vector2 min, Vector2 max,
        Vector2 size, Vector2 position)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
