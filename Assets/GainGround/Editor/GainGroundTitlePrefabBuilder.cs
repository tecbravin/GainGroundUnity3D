using GainGround.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class GainGroundTitlePrefabBuilder
{
    const string PrefabPath = "Assets/GainGround/Resources/HUD/GainGroundTitleUI.prefab";
    const string Art = "Assets/GainGround/Resources/PS2Title/";
    const string Skin = "Assets/GainGround/Resources/SelectionUI/";
    static readonly Color Gold = new Color(.97f, .82f, .5f);

    [InitializeOnLoadMethod]
    static void CreateMissingPrefab()
    {
        EditorApplication.delayCall += () => {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) Generate();
        };
    }

    [MenuItem("Gain Ground/UI/Rebuild title UI prefab")]
    public static void Generate()
    {
        var root = new GameObject("Gain Ground Title UI", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster),
            typeof(GainGroundTitleView));
        var rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(1280, 720);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.transform.SetParent(rootRect, false);

        var backdrop = Rect(rootRect, "Black backdrop", Vector2.zero, Vector2.one,
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var backdropImage = backdrop.gameObject.AddComponent<UnityEngine.UI.Image>();
        backdropImage.color = Color.black;
        backdropImage.raycastTarget = false;

        var left = Artwork(rootRect, "Title_L", new Vector2(0, 0), new Vector2(.5f, 1),
            Sprite(Art + "TITLE_L.png"));
        var right = Artwork(rootRect, "Title_R", new Vector2(.5f, 0), Vector2.one,
            Sprite(Art + "TITLE_R.png"));

        var safe = Rect(rootRect, "Safe area", Vector2.zero, Vector2.one,
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var logo = Artwork(safe, "GAIN GROUND animated logo", new Vector2(.5f, 1),
            new Vector2(.5f, 1), Sprite(Art + "LOGO_01.png"));
        logo.rectTransform.pivot = new Vector2(.5f, 1);
        logo.rectTransform.sizeDelta = new Vector2(780, 430);
        logo.rectTransform.anchoredPosition = new Vector2(0, -8);
        var logoFrames = new Sprite[5];
        for (int i = 0; i < logoFrames.Length; i++)
            logoFrames[i] = Sprite(Art + "LOGO_0" + (i + 1) + ".png");
        var reforge = Artwork(safe, "Reforged", new Vector2(.5f, 1),
            new Vector2(.5f, 1), Sprite(Art + "REFORGED.png"));
        reforge.rectTransform.pivot = new Vector2(.5f, 1);
        reforge.rectTransform.sizeDelta = new Vector2(400, 133);
        reforge.rectTransform.anchoredPosition = new Vector2(0, -8);
        reforge.preserveAspect = true;
        reforge.color = new Color(1, 1, 1, 0);
        var gainHit = Rect(logo.rectTransform, "GAIN secret click area",
            new Vector2(.27f, .48f), new Vector2(.75f, .67f), new Vector2(.5f, .5f),
            Vector2.zero, Vector2.zero);
        var gainImage = gainHit.gameObject.AddComponent<UnityEngine.UI.Image>();
        gainImage.color = new Color(1, 1, 1, 0);
        var gainButton = gainHit.gameObject.AddComponent<UnityEngine.UI.Button>();
        gainButton.transition = UnityEngine.UI.Selectable.Transition.None;
        gainButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        gainButton.targetGraphic = gainImage;

        var panel = Rect(safe, "Main menu window", new Vector2(.5f, 0),
            new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(600, 344),
            new Vector2(0, 16));
        var panelImage = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
        panelImage.sprite = Sprite(Skin + "Frame.png");
        panelImage.raycastTarget = false;
        var scroll = panel.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24;

        var viewport = Rect(panel, "Viewport", Vector2.zero, Vector2.one,
            new Vector2(.5f, .5f), new Vector2(-90, -64), Vector2.zero);
        var viewportImage = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = true;
        var mask = viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = false;
        var content = Rect(viewport, "Content", new Vector2(0, 1), Vector2.one,
            new Vector2(.5f, 1), Vector2.zero, Vector2.zero);
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 14, 14);
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;

        var record = Label(content, "High score", "RECORDE: 0", 23);
        var recordHeight = record.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        recordHeight.preferredHeight = 42;
        string[] names = { "Solo", "Duo", "Continue", "Menu", "Sair" };
        var buttons = new UnityEngine.UI.Button[names.Length];
        var markers = new UnityEngine.UI.Image[names.Length];
        var heights = new UnityEngine.UI.LayoutElement[names.Length];
        for (int i = 0; i < names.Length; i++)
            buttons[i] = MenuButton(content, names[i], 20, out markers[i], out heights[i]);
        var roster = MenuButton(content, "Grupo: iniciais + resgates [F2]", 16,
            out _, out var rosterHeight);
        var rosterLabel = roster.GetComponentInChildren<UnityEngine.UI.Text>();

        var prompt = Label(safe, "Intro prompt", "Pressione um botão ou toque para continuar", 24);
        var promptRect = prompt.rectTransform;
        promptRect.anchorMin = new Vector2(.08f, 0);
        promptRect.anchorMax = new Vector2(.92f, 0);
        promptRect.pivot = new Vector2(.5f, 0);
        promptRect.sizeDelta = new Vector2(0, 52);
        promptRect.anchoredPosition = new Vector2(0, 28);
        prompt.gameObject.SetActive(false);

        root.GetComponent<GainGroundTitleView>().Configure(canvas,
            root.GetComponent<UnityEngine.UI.GraphicRaycaster>(), safe, left, right,
            logo, logoFrames, reforge, panel, scroll, layout, record, recordHeight, buttons,
            markers, heights, roster, rosterHeight, rosterLabel, prompt, gainButton);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("Title UI prefab criado: " + PrefabPath);
    }

    static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    static UnityEngine.UI.Image Artwork(RectTransform parent, string name, Vector2 min,
        Vector2 max, Sprite sprite)
    {
        var rect = Rect(parent, name, min, max, new Vector2(.5f, .5f),
            Vector2.zero, Vector2.zero);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    static UnityEngine.UI.Button MenuButton(RectTransform parent, string name, int fontSize,
        out UnityEngine.UI.Image marker, out UnityEngine.UI.LayoutElement height)
    {
        var rect = Rect(parent, name, Vector2.zero, Vector2.one,
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        height = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        height.preferredHeight = 48;
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = Sprite(Skin + "IronButton.png");
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        marker = Artwork(rect, "Selected marker", new Vector2(0, .5f),
            new Vector2(0, .5f), Sprite("Assets/GainGround/Resources/HUD/ArrowRight.png"));
        marker.rectTransform.sizeDelta = new Vector2(25, 25);
        marker.rectTransform.anchoredPosition = new Vector2(20, 0);
        marker.gameObject.SetActive(false);
        var label = Label(rect, "Label", name, fontSize);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(34, 0);
        label.rectTransform.offsetMax = new Vector2(-16, 0);
        return button;
    }

    static UnityEngine.UI.Text Label(RectTransform parent, string name, string value, int size)
    {
        var rect = Rect(parent, name, Vector2.zero, Vector2.one,
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
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
        Vector2 max, Vector2 pivot, Vector2 size, Vector2 position)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }
}
