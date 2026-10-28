using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GainGround.Game
{
    public sealed class HudTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] UnityEngine.UI.Graphic targetGraphic;
        [SerializeField] Image rechargeFill;
        [SerializeField] Image icon;
        static readonly Color ChargingColor = new Color(.28f, .28f, .28f, 1f);
        static readonly Color DimmedIconColor = new Color(.48f, .48f, .48f, 1f);
        static readonly Color DisabledButtonColor = new Color(.16f, .16f, .16f, 1f);
        static readonly Color DisabledIconColor = new Color(.3f, .3f, .3f, 1f);
        int pointerId = int.MinValue;
        bool pressed;
        bool available = true;

        public void Configure(UnityEngine.UI.Graphic graphic, Image fill = null, Image iconImage = null)
        {
            targetGraphic = graphic;
            rechargeFill = fill;
            icon = iconImage;
            RefreshTint();
        }

        public void SetCharge(float ready)
        {
            if (rechargeFill == null) return;
            ready = available ? Mathf.Clamp01(ready) : 0f;
            if (!Mathf.Approximately(rechargeFill.fillAmount, ready)) rechargeFill.fillAmount = ready;
            if (icon != null)
            {
                var color = available ? Color.Lerp(DimmedIconColor, Color.white, ready) : DisabledIconColor;
                if (icon.color != color) icon.color = color;
            }
        }

        public void SetAvailable(bool value)
        {
            if (available == value) return;
            available = value;
            if (!available) ResetInput();
            else RefreshTint();
            if (icon != null && !available) icon.color = DisabledIconColor;
        }

        public bool Held => available && pointerId != int.MinValue;
        public bool ConsumePress()
        {
            bool result = available && pressed;
            pressed = false;
            return result;
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!available || Held) return;
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
                targetGraphic.color = !available ? DisabledButtonColor :
                    rechargeFill != null ? ChargingColor : Color.white;
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
