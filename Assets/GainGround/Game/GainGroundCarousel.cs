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
        void DrawCharacterCarousel(PlayerState p, Rect bounds, bool mobile)
        {
            var previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.TRS(bounds.position, Quaternion.identity,
                new Vector3(bounds.width / 480, bounds.height / 150, 1));
            var text = mobile ? mobileLabel : labelStyle;
            var button = mobile ? mobileButton : IronButtonStyle;
            DrawSelectionPanel(new Rect(-8, -8, 496, 166));
            GUI.Label(new Rect(8, 2, 464, 25), "P" + (p.id + 1) + " - SELECIONE O CAMPEÃO", SelectionHeading);
            int count = 0;
            foreach (var member in p.party) if (!member.lost && !member.escaped) count++;
            if (count == 0) { GUI.matrix = previous; return; }
            if (p.selectionIndex < 0 || p.selectionIndex >= p.party.Count ||
                p.party[p.selectionIndex].lost || p.party[p.selectionIndex].escaped)
                p.selectionIndex = CarouselNeighbor(p, p.selectionIndex, 1);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && count > 1;
            if (CarouselArrowButton(new Rect(6, 49, 46, 55), true)) p.selectionIndex = CarouselNeighbor(p, p.selectionIndex, -1);
            if (CarouselArrowButton(new Rect(428, 49, 46, 55), false)) p.selectionIndex = CarouselNeighbor(p, p.selectionIndex, 1);
            GUI.enabled = enabled;
            int selected = p.selectionIndex;
            int left = Mathf.Min(2, (count - 1) / 2), right = Mathf.Min(2, count / 2);
            for (int slot = -left; slot <= right; slot++)
            {
                int index = selected;
                for (int step = 0; step < Mathf.Abs(slot); step++) index = CarouselNeighbor(p, index, slot < 0 ? -1 : 1);
                bool center = slot == 0;
                float width = center ? 78 : 56, height = center ? 82 : 62;
                var rect = new Rect(240 + slot * 70 - width / 2, center ? 30 : 40, width, height);
                HUDRect(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6),
                    center ? p.Tint : new Color(.18f, .22f, .28f));
                if (IronButton(rect, GUIContent.none, IronButtonStyle))
                {
                    ChooseCharacter(p, index);
                    if (mobile) { mobilePlayer = p.id; ResetMobileInput(); }
                }
                var hero = p.party[index].definition;
                var image = new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6);
                if (hero.hudPortrait != null) GUI.DrawTexture(image, hero.hudPortrait, ScaleMode.ScaleToFit, true);
                else if (hero.sprite != null)
                {
                    var sprite = hero.sprite; var r = sprite.textureRect; var t = sprite.texture;
                    float fit = Mathf.Min(image.width / r.width, image.height / r.height);
                    var fitted = new Rect(image.center.x - r.width * fit / 2, image.center.y - r.height * fit / 2, r.width * fit, r.height * fit);
                    HUDTexture(fitted, t, new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height));
                }
            }
            GUI.Label(new Rect(52, 116, 376, 28), p.party[p.selectionIndex].definition.displayName, SelectionHeading);
            if (selectionPortraitsLoading)
                GUI.Label(new Rect(85, 93, 310, 24), "Carregando retratos...", SelectionHeading);
            GUI.matrix = previous;
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
