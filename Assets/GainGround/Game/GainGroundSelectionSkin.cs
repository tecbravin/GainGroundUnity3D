using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        GUIStyle selectionPanel, selectionHeading;
        GUIStyle ironButtonStyle;
        Texture2D ironButtonTexture;
        bool ironTextureLoaded;
        Texture2D menuBackground;
        readonly System.Collections.Generic.Dictionary<GUIStyle, GUIStyle> goldMenuStyles = new System.Collections.Generic.Dictionary<GUIStyle, GUIStyle>();
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
            if (selectionPanel != null) return;
            var texture = Resources.Load<Texture2D>("SelectionUI/Frame");
            selectionPanel = new GUIStyle(GUI.skin.box);
            selectionPanel.normal.background = texture;
            selectionPanel.border = new RectOffset(110, 110, 110, 110);
            selectionHeading = new GUIStyle(GUI.skin.label) {
                font = Resources.Load<Font>("SelectionUI/UncialAntiqua-Regular"),
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
        void EnsureIronTexture()
        {
            if (ironTextureLoaded) return;
            ironButtonTexture = Resources.Load<Texture2D>("SelectionUI/IronButton");
            ironTextureLoaded = true;
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
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        }
        void DrawMenuBackground(Rect rect)
        {
            if (menuBackground == null) menuBackground = Resources.Load<Texture2D>("Menu/Menu");
            if (menuBackground == null) return;
            var previousColor = GUI.color;
            GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * .5f);
            GUI.DrawTexture(rect, menuBackground, ScaleMode.ScaleAndCrop, true);
            GUI.color = previousColor;
        }
        GUIStyle SelectionHeading { get { PrepareSelectionSkin(); return selectionHeading; } }
    }
}
