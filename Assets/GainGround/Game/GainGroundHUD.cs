using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        static void HUDRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void HUDTexture(Rect rect, Texture2D texture, Rect uv)
        {
            if (texture != null) GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
        }
    }
}
