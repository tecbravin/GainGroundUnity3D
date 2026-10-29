using GainGround.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

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
    static Sprite ShootIcon => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/ShootIcon.png");
    static Sprite SpecialIcon => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/SpecialIcon.png");
    static Sprite DashIcon => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/DashIcon.png");
    static Sprite TimeIcon => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/TimeIcon.png");
    static Sprite EnemyIcon => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/EnemyIcon.png");
    static Sprite PauseIcon => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/PauseIcon.png");
    static Sprite TeamIcon => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/TeamIcon.png");
    static Sprite ExitedIcon => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/ExitedIcon.png");
    static Sprite ScoreFrame => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/ScoreFrame.png");

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

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.transform.SetParent(rect, false);

        var left = Rail(rect, "Player 1 - left", false, new Color(.12f, .28f, .38f, .48f));
        var right = Rail(rect, "Player 2 - right", true, new Color(.36f, .22f, .10f, .48f));
        var timeGroup = HeaderStatus(left, "Time status", TimeIcon, 0);
        var enemyGroup = HeaderStatus(left, "Enemy status", EnemyIcon, 1);
        var timer = Label(timeGroup, "Timer", "120", 30, TextAnchor.MiddleCenter,
            new Vector2(.34f, 0), Vector2.one);
        var enemies = Label(enemyGroup, "Enemies alive", "08", 30, TextAnchor.MiddleCenter,
            new Vector2(.34f, 0), Vector2.one);
        var stage = Label(left, "Stage name", "1-1 / Planície", 30, TextAnchor.MiddleCenter,
            new Vector2(.04f, .715f), new Vector2(.96f, .785f));
        var pause = UiButton(left, "Pause", "", new Vector2(.52f, .63f),
            new Vector2(.94f, .70f), 17);
        ButtonIcon((RectTransform)pause.transform, PauseIcon);
        var scoreFrame = Rect(right, "Score frame", new Vector2(.03f, .81f),
            new Vector2(.97f, .985f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var scoreImage = scoreFrame.gameObject.AddComponent<UnityEngine.UI.Image>();
        scoreImage.sprite = ScoreFrame;
        scoreImage.raycastTarget = false;
        var score = Label(scoreFrame, "Team score", "000000", 36, TextAnchor.MiddleCenter,
            new Vector2(.13f, .33f), new Vector2(.87f, .65f));

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

    static RectTransform HeaderStatus(RectTransform rail, string name, Sprite sprite, int column)
    {
        var group = Rect(rail, name, new Vector2(column * .5f, .89f),
            new Vector2((column + 1) * .5f, .975f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        group.offsetMin = new Vector2(column == 0 ? 30 : 5, 0);
        group.offsetMax = new Vector2(column == 0 ? -5 : -10, 0);
        var icon = Rect(group, "Icon", new Vector2(.02f, .1f), new Vector2(.3f, .9f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var image = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return group;
    }

    static GainGroundHudView.PlayerPanel BuildPlayer(RectTransform rail,
        RectTransform actionRail, int id)
    {
        var result = new GainGroundHudView.PlayerPanel { rail = rail };
        var bottom = Rect(rail, "Player " + id + " roster and portrait",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0),
            new Vector2(0, 260), Vector2.zero);
        result.rosterPanel = bottom.gameObject;
        var background = bottom.gameObject.AddComponent<UnityEngine.UI.Image>();
        background.sprite = WindowSprite;
        background.raycastTarget = false;
        result.selectionOutline = bottom.gameObject.AddComponent<UnityEngine.UI.Outline>();
        result.selectionOutline.effectColor = new Color(1f, .75f, .25f, .55f);
        result.selectionOutline.effectDistance = new Vector2(5f, 5f);
        result.selectionOutline.useGraphicAlpha = false;
        result.selectionOutline.enabled = false;
        var brighten = Rect(bottom, "Selection brightness", Vector2.zero, Vector2.one,
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        result.selectionBrightness = brighten.gameObject.AddComponent<UnityEngine.UI.Image>();
        result.selectionBrightness.color = new Color(1f, 1f, 1f, .2f);
        result.selectionBrightness.raycastTarget = false;
        brighten.gameObject.SetActive(false);
        result.nameText = Label(bottom, "Character name", "P" + id,
            18, TextAnchor.MiddleCenter, new Vector2(.04f, .88f), new Vector2(.96f, .99f));
        if (id == 2) result.nameText.rectTransform.anchoredPosition = new Vector2(0, -20);
        var portraitButton = UiButton(bottom, "Selected portrait - click to confirm", "",
            new Vector2(.2f, .49f), new Vector2(.8f, .87f), 16);
        // Keep the portrait clickable without drawing the iron button behind it.
        var portraitHitArea = portraitButton.GetComponent<UnityEngine.UI.Image>();
        portraitHitArea.sprite = null;
        portraitHitArea.color = Color.clear;
        result.portraitButton = portraitButton;
        var portraitRect = (RectTransform)portraitButton.transform;
        var raw = Rect(portraitRect, "Character portrait", new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(104, 118), Vector2.zero);
        raw.localScale = Vector3.one * 1.56f;
        result.portrait = raw.gameObject.AddComponent<UnityEngine.UI.Image>();
        result.portrait.preserveAspect = true;
        result.portrait.raycastTarget = false;
        result.portrait.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/GainGround/Editable/PS2HUD/" + (id == 1 ? "Johnny" : "Athra") + ".png");
        var sprite = Rect(portraitRect, "Character sprite fallback", new Vector2(.07f, .07f),
            new Vector2(.93f, .93f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        sprite.localScale = Vector3.one * 1.56f;
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
            new Vector2(.02f, .54f), new Vector2(.25f, .82f), 24);
        result.nextButton = UiButton(selection, "Next character", "",
            new Vector2(.75f, .54f), new Vector2(.98f, .82f), 24);
        result.previousButton.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        result.nextButton.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        ArrowIcon((RectTransform)result.previousButton.transform, ArrowLeft);
        ArrowIcon((RectTransform)result.nextButton.transform, ArrowRight);
        result.selectedNameText = Label(bottom, "Selected character name",
            id == 1 ? "JOHNNY" : "ATHRA", 38, TextAnchor.MiddleCenter,
            new Vector2(.04f, .16f), new Vector2(.96f, .32f));
        float statusBottom = id == 1 ? .79f : .715f;
        float statusTop = id == 1 ? .875f : .795f;
        result.rosterText = StatusCount(rail, "Team count", "3", TeamIcon, 0,
            statusBottom, statusTop);
        result.exitedText = StatusCount(rail, "Exited count", "0", ExitedIcon, 1,
            statusBottom, statusTop);
        var bar = Rect(bottom, "Dash bar", new Vector2(.07f, .04f),
            new Vector2(.93f, .12f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var barImage = bar.gameObject.AddComponent<UnityEngine.UI.Image>();
        barImage.color = new Color(.06f, .08f, .1f);
        barImage.raycastTarget = false;
        var fill = Rect(bar, "Dash ready fill", Vector2.zero, Vector2.one,
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        result.dashFill = fill.gameObject.AddComponent<UnityEngine.UI.Image>();
        result.dashFill.color = id == 1 ? new Color(.36f, .8f, 1) : new Color(1, .66f, .28f);
        result.dashFill.raycastTarget = false;

        var touch = Rect(rail, "Player " + id + " touch controls",
            id == 1 ? new Vector2(.02f, .04f) : new Vector2(.03f, .31f),
            id == 1 ? new Vector2(.98f, .69f) : new Vector2(.97f, .67f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        result.touchControls = touch.gameObject;
        var pad = Rect(touch, "Movement pad",
            id == 1 ? new Vector2(.04f, .04f) : new Vector2(.12f, .05f),
            id == 1 ? new Vector2(.96f, .96f) : new Vector2(.88f, .95f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var padImage = pad.gameObject.AddComponent<UnityEngine.UI.Image>();
        padImage.sprite = WindowSprite;
        padImage.preserveAspect = id == 1;
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
            new Vector2(0, .31f), new Vector2(1, .67f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        result.touchActions = actions.gameObject;
        result.fireButton = TouchButton(actions, "Shoot", ShootIcon, false, 0, 1);
        result.specialButton = TouchButton(actions, "Special", SpecialIcon, true, 1, 1);
        result.dashButton = TouchButton(actions, "Dash", DashIcon, true, 0, 0);
        result.switchButton = TouchButton(actions, "Switch character", null, false, 1, 0);
        if (id == 2)
        {
            touch.gameObject.SetActive(false);
            actions.gameObject.SetActive(false);
        }
        return result;
    }

    static HudTouchButton TouchButton(RectTransform parent, string name, Sprite iconSprite,
        bool hasCooldown, int column, int row)
    {
        var rect = Rect(parent, name, new Vector2(column * .5f, row * .5f),
            new Vector2((column + 1) * .5f, (row + 1) * .5f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        rect.offsetMin = new Vector2(column == 0 ? 10 : 5, row == 0 ? 10 : 5);
        rect.offsetMax = new Vector2(column == 0 ? -5 : -10, row == 0 ? -5 : -10);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = ButtonSprite;
        var action = rect.gameObject.AddComponent<HudTouchButton>();
        UnityEngine.UI.Image fill = null;
        if (hasCooldown)
        {
            var fillRect = Rect(rect, "Recharge fill", Vector2.zero, Vector2.one,
                new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            fill = fillRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            fill.sprite = ButtonSprite;
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.raycastTarget = false;
        }
        UnityEngine.UI.Image icon = null;
        if (iconSprite != null)
        {
            var iconRect = Rect(rect, "Icon", new Vector2(.14f, .14f), new Vector2(.86f, .86f),
                new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            icon = iconRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        else Label(rect, "Label", "TROCAR", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        action.Configure(image, fill, icon);
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

    static void ButtonIcon(RectTransform parent, Sprite sprite)
    {
        var rect = Rect(parent, "Pause icon", new Vector2(.3f, .12f),
            new Vector2(.7f, .88f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    static UnityEngine.UI.Text StatusCount(RectTransform parent, string name,
        string value, Sprite iconSprite, int column, float bottom, float top)
    {
        var group = Rect(parent, name + " status",
            new Vector2(column * .5f, bottom),
            new Vector2((column + 1) * .5f, top),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        group.offsetMin = new Vector2(column == 0 ? 10 : 5, 0);
        group.offsetMax = new Vector2(column == 0 ? -5 : -10, 0);
        var icon = Rect(group, "Icon", new Vector2(.02f, .1f),
            new Vector2(.30f, .9f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var image = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = iconSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return Label(group, name, value, 30, TextAnchor.MiddleCenter,
            new Vector2(.34f, 0), Vector2.one);
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
