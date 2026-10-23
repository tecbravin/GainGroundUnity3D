using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        int CarouselNeighbor(PlayerState p, int index, int delta)
        {
            for (int n = 1; n <= p.party.Count; n++)
            {
                int next = ((index + delta * n) % p.party.Count + p.party.Count) % p.party.Count;
                if (!p.party[next].lost && !p.party[next].escaped) return next;
            }
            return index;
        }
        void DrawStageCarousel(Rect bounds, bool mobile)
        {
            DrawSelectionPanel(bounds);
            DrawMenuBackground(bounds);
            if (stages == null || stages.Length == 0)
            {
                GUI.Label(new Rect(bounds.x + 20, bounds.y + bounds.height * .4f, bounds.width - 40, 50),
                    "Nenhum estágio disponível", SelectionHeading);
                return;
            }

            stageSelectIndex = (stageSelectIndex % stages.Length + stages.Length) % stages.Length;
            float scale = Mathf.Min(bounds.width / 480f, bounds.height / 340f);
            float width = 480 * scale, height = 340 * scale;
            var previousMatrix = GUI.matrix;
            GUI.matrix = previousMatrix * Matrix4x4.TRS(
                new Vector3(bounds.center.x - width / 2, bounds.center.y - height / 2), Quaternion.identity,
                new Vector3(scale, scale, 1));

            var button = mobile ? mobileButton : IronButtonStyle;
            GUI.Label(new Rect(8, 3, 464, 32), "ESCOLHA O ESTÁGIO", SelectionHeading);
            bool wasEnabled = GUI.enabled;
            DrawStagePreview(stageSelectIndex, new Rect(80, 47, 320, 205), true);
            if (stages.Length > 1)
            {
                int previous = (stageSelectIndex + stages.Length - 1) % stages.Length;
                int next = (stageSelectIndex + 1) % stages.Length;
                DrawStagePreview(previous, new Rect(22, 107, 54, 88), false);
                DrawStagePreview(next, new Rect(404, 107, 54, 88), false);
            }
            GUI.enabled = wasEnabled && stages.Length > 1;
            if (CarouselArrowButton(new Rect(8, 204, 54, 48), true))
                stageSelectIndex = (stageSelectIndex + stages.Length - 1) % stages.Length;
            if (CarouselArrowButton(new Rect(418, 204, 54, 48), false))
                stageSelectIndex = (stageSelectIndex + 1) % stages.Length;
            GUI.enabled = wasEnabled;

            string name = stages[stageSelectIndex] != null ? stages[stageSelectIndex].displayName : "Estágio";
            GUI.Label(new Rect(64, 256, 352, 30), name, SelectionHeading);
            GUI.Label(new Rect(130, 282, 220, 22), (stageSelectIndex + 1) + " / " + stages.Length, SelectionHeading);
            if (IronButton(new Rect(130, 306, 220, 42), mobile ? "JOGAR" : "INICIAR ESTÁGIO", button))
            {
                StartAtSelectedStage(stageSelectIndex);
                GUI.matrix = previousMatrix;
                GUIUtility.ExitGUI();
            }
            if (IronButton(new Rect(8, 306, 112, 42), "VOLTAR", button)) state = ScreenState.Title;
            GUI.matrix = previousMatrix;
        }

        void DrawStagePreview(int index, Rect rect, bool selected)
        {
            var stageInfo = stages[index];
            HUDRect(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6),
                selected ? new Color(.62f, .48f, .25f) : new Color(.18f, .22f, .28f));
            if (stageInfo == null || stageInfo.background == null)
            {
                GUI.Label(rect, selected && streamStageMedia ?
                    (string.IsNullOrEmpty(mediaError) ? "Carregando prévia..." : "Prévia indisponível") :
                    (index + 1).ToString(), SelectionHeading);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) stageSelectIndex = index;
                return;
            }
            var sprite = stageInfo.background;
            var source = sprite.textureRect;
            var fit = Mathf.Min(rect.width / source.width, rect.height / source.height);
            var fitted = new Rect(rect.center.x - source.width * fit / 2, rect.center.y - source.height * fit / 2,
                source.width * fit, source.height * fit);
            HUDTexture(fitted, sprite.texture, new Rect(source.x / sprite.texture.width, source.y / sprite.texture.height,
                source.width / sprite.texture.width, source.height / sprite.texture.height));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) stageSelectIndex = index;
        }
    }
}
