using UnityEngine;
using UnityEngine.EventSystems;

namespace GainGround.Game
{
    public sealed class HudTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] UnityEngine.UI.Graphic targetGraphic;
        int pointerId = int.MinValue;
        bool pressed;

        public void Configure(UnityEngine.UI.Graphic graphic)
        {
            targetGraphic = graphic;
            RefreshTint();
        }

        public bool Held => pointerId != int.MinValue;
        public bool ConsumePress()
        {
            bool result = pressed;
            pressed = false;
            return result;
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (Held) return;
            pointerId = eventData.pointerId;
            pressed = true;
            RefreshTint();
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointerId != eventData.pointerId) return;
            pointerId = int.MinValue;
            RefreshTint();
        }
        void RefreshTint()
        {
            if (targetGraphic != null)
                targetGraphic.color = Color.white;
        }
        public void ResetInput()
        {
            pointerId = int.MinValue;
            pressed = false;
            RefreshTint();
        }
        void OnDisable() { ResetInput(); }
    }
}
