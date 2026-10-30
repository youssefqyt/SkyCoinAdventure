using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SkyCoin
{
    /// <summary>
    /// On-screen touch controls for phones and tablets.
    ///
    /// WHY THIS FILE EXISTS
    /// The game was written for keyboard + mouse. This component builds the missing phone
    /// controls at runtime, so no scene, prefab or Canvas has to be edited:
    ///
    ///   left  : virtual joystick  -> movement (camera relative, like WASD)
    ///   right : full screen drag  -> camera look (like the mouse)
    ///   bottom: SAUT / COURIR     -> jump and sprint
    ///   top   : II                -> pause
    ///
    /// It writes into <see cref="GameInput"/> through the "virtual input" API, so the player,
    /// the camera and the game manager do not need to know a phone exists.
    ///
    /// VISIBILITY
    /// The controls only appear while <see cref="GameState.Playing"/>: they hide themselves in
    /// the main menu, the pause menu, the victory panel and the game over panel. That also
    /// means they can never steal a click from a menu button.
    ///
    /// Desktop builds never show them. Inside the Unity Editor they are shown by default
    /// (<see cref="showInEditor"/>) so the whole thing can be tested with the mouse: drag with
    /// the left button to look, drag the joystick to walk, click the buttons to jump / run.
    /// </summary>
    [DefaultExecutionOrder(-350)]
    public class MobileControls : MonoBehaviour
    {
        public static MobileControls Instance { get; private set; }

        [Header("Activation")]
        [Tooltip("Show the touch controls inside the Unity Editor so they can be tested with the mouse. " +
                 "Desktop builds ignore this flag and never show them.")]
        [SerializeField] private bool showInEditor = true;

        [Header("Touch feel")]
        [Tooltip("Multiplier applied to a finger drag before it reaches the camera. " +
                 "Raise it if turning feels too slow, lower it if it feels too twitchy.")]
        [SerializeField] private float lookScale = 1.25f;

        [Tooltip("Joystick dead zone. 0 = reacts instantly, 0.9 = the stick must nearly reach the edge.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float deadZone = 0.15f;

        [Header("Layout (1920 x 1080 reference)")]
        [SerializeField] private float joystickDiameter = 280f;
        [SerializeField] private float jumpButtonSize = 200f;
        [SerializeField] private float sprintButtonSize = 150f;
        [SerializeField] private float pauseButtonSize = 100f;

        private GameObject _canvasObject;
        private RectTransform _safeArea;
        private bool _subscribed;
        private static Sprite _circleSprite;

        public float LookScale => lookScale;
        public float DeadZone => deadZone;

        /// <summary>True on a real phone, or in the Editor when the preview flag is on.</summary>
        private bool ControlsAllowed =>
            Application.isMobilePlatform ||
            (Application.isEditor && showInEditor) ||
            Touchscreen.current != null;

        /// <summary>
        /// Editor only: the touch controls are being previewed with the mouse, so the game must
        /// not lock the cursor - a locked cursor sits in the middle of the screen and no button
        /// or joystick could ever be clicked.
        /// </summary>
        public bool IsEditorPreview => Application.isEditor && !Application.isMobilePlatform && showInEditor;

        // ------------------------------------------------------------------
        //  Lifetime
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            MobilePlatformSettings.Apply();
            Build();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StateChanged += HandleStateChanged;
                _subscribed = true;
            }

            if (GameInput.Instance != null)
            {
                GameInput.Instance.UsingTouchControls = ControlsAllowed;
            }

            // GameManager may already have applied its first state before this Start() ran,
            // so pull the current value once instead of waiting for the next event.
            HandleStateChanged(GameManager.Instance != null ? GameManager.Instance.State : GameState.Boot);
            LocaliseMenuForMobile();
            FitHudToSafeArea();
        }

        private void OnDestroy()
        {
            if (_subscribed && GameManager.Instance != null)
            {
                GameManager.Instance.StateChanged -= HandleStateChanged;
                _subscribed = false;
            }

            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        //  State / scene reactions
        // ------------------------------------------------------------------

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LocaliseMenuForMobile();
            FitHudToSafeArea();
        }

        /// <summary>
        /// The HUD is placed 40 px from the screen edges, which is exactly where a notch or a
        /// rounded corner sits in landscape. Fitting the HUD group to the safe area keeps the
        /// coin counter and the countdown readable on those phones.
        /// </summary>
        private static void FitHudToSafeArea()
        {
            if (!Application.isMobilePlatform) return;

            GameObject hud = GameObject.Find("HUD");
            if (hud == null) return;
            if (hud.GetComponent<SafeAreaFitter>() == null) hud.AddComponent<SafeAreaFitter>();
        }

        private void HandleStateChanged(GameState state)
        {
            bool visible = ControlsAllowed && state == GameState.Playing;

            if (_canvasObject != null && _canvasObject.activeSelf != visible)
            {
                _canvasObject.SetActive(visible);
            }

            if (visible && IsEditorPreview)
            {
                // Undo the FPS style cursor lock applied by GameManager for Playing, otherwise
                // the pointer is frozen in the middle of the screen and nothing can be clicked.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Never leave a finger value stuck when the controls disappear
            // (for example: sprint held while the pause menu opens).
            if (!visible) ResetInput();
        }

        /// <summary>Swaps the keyboard-only hint at the bottom of the main menu.</summary>
        private static void LocaliseMenuForMobile()
        {
            if (!Application.isMobilePlatform) return;

            GameObject footer = GameObject.Find("Footer");
            if (footer == null) return;

            TMP_Text label = footer.GetComponent<TMP_Text>();
            if (label != null) label.text = "Touche une option pour jouer";
        }

        // ------------------------------------------------------------------
        //  Values written by the touch widgets
        // ------------------------------------------------------------------

        public void SetMove(Vector2 value)
        {
            if (GameInput.Instance != null) GameInput.Instance.SetVirtualMove(value);
        }

        public void AddLook(Vector2 screenDelta)
        {
            if (GameInput.Instance != null) GameInput.Instance.AddVirtualLook(screenDelta * lookScale);
        }

        public void JumpPressed()
        {
            if (GameInput.Instance != null) GameInput.Instance.QueueJump();
        }

        public void SetSprint(bool held)
        {
            if (GameInput.Instance != null) GameInput.Instance.SetVirtualSprint(held);
        }

        public void PausePressed()
        {
            // GameManager already ignores this unless the game is Playing.
            if (GameManager.Instance != null) GameManager.Instance.TogglePause();
        }

        private void ResetInput()
        {
            if (GameInput.Instance != null) GameInput.Instance.ResetVirtualInput();
        }

        // ------------------------------------------------------------------
        //  UI construction
        // ------------------------------------------------------------------

        private void Build()
        {
            _canvasObject = new GameObject("MobileControlsCanvas", typeof(RectTransform));
            _canvasObject.transform.SetParent(transform, false);

            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;                 // above the HUD (0), below the fade (32767)

            CanvasScaler scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasObject.AddComponent<GraphicRaycaster>();

            GameObject safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(_canvasObject.transform, false);
            _safeArea = (RectTransform)safe.transform;
            Stretch(_safeArea);
            safe.AddComponent<SafeAreaFitter>();

            // Build order is sibling order: the look area is added first so every button
            // is drawn on top of it and receives the pointer before it does.
            BuildLookArea();
            BuildJoystick();
            BuildJumpButton();
            BuildSprintButton();
            BuildPauseButton();

            _canvasObject.SetActive(false);
        }

        private void BuildLookArea()
        {
            GameObject go = new GameObject("LookArea", typeof(RectTransform));
            go.transform.SetParent(_safeArea, false);
            Stretch((RectTransform)go.transform);

            Image image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);   // invisible, but still clickable
            image.raycastTarget = true;

            TouchLookArea area = go.AddComponent<TouchLookArea>();
            area.Owner = this;
        }

        private void BuildJoystick()
        {
            RectTransform background = CreateCircle("Joystick", _safeArea, joystickDiameter,
                new Color(1f, 1f, 1f, 0.16f));
            background.anchorMin = new Vector2(0f, 0f);
            background.anchorMax = new Vector2(0f, 0f);
            background.anchoredPosition = new Vector2(60f + joystickDiameter * 0.5f, 60f + joystickDiameter * 0.5f);

            RectTransform knob = CreateCircle("Knob", background, joystickDiameter * 0.42f,
                new Color(1f, 1f, 1f, 0.55f));
            knob.GetComponent<Image>().raycastTarget = false;
            knob.anchoredPosition = Vector2.zero;

            TouchJoystick joystick = background.gameObject.AddComponent<TouchJoystick>();
            joystick.Owner = this;
            joystick.Background = background;
            joystick.Knob = knob;
        }

        private void BuildJumpButton()
        {
            RectTransform rect = CreateCircle("JumpButton", _safeArea, jumpButtonSize,
                new Color(0.16f, 0.62f, 0.42f, 0.85f));
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-(60f + jumpButtonSize * 0.5f), 60f + jumpButtonSize * 0.5f);

            AddLabel(rect, "SAUT", jumpButtonSize * 0.20f);

            TouchButton button = rect.gameObject.AddComponent<TouchButton>();
            button.OnDown = JumpPressed;
        }

        private void BuildSprintButton()
        {
            RectTransform rect = CreateCircle("SprintButton", _safeArea, sprintButtonSize,
                new Color(0.20f, 0.42f, 0.80f, 0.85f));
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(
                -(60f + jumpButtonSize * 0.5f),
                60f + jumpButtonSize + 24f + sprintButtonSize * 0.5f);

            AddLabel(rect, "COURIR", sprintButtonSize * 0.17f);

            TouchButton button = rect.gameObject.AddComponent<TouchButton>();
            button.OnDown = () => SetSprint(true);
            button.OnUp = () => SetSprint(false);
        }

        private void BuildPauseButton()
        {
            RectTransform rect = CreateCircle("PauseButton", _safeArea, pauseButtonSize,
                new Color(0.06f, 0.09f, 0.14f, 0.75f));
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(
                -(30f + pauseButtonSize * 0.5f),
                -(120f + pauseButtonSize * 0.5f));   // below the Level 2 countdown, top right

            AddLabel(rect, "II", pauseButtonSize * 0.34f);

            TouchButton button = rect.gameObject.AddComponent<TouchButton>();
            button.OnDown = PausePressed;
        }

        // ------------------------------------------------------------------
        //  Small UI helpers
        // ------------------------------------------------------------------

        private static RectTransform CreateCircle(string name, Transform parent, float size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.sprite = CircleSprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = true;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static void AddLabel(RectTransform parent, string content, float fontSize)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = content;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;               // the button itself must get the touch
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// A white circle sprite generated in code. No texture asset to import, no import
        /// settings to get wrong, and it matches the project's "everything is procedural" style.
        /// </summary>
        private static Sprite CircleSprite
        {
            get
            {
                if (_circleSprite != null) return _circleSprite;

                const int size = 128;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "MobileCircle",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                Color32[] pixels = new Color32[size * size];
                Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
                float radius = (size * 0.5f) - 1f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                        float alpha = Mathf.Clamp01(radius - distance);   // 1 px of anti-aliasing
                        pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                _circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                _circleSprite.name = "MobileCircle";
                return _circleSprite;
            }
        }
    }

    /// <summary>
    /// Creates the touch controls once, after the first scene has loaded, and keeps them alive
    /// across scene changes. Desktop builds are skipped entirely.
    /// </summary>
    public static class MobileControlsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (MobileControls.Instance != null) return;
            if (!Application.isMobilePlatform && !Application.isEditor && Touchscreen.current == null) return;

            GameObject go = new GameObject("MobileControls (auto)");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<MobileControls>();
        }
    }
}
