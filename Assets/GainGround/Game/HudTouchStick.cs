using UnityEngine;
using UnityEngine.EventSystems;

namespace GainGround.Game
{
    public sealed class HudTouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform knob;
        [SerializeField, Range(.5f, 2f)] float sensitivity = 1.35f;
        [SerializeField, Range(0f, .2f)] float deadZone = .02f;
        RectTransform area;
        Camera pointerCamera;
        int pointerId = int.MinValue;
        public Vector2 Direction { get; private set; }

        void Awake() { area = (RectTransform)transform; }
        public void SetKnob(RectTransform value) { knob = value; }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != int.MinValue) return;
            pointerId = eventData.pointerId;
            pointerCamera = eventData.pressEventCamera;
            OnDrag(eventData);
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (pointerId != eventData.pointerId) return;
            SetPointerPosition(eventData.position, eventData.pressEventCamera);
        }
        void Update()
        {
            if (pointerId == int.MinValue) return;
            if (TryGetHeldPointer(out var position)) SetPointerPosition(position, pointerCamera);
            else ResetInput();
        }
        bool TryGetHeldPointer(out Vector2 position)
        {
            if (pointerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.fingerId != pointerId) continue;
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) break;
                    position = touch.position;
                    return true;
                }
            }
            // The Device Simulator can stop reporting a touch after it crosses
            // a control's bounds while the simulated mouse button is still held.
            if ((pointerId < 0 || Input.touchCount == 0) && Input.GetMouseButton(0))
            {
                position = Input.mousePosition;
                return true;
            }
            position = Vector2.zero;
            return false;
        }
        void SetPointerPosition(Vector2 screenPosition, Camera eventCamera)
        {
            if (area == null) area = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                area, screenPosition, eventCamera, out var point)) return;
            Vector2 offset = point - area.rect.center;
            float frameSide = Mathf.Min(area.rect.width, area.rect.height);
            float radius = Mathf.Max(1, frameSide * .38f);
            float amount = Mathf.Clamp01(offset.magnitude / radius * sensitivity);
            amount = amount <= deadZone ? 0 : (amount - deadZone) / (1 - deadZone);
            Direction = offset.sqrMagnitude > 0 ? offset.normalized * amount : Vector2.zero;
            if (knob != null)
            {
                float knobRadius = Mathf.Max(knob.rect.width, knob.rect.height) * .5f;
                float travel = Mathf.Max(0, frameSide * .5f - knobRadius - frameSide * .02f);
                knob.anchoredPosition = new Vector2(
                    Mathf.Clamp(offset.x, -travel, travel),
                    Mathf.Clamp(offset.y, -travel, travel));
            }
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointerId != eventData.pointerId) return;
            if (TryGetHeldPointer(out var position)) SetPointerPosition(position, pointerCamera);
            else ResetInput();
        }
        public void ResetInput()
        {
            pointerId = int.MinValue;
            pointerCamera = null;
            Direction = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }
        void OnDisable() { ResetInput(); }
    }
}
