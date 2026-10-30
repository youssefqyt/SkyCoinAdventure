using UnityEngine;
using UnityEngine.EventSystems;

namespace SkyCoin
{
    /// <summary>
    /// The invisible full screen area used to turn the camera with a finger.
    ///
    /// It sits BEHIND the joystick and the buttons (first sibling), so a touch on a button
    /// is handled by the button and never turns the camera.
    ///
    /// While a finger drags, mouse look is suppressed. On a phone that changes nothing, but in
    /// the Editor it stops the mouse delta and the drag delta from being applied twice.
    /// </summary>
    public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public MobileControls Owner;

        private bool _dragging;

        public void OnPointerDown(PointerEventData eventData)
        {
            _dragging = true;
            SetMouseLookSuppressed(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || Owner == null) return;
            Owner.AddLook(eventData.delta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _dragging = false;
            SetMouseLookSuppressed(false);
        }

        private void OnDisable()
        {
            _dragging = false;
            SetMouseLookSuppressed(false);
        }

        private static void SetMouseLookSuppressed(bool suppressed)
        {
            if (GameInput.Instance != null) GameInput.Instance.SuppressMouseLook = suppressed;
        }
    }
}
