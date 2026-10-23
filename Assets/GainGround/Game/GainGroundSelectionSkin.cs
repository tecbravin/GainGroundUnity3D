using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        GUIStyle selectionPanel, selectionHeading;
        GUIStyle ironButtonStyle;
        GUIStyle swordSliderThumb;
        Texture2D swordSliderTexture;
        Texture2D selectionFrameTexture;
        Font selectionFont;
        Texture2D ironButtonTexture;
        Texture2D menuBackground;
        Texture2D carouselArrowLeft, carouselArrowRight;
        readonly System.Collections.Generic.Dictionary<GUIStyle, GUIStyle> goldMenuStyles = new System.Collections.Generic.Dictionary<GUIStyle, GUIStyle>();
        void LoadGuiAssets()
        {
            if (persistentResources == null)
                persistentResources = Resources.LoadAll<UnityEngine.Object>(string.Empty);
            // Keep shared GUI resources referenced while stage bundles are released.
            if (selectionFrameTexture == null) selectionFrameTexture = Resources.Load<Texture2D>("SelectionUI/Frame");
            if (selectionFont == null) selectionFont = Resources.Load<Font>("SelectionUI/UncialAntiqua-Regular");
            if (ironButtonTexture == null) ironButtonTexture = Resources.Load<Texture2D>("SelectionUI/IronButton");
            if (menuBackground == null) menuBackground = Resources.Load<Texture2D>("Menu/Menu");
            if (menuBackground == null)
            {
                var menuSprite = Resources.Load<Sprite>("Menu/Menu");
                if (menuSprite != null) menuBackground = menuSprite.texture;
            }
        }
        GUIStyle GoldMenuText(GUIStyle original)
        {
            PrepareSelectionSkin();
            if (!goldMenuStyles.TryGetValue(original, out var style))
            {
                style = new GUIStyle(original) { font = selectionHeading.font, fontStyle = FontStyle.Normal };
                style.normal.textColor = selectionHeading.normal.textColor;
                style.hover.textColor = selectionHeading.normal.textColor;
                style.active.textColor = selectionHeading.normal.textColor;
                style.focused.textColor = selectionHeading.normal.textColor;
                goldMenuStyles.Add(original, style);
            }
            return style;
        }
        void PrepareSelectionSkin()
        {
            LoadGuiAssets();
            if (selectionPanel != null && selectionHeading != null &&
                selectionPanel.normal.background != null && selectionHeading.font != null) return;
            goldMenuStyles.Clear();
            ironButtonStyle = null;
            selectionPanel = new GUIStyle(GUI.skin.box);
            selectionPanel.normal.background = selectionFrameTexture;
            selectionPanel.border = new RectOffset(110, 110, 110, 110);
            selectionHeading = new GUIStyle(GUI.skin.label) {
                font = selectionFont != null ? selectionFont : GUI.skin.label.font,
                fontSize = 21, alignment = TextAnchor.MiddleCenter, wordWrap = true
            };
            selectionHeading.normal.textColor = new Color(.94f, .79f, .44f);
        }
        GUIStyle IronButtonStyle
        {
            get
            {
                if (ironButtonStyle != null) return ironButtonStyle;
                PrepareSelectionSkin();
                ironButtonStyle = new GUIStyle()
                {
                    font = selectionHeading.font,
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    padding = new RectOffset(12, 12, 5, 5),
                    margin = new RectOffset(3, 3, 3, 3),
                    stretchWidth = true
                };
                ironButtonStyle.normal.textColor = new Color(.88f, .87f, .82f);
                ironButtonStyle.hover.textColor = new Color(1f, .88f, .59f);
                ironButtonStyle.active.textColor = Color.white;
                ironButtonStyle.focused.textColor = new Color(1f, .88f, .59f);
                ironButtonStyle.onNormal.textColor = ironButtonStyle.normal.textColor;
                ironButtonStyle.onHover.textColor = ironButtonStyle.hover.textColor;
                ironButtonStyle.onActive.textColor = ironButtonStyle.active.textColor;
                ironButtonStyle.onFocused.textColor = ironButtonStyle.focused.textColor;
                return ironButtonStyle;
            }
        }
        GUIStyle SwordSliderThumb
        {
            get
            {
                if (swordSliderThumb != null) return swordSliderThumb;
                const int width = 20, height = 40;
                swordSliderTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Vertical sword slider marker",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color32[width * height];
                var dark = new Color32(45, 37, 35, 255);
                var silver = new Color32(218, 225, 230, 255);
                var shine = new Color32(255, 255, 245, 255);
                var gold = new Color32(218, 171, 68, 255);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        Color32 color = new Color32(0, 0, 0, 0);
                        // The blade points straight down; hilt and guard remain above it.
                        if (y == 1 && (x == 9 || x == 10)) color = silver;
                        else if (y >= 2 && y <= 22 && x >= 7 && x <= 12)
                            color = x == 7 || x == 12 ? dark : x == 9 ? shine : silver;
                        else if (y >= 23 && y <= 25 && x >= 2 && x <= 17)
                            color = y == 24 ? gold : dark;
                        else if (y >= 26 && y <= 35 && x >= 8 && x <= 11)
                            color = x == 8 || x == 11 ? dark : gold;
                        else if (y >= 36 && y <= 38 && x >= 7 && x <= 12)
                            color = y == 37 ? gold : dark;
                        pixels[y * width + x] = color;
                    }
                swordSliderTexture.SetPixels32(pixels);
                swordSliderTexture.Apply(false, true);
                swordSliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb)
                {
                    fixedWidth = width,
                    fixedHeight = height,
                    border = new RectOffset(0, 0, 0, 0)
                };
                swordSliderThumb.normal.background = swordSliderTexture;
                swordSliderThumb.hover.background = swordSliderTexture;
                swordSliderThumb.active.background = swordSliderTexture;
                swordSliderThumb.focused.background = swordSliderTexture;
                return swordSliderThumb;
            }
        }
        void EnsureIronTexture()
        {
            if (ironButtonTexture == null) LoadGuiAssets();
        }
        public static Rect[] IronFrameRects(Rect bounds)
        {
            float cornerWidth = Mathf.Min(bounds.width * .22f, bounds.height * .42f);
            float cornerHeight = Mathf.Min(bounds.height * .38f, cornerWidth / 1.125f);
            float[] x = { bounds.xMin, bounds.xMin + cornerWidth, bounds.xMax - cornerWidth, bounds.xMax };
            float[] y = { bounds.yMin, bounds.yMin + cornerHeight, bounds.yMax - cornerHeight, bounds.yMax };
            var rects = new Rect[9];
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    rects[row * 3 + column] = new Rect(x[column], y[row], x[column + 1] - x[column], y[row + 1] - y[row]);
            return rects;
        }
        void DrawIronFrame(Rect bounds)
        {
            EnsureIronTexture();
            if (ironButtonTexture == null || Event.current.type != EventType.Repaint) return;
            var slices = IronFrameRects(bounds);
            float sourceWidth = ironButtonTexture.width, sourceHeight = ironButtonTexture.height;
            float[] sourceX = { 0, sourceWidth * .09f, sourceWidth * .91f, sourceWidth };
            float[] sourceY = { sourceHeight, sourceHeight * .76f, sourceHeight * .24f, 0 };
            var previous = GUI.color;
            if (!GUI.enabled)
                GUI.color = new Color(previous.r * .55f, previous.g * .55f, previous.b * .55f, previous.a);
            else if (bounds.Contains(Event.current.mousePosition))
                GUI.color = new Color(previous.r, previous.g * .96f, previous.b * .82f, previous.a);
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                {
                    var uv = new Rect(sourceX[column] / sourceWidth, sourceY[row + 1] / sourceHeight,
                        (sourceX[column + 1] - sourceX[column]) / sourceWidth,
                        (sourceY[row] - sourceY[row + 1]) / sourceHeight);
                    GUI.DrawTextureWithTexCoords(slices[row * 3 + column], ironButtonTexture, uv, true);
                }
            GUI.color = previous;
        }
        bool IronButton(Rect bounds, string text, GUIStyle style = null)
        {
            DrawIronFrame(bounds);
            if (ironButtonTexture == null) return GUI.Button(bounds, text, GUI.skin.button);
            return GUI.Button(bounds, text, style ?? IronButtonStyle);
        }
        bool IronButton(Rect bounds, GUIContent content, GUIStyle style)
        {
            DrawIronFrame(bounds);
            if (ironButtonTexture == null) return GUI.Button(bounds, content, GUI.skin.button);
            return GUI.Button(bounds, content, style);
        }
        bool CarouselArrowButton(Rect bounds, bool left)
        {
            if (carouselArrowLeft == null) carouselArrowLeft = MakeCarouselArrow(true);
            if (carouselArrowRight == null) carouselArrowRight = MakeCarouselArrow(false);
            var previous = GUI.color;
            if (!GUI.enabled) GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * .45f);
            var icon = left ? carouselArrowLeft : carouselArrowRight;
            GUI.DrawTexture(new Rect(bounds.center.x - 16, bounds.center.y - 20, 32, 40), icon);
            GUI.color = previous;
            return GUI.Button(bounds, GUIContent.none, GUIStyle.none);
        }
        static Texture2D MakeCarouselArrow(bool left)
        {
            const int width = 32, height = 40;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[width * height];
            var edge = new Color32(42, 27, 13, 255);
            var gold = new Color32(234, 187, 83, 255);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int column = left ? x : width - 1 - x;
                    int halfHeight = Mathf.RoundToInt((column - 3) * 17f / 25f);
                    if (column < 3 || column > 28 || Mathf.Abs(y - 19.5f) > halfHeight) continue;
                    pixels[y * width + x] = column < 6 || column > 25 ||
                        Mathf.Abs(y - 19.5f) > halfHeight - 2 ? edge : gold;
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
        bool IronLayoutButton(string text, params GUILayoutOption[] options) => IronLayoutButton(text, IronButtonStyle, options);
        bool IronLayoutButton(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var content = new GUIContent(text);
            var bounds = GUILayoutUtility.GetRect(content, style, options);
            return IronButton(bounds, text, style);
        }
        void IronBox(Rect bounds, string text, GUIStyle style)
        {
            DrawIronFrame(bounds);
            GUI.Box(bounds, text, ironButtonTexture != null ? style : GUI.skin.box);
        }
        void DrawSelectionPanel(Rect rect)
        {
            PrepareSelectionSkin();
            var texture = selectionPanel.normal.background;
            if (texture == null) { GUI.Box(rect, ""); return; }
            var previousColor = GUI.color;
            GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * .65f);
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = previousColor;
            const float border = .085f;
            float sideWidth = rect.width * border;
            float edgeHeight = rect.height * border;
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMin, rect.yMin, sideWidth, rect.height), texture,
                new Rect(0, 0, border, 1), true);
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - sideWidth, rect.yMin, sideWidth, rect.height), texture,
                new Rect(1 - border, 0, border, 1), true);
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMin, rect.yMin, rect.width, edgeHeight), texture,
                new Rect(0, 1 - border, 1, border), true);
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMin, rect.yMax - edgeHeight, rect.width, edgeHeight), texture,
                new Rect(0, 0, 1, border), true);
        }
        void DrawMenuBackground(Rect rect)
        {
            if (menuBackground == null) LoadGuiAssets();
            if (menuBackground == null) return;
            var previousColor = GUI.color;
            GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * .5f);
            GUI.DrawTexture(rect, menuBackground, ScaleMode.ScaleAndCrop, true);
            GUI.color = previousColor;
        }
        GUIStyle SelectionHeading { get { PrepareSelectionSkin(); return selectionHeading; } }
    }
}
