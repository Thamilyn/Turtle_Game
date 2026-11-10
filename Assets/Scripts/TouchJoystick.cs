using UnityEngine;
using UnityEngine.EventSystems;

namespace TurtleGame
{
    /// <summary>A single captured finger drives analog movement; other fingers remain free for UI.</summary>
    public sealed class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform handle;
        private int? pointerId;
        public Vector2 Value { get; private set; }

        public void Initialize(RectTransform thumb)
        {
            handle = thumb;
            ResetInput();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId.HasValue || eventData.button != PointerEventData.InputButton.Left)
                return;
            pointerId = eventData.pointerId;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (pointerId != eventData.pointerId || handle == null)
                return;
            var area = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                area, eventData.position, eventData.pressEventCamera, out Vector2 position))
                return;
            float radius = Mathf.Min(area.rect.width, area.rect.height) * 0.5f;
            float travel = Mathf.Max(1f, radius - handle.rect.width * 0.5f);
            Vector2 displacement = Vector2.ClampMagnitude(position - area.rect.center, travel);
            handle.anchoredPosition = displacement;
            Vector2 input = displacement / travel;
            const float deadZone = 0.12f;
            Value = input.magnitude <= deadZone ? Vector2.zero
                : input.normalized * Mathf.InverseLerp(deadZone, 1f, input.magnitude);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointerId == eventData.pointerId)
                ResetInput();
        }

        public void ResetInput()
        {
            pointerId = null;
            Value = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }

        private void OnDisable() => ResetInput();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ResetInput();
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused) ResetInput();
        }
    }
}
