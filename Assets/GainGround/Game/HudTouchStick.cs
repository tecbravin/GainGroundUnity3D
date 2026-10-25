using UnityEngine;
using UnityEngine.EventSystems;

namespace GainGround.Game
{
    public sealed class HudTouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform knob;
        RectTransform area;
        int pointerId = int.MinValue;
        public Vector2 Direction { get; private set; }

        void Awake() { area = (RectTransform)transform; }
        public void SetKnob(RectTransform value) { knob = value; }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != int.MinValue) return;
            pointerId = eventData.pointerId;
            OnDrag(eventData);
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (pointerId != eventData.pointerId) return;
            if (area == null) area = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                area, eventData.position, eventData.pressEventCamera, out var point)) return;
            float radius = Mathf.Max(1, Mathf.Min(area.rect.width, area.rect.height) * .38f);
            Direction = Vector2.ClampMagnitude(point / radius, 1);
            if (knob != null) knob.anchoredPosition = Direction * radius * .48f;
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointerId == eventData.pointerId) ResetInput();
        }
        public void ResetInput()
        {
            pointerId = int.MinValue;
            Direction = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }
        void OnDisable() { ResetInput(); }
    }
}
