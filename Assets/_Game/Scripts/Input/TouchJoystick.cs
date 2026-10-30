using UnityEngine;
using UnityEngine.EventSystems;

namespace SkyCoin
{
    /// <summary>
    /// The left virtual joystick.
    ///
    /// It lives on the round background image. The EventSystem sends pointer down / drag / up
    /// to this object, so one finger can hold the stick while a second finger drags the camera.
    /// The knob is only a picture: the value is computed from where the finger is relative to
    /// the centre, which is what makes the stick feel right even when the thumb slides outside
    /// the circle.
    /// </summary>
    public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public MobileControls Owner;
        public RectTransform Background;
        public RectTransform Knob;

        private bool _dragging;

        public void OnPointerDown(PointerEventData eventData)
        {
            _dragging = true;
            UpdateFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging) UpdateFromPointer(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _dragging = false;
            ResetKnob();
        }

        private void OnDisable()
        {
            _dragging = false;
            ResetKnob();
        }

        private void UpdateFromPointer(PointerEventData eventData)
        {
            if (Owner == null || Background == null) return;

            // Convert the screen position into the joystick's local space.
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    Background, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            float radius = Background.rect.width * 0.5f;
            if (radius <= 1f) return;

            Vector2 clamped = Vector2.ClampMagnitude(local, radius);

            if (Knob != null) Knob.anchoredPosition = clamped;

            Vector2 value = clamped / radius;
            float magnitude = value.magnitude;

            // Dead zone, then rescale the rest so the stick still reaches full speed at the edge.
            if (magnitude < Owner.DeadZone)
            {
                value = Vector2.zero;
            }
            else
            {
                float scaled = (magnitude - Owner.DeadZone) / (1f - Owner.DeadZone);
                value = value.normalized * Mathf.Clamp01(scaled);
            }

            Owner.SetMove(value);
        }

        private void ResetKnob()
        {
            if (Knob != null) Knob.anchoredPosition = Vector2.zero;
            if (Owner != null) Owner.SetMove(Vector2.zero);
        }
    }
}
