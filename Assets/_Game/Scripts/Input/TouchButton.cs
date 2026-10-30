using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SkyCoin
{
    /// <summary>
    /// A round on-screen button.
    ///
    /// The press is reported on POINTER DOWN, not on click, so jump feels immediate instead of
    /// waiting for the finger to lift. OnUp is also called when the button disappears, which is
    /// what stops "sprint" staying held after the pause menu opens.
    /// </summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action OnDown;
        public Action OnUp;

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnUp?.Invoke();
        }

        private void OnDisable()
        {
            OnUp?.Invoke();
        }
    }
}
