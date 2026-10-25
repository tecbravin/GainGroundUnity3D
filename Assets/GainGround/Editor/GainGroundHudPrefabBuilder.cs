using GainGround.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class GainGroundHudPrefabBuilder
{
    const string Folder = "Assets/GainGround/Resources/HUD";
    const string PrefabPath = Folder + "/GainGroundHUD.prefab";
    static readonly Color Gold = new Color(.97f, .82f, .5f);
    static Sprite WindowSprite => AssetDatabase.LoadAssetAtPath<Sprite>(
        "Assets/GainGround/Resources/SelectionUI/Frame.png");
    static Sprite ButtonSprite => AssetDatabase.LoadAssetAtPath<Sprite>(
        "Assets/GainGround/Resources/SelectionUI/IronButton.png");
    static Sprite ArrowLeft => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/ArrowLeft.png");
    static Sprite ArrowRight => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/ArrowRight.png");
    static Sprite TouchKnob => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/TouchKnob.png");

    [InitializeOnLoadMethod]
    static void CreateMissingPrefab()
    {
        EditorApplication.delayCall += () => {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) Generate();
        };
    }

    [MenuItem("Gain Ground/UI/Rebuild HUD prefab")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/GainGround/Resources"))
                AssetDatabase.CreateFolder("Assets/GainGround", "Resources");
            AssetDatabase.CreateFolder("Assets/GainGround/Resources", "HUD");
        }
        var root = new GameObject("Gain Ground HUD", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster),
            typeof(GainGroundHudView));
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(1280, 720);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystem.transform.SetParent(rect, false);

        var left = Rail(rect, "Player 1 - left", false, new Color(.12f, .28f, .38f, .48f));
        var right = Rail(rect, "Player 2 - right", true, new Color(.36f, .22f, .10f, .48f));
        var timer = Label(left, "Timer", "TEMPO  120", 25, TextAnchor.MiddleLeft,
            new Vector2(.06f, .927f), new Vector2(.94f, .973f));
        var enemies = Label(left, "Enemies alive", "INIMIGOS  08", 21, TextAnchor.MiddleLeft,
            new Vector2(.06f, .878f), new Vector2(.94f, .927f));
        var stage = Label(left, "Stage name", "1-1 / Planície", 15, TextAnchor.MiddleLeft,
            new Vector2(.06f, .832f), new Vector2(.94f, .88f));
        var pause = UiButton(left, "Pause", "PAUSA", new Vector2(.52f, .69f),
            new Vector2(.94f, .81f), 17);
        var score = Label(right, "Team score", "PONTOS  000000", 24, TextAnchor.MiddleRight,
            new Vector2(.05f, .91f), new Vector2(.95f, .975f));

        var player1 = BuildPlayer(left, right, 1);
        var player2 = BuildPlayer(right, right, 2);
        root.GetComponent<GainGroundHudView>().Configure(canvas, player1, player2,
            timer, enemies, score, stage, pause);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("HUD prefab criado: " + PrefabPath);
    }

    static RectTransform Rail(RectTransform parent, string name, bool right, Color color)
    {
        var rail = Rect(parent, name, right ? new Vector2(1, 0) : Vector2.zero,
            right ? Vector2.one : new Vector2(0, 1),
            right ? new Vector2(1, .5f) : new Vector2(0, .5f),
            new Vector2(290, 0), Vector2.zero);
        var image = rail.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return rail;
    }

    static GainGroundHudView.PlayerPanel BuildPlayer(RectTransform rail,
        RectTransform actionRail, int id)
    {
        var result = new GainGroundHudView.PlayerPanel { rail = rail };
        var bottom = Rect(rail, "Player " + id + " roster and portrait",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0),
            new Vector2(0, 220), Vector2.zero);
        result.rosterPanel = bottom.gameObject;
        var background = bottom.gameObject.AddComponent<UnityEngine.UI.Image>();
        background.sprite = WindowSprite;
        background.raycastTarget = false;
        result.nameText = Label(bottom, "Character name", "P" + id + "  AGUARDANDO",
            18, TextAnchor.MiddleCenter, new Vector2(.04f, .88f), new Vector2(.96f, .99f));
        var portraitButton = UiButton(bottom, "Selected portrait - click to confirm", "",
            new Vector2(.27f, .46f), new Vector2(.73f, .87f), 16);
        result.portraitButton = portraitButton;
        var portraitRect = (RectTransform)portraitButton.transform;
        var raw = Rect(portraitRect, "Character portrait", new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(73, 91), Vector2.zero);
        result.portrait = raw.gameObject.AddComponent<UnityEngine.UI.Image>();
        result.portrait.preserveAspect = true;
        result.portrait.raycastTarget = false;
        result.portrait.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/GainGround/Editable/PS2HUD/" + (id == 1 ? "Johnny" : "Athra") + ".png");
        var sprite = Rect(portraitRect, "Character sprite fallback", new Vector2(.07f, .07f),
            new Vector2(.93f, .93f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        result.spritePortrait = sprite.gameObject.AddComponent<UnityEngine.UI.Image>();
        result.spritePortrait.preserveAspect = true;
        result.spritePortrait.raycastTarget = false;
        result.spritePortrait.gameObject.SetActive(false);
        result.portraitPlaceholder = Label(portraitRect, "Portrait placeholder", "P" + id,
            28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one).gameObject;
        result.portraitPlaceholder.SetActive(result.portrait.sprite == null);
        result.selectionButtons = Rect(bottom, "Character selection arrows", Vector2.zero,
            Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero).gameObject;
        var selection = (RectTransform)result.selectionButtons.transform;
        result.previousButton = UiButton(selection, "Previous character", "",
            new Vector2(.02f, .49f), new Vector2(.25f, .77f), 24);
        result.nextButton = UiButton(selection, "Next character", "",
            new Vector2(.75f, .49f), new Vector2(.98f, .77f), 24);
        ArrowIcon((RectTransform)result.previousButton.transform, ArrowLeft);
        ArrowIcon((RectTransform)result.nextButton.transform, ArrowRight);
        result.selectedNameText = Label(bottom, "Selected character name",
            id == 1 ? "JOHNNY" : "ATHRA", 17, TextAnchor.MiddleCenter,
            new Vector2(.04f, .36f), new Vector2(.96f, .45f));
        result.rosterText = Label(bottom, "Roster status", "EQUIPE 3  |  SALVOS 0",
            14, TextAnchor.MiddleCenter, new Vector2(.04f, .25f), new Vector2(.96f, .35f));
        result.dashText = Label(bottom, "Dash cooldown", "DASH PRONTO",
            16, TextAnchor.MiddleCenter, new Vector2(.04f, .13f), new Vector2(.96f, .24f));
        var bar = Rect(bottom, "Dash bar", new Vector2(.07f, .05f),
            new Vector2(.93f, .11f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var barImage = bar.gameObject.AddComponent<UnityEngine.UI.Image>();
        barImage.color = new Color(.06f, .08f, .1f);
        barImage.raycastTarget = false;
        var fill = Rect(bar, "Dash ready fill", Vector2.zero, Vector2.one,
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        result.dashFill = fill.gameObject.AddComponent<UnityEngine.UI.Image>();
        result.dashFill.color = id == 1 ? new Color(.36f, .8f, 1) : new Color(1, .66f, .28f);
        result.dashFill.raycastTarget = false;

        var touch = Rect(rail, "Player " + id + " touch controls",
            new Vector2(.03f, .31f), new Vector2(.97f, .67f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        result.touchControls = touch.gameObject;
        var pad = Rect(touch, "Movement pad", new Vector2(.12f, .05f),
            new Vector2(.88f, .95f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var padImage = pad.gameObject.AddComponent<UnityEngine.UI.Image>();
        padImage.sprite = WindowSprite;
        var stick = pad.gameObject.AddComponent<HudTouchStick>();
        var knob = Rect(pad, "Movement knob", new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(38, 38), Vector2.zero);
        var knobImage = knob.gameObject.AddComponent<UnityEngine.UI.Image>();
        knobImage.sprite = TouchKnob;
        knobImage.raycastTarget = false;
        stick.SetKnob(knob);
        result.stick = stick;
        var actions = Rect(actionRail, "Player " + id + " touch actions",
            new Vector2(.03f, .31f), new Vector2(.97f, .67f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        result.touchActions = actions.gameObject;
        result.fireButton = TouchButton(actions, "Shoot", "TIRO",
            new Vector2(.01f, .52f), new Vector2(.49f, .95f));
        result.specialButton = TouchButton(actions, "Special", "ESP",
            new Vector2(.51f, .52f), new Vector2(.99f, .95f));
        result.dashButton = TouchButton(actions, "Dash", "DASH",
            new Vector2(.01f, .05f), new Vector2(.49f, .48f));
        result.switchButton = TouchButton(actions, "Switch character", "TROCAR",
            new Vector2(.51f, .05f), new Vector2(.99f, .48f));
        if (id == 2)
        {
            touch.gameObject.SetActive(false);
            actions.gameObject.SetActive(false);
        }
        return result;
    }

    static HudTouchButton TouchButton(RectTransform parent, string name, string text,
        Vector2 min, Vector2 max)
    {
        var rect = Rect(parent, name, min, max, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = ButtonSprite;
        var action = rect.gameObject.AddComponent<HudTouchButton>();
        action.Configure(image);
        Label(rect, "Label", text, 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        return action;
    }

    static UnityEngine.UI.Button UiButton(RectTransform parent, string name, string text,
        Vector2 min, Vector2 max, int fontSize)
    {
        var rect = Rect(parent, name, min, max, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = ButtonSprite;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.transition = UnityEngine.UI.Selectable.Transition.None;
        if (!string.IsNullOrEmpty(text))
            Label(rect, "Label", text, fontSize, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        return button;
    }

    static void ArrowIcon(RectTransform parent, Sprite sprite)
    {
        var rect = Rect(parent, "Arrow sprite", new Vector2(.18f, .12f),
            new Vector2(.82f, .88f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    static UnityEngine.UI.Text Label(RectTransform parent, string name, string text,
        int fontSize, TextAnchor alignment, Vector2 min, Vector2 max)
    {
        var rect = Rect(parent, name, min, max, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
        label.font = Resources.Load<Font>("SelectionUI/UncialAntiqua-Regular");
        label.text = text;
        label.color = Gold;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 9;
        label.resizeTextMaxSize = fontSize;
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
