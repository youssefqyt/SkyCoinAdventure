using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SkyCoin
{
    /// <summary>
    /// The only thing in the project allowed to change scenes.
    ///
    /// It exists to guarantee three things that are normally broken in student projects:
    ///   1. Time.timeScale is restored BEFORE the new scene loads, so a level can never
    ///      start frozen because the previous one ended in a pause menu or a Game Over.
    ///   2. Double loads are impossible (a second request while fading is ignored).
    ///   3. A scene that is missing from the Build Profiles scene list produces a clear,
    ///      actionable error instead of a silent no-op.
    ///
    /// The black fade overlay is generated in code, so no scene needs to contain it.
    /// While idle the overlay does not block clicks (raycastTarget = false).
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Fade")]
        [SerializeField] private float fadeOutDuration = 0.25f;
        [SerializeField] private float fadeInDuration = 0.25f;
        [SerializeField] private float holdBlackDuration = 0.08f;

        private Image _fadeImage;
        private bool _isLoading;

        public bool IsLoading => _isLoading;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            CreateFadeOverlay();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Loads a scene by name, with a fade. Safe to call from any UI button.</summary>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneLoader] Tried to load an empty scene name.");
                return;
            }

            if (_isLoading)
            {
                Debug.LogWarning($"[SceneLoader] Already loading a scene. Ignoring the request for '{sceneName}'.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not in the build list. " +
                               "Add it in File > Build Profiles > Scene List, then try again.");
                return;
            }

            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            _isLoading = true;

            // Never fade while the game is frozen.
            Time.timeScale = 1f;

            if (_fadeImage != null) _fadeImage.raycastTarget = true;

            yield return Fade(0f, 1f, fadeOutDuration);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResetForNewScene();
            }
            else
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            while (operation != null && !operation.isDone)
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(holdBlackDuration);
            yield return Fade(1f, 0f, fadeInDuration);

            if (_fadeImage != null) _fadeImage.raycastTarget = false;
            _isLoading = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (_fadeImage == null) yield break;

            if (duration <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Unscaled: the fade must work while the game is paused or over.
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            if (_fadeImage == null) return;
            Color color = _fadeImage.color;
            color.a = Mathf.Clamp01(alpha);
            _fadeImage.color = color;
        }

        private void CreateFadeOverlay()
        {
            GameObject canvasObject = new GameObject("FadeCanvas (auto)");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;   // above every in-game panel

            GameObject imageObject = new GameObject("FadeImage");
            imageObject.transform.SetParent(canvasObject.transform, false);

            _fadeImage = imageObject.AddComponent<Image>();
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);
            _fadeImage.raycastTarget = false;

            RectTransform rect = _fadeImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
