using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// Keeps a UI panel inside the part of the screen that is actually visible.
    ///
    /// Phones have notches, rounded corners and a home indicator. <see cref="Screen.safeArea"/>
    /// reports the rectangle that is safe to draw in; this component copies it onto its own
    /// RectTransform. It is re-applied whenever the screen size or the safe area changes, so it
    /// also survives rotating the phone.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea == _lastSafeArea &&
                Screen.width == _lastWidth &&
                Screen.height == _lastHeight)
            {
                return;
            }

            Apply();
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            if (_rect == null || Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = _lastSafeArea.position;
            Vector2 max = _lastSafeArea.position + _lastSafeArea.size;

            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
