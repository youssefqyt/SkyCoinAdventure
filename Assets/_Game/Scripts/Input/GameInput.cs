using UnityEngine;
using UnityEngine.InputSystem;

namespace SkyCoin
{
    /// <summary>
    /// The one place where gameplay input is defined and read.
    ///
    /// WHY THIS FILE EXISTS (and why there is no .inputactions asset):
    /// This project uses the Input System package with "Active Input Handling = Input System Package".
    /// Instead of shipping a large generated .inputactions asset, the action map is built in C#
    /// with explicit, documented names and binding paths. That means:
    ///   - no GUID or asset-version mismatch can break the game,
    ///   - the full binding list is readable in one file,
    ///   - it works identically in the Editor and in a build.
    ///
    /// ACTION MAP: "Gameplay"
    ///   Move   (Value,  Vector2) : WASD composite, Arrow composite, Gamepad left stick
    ///   Look   (Value,  Vector2) : Mouse delta, Gamepad right stick
    ///   Zoom   (Value,  float)   : Mouse scroll wheel Y, Gamepad dpad up/down
    ///   Jump   (Button)          : Space, Gamepad button south
    ///   Sprint (Button)          : Left/Right Shift, Gamepad left stick press
    ///   Pause  (Button)          : Escape, Gamepad start
    ///
    /// AZERTY KEYBOARDS
    ///   The whole design is camera relative, so the physical WASD cluster is what matters.
    ///   The arrow-key composite is always available as a layout independent fallback.
    ///   If WASD does not feel right on your keyboard, change only the binding strings
    ///   in BuildActions() below - there is no other place to touch.
    ///
    /// UI input (buttons, pointer) is handled separately by the EventSystem using
    /// InputSystemUIInputModule and the UI action map of Assets/InputSystem_Actions.inputactions.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public class GameInput : MonoBehaviour
    {
        public static GameInput Instance { get; private set; }

        [Header("Lifetime")]
        [Tooltip("Keep this object alive across scene loads. Turn this ON in Milestone 3.")]
        [SerializeField] private bool persistAcrossScenes = false;

        [Header("Debug")]
        [Tooltip("Log the raw values when a key is pressed. Useful while wiring Milestone 1.")]
        [SerializeField] private bool logKeys = false;

        private InputActionMap _gameplayMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _zoomAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _pauseAction;

        private bool _built;

        // ------------------------------------------------------------------
        //  Public read-only view for gameplay scripts
        // ------------------------------------------------------------------

        /// <summary>Camera frame independent move input, already normalised to at most length 1.</summary>
        public Vector2 Move
        {
            get
            {
                // A touch joystick takes priority. If it is idle, keyboard / gamepad are used,
                // so the same build still plays with WASD in the Editor.
                if (_virtualMove.sqrMagnitude > 0.0001f) return _virtualMove;

                if (_moveAction == null) return Vector2.zero;
                Vector2 value = _moveAction.ReadValue<Vector2>();
                return value.sqrMagnitude > 1f ? value.normalized : value;
            }
        }

        /// <summary>Look delta for this frame (mouse, right stick, or a finger drag).</summary>
        public Vector2 Look
        {
            get
            {
                Vector2 look = Vector2.zero;

                // While a finger drags the touch look area, the mouse delta is ignored so a
                // desktop test with the mouse cannot move the camera twice.
                if (!SuppressMouseLook && _lookAction != null)
                {
                    look = _lookAction.ReadValue<Vector2>();
                }

                if (_virtualLookFrame == Time.frameCount) look += _virtualLook;
                return look;
            }
        }

        /// <summary>Scroll wheel / pinch value: positive means "zoom in".</summary>
        public float Zoom
        {
            get
            {
                float zoom = _zoomAction != null ? _zoomAction.ReadValue<float>() : 0f;
                if (_virtualZoomFrame == Time.frameCount) zoom += _virtualZoom;
                return zoom;
            }
        }

        public bool SprintHeld => (_sprintAction != null && _sprintAction.IsPressed()) || _virtualSprint;
        public bool JumpPressedThisFrame => (_jumpAction != null && _jumpAction.WasPressedThisFrame()) || _jumpPending;
        public bool PausePressedThisFrame => _pauseAction != null && _pauseAction.WasPressedThisFrame();

        // ------------------------------------------------------------------
        //  Virtual input (written by the on-screen touch controls)
        // ------------------------------------------------------------------

        private Vector2 _virtualMove;
        private Vector2 _virtualLook;
        private int _virtualLookFrame = -1;
        private float _virtualZoom;
        private int _virtualZoomFrame = -1;
        private bool _virtualSprint;
        private bool _jumpPending;

        /// <summary>True while the on-screen touch controls are driving the game.</summary>
        public bool UsingTouchControls { get; set; }

        /// <summary>Set while a finger drags the look area, so the mouse delta is ignored.</summary>
        public bool SuppressMouseLook { get; set; }

        /// <summary>Joystick value, already normalised to at most length 1.</summary>
        public void SetVirtualMove(Vector2 value) => _virtualMove = Vector2.ClampMagnitude(value, 1f);

        /// <summary>Sprint button held / released.</summary>
        public void SetVirtualSprint(bool held) => _virtualSprint = held;

        /// <summary>
        /// Adds one frame of finger-drag delta. Frame stamped on purpose: the camera reads it
        /// in LateUpdate of the same frame, and it disappears on its own on the next frame.
        /// </summary>
        public void AddVirtualLook(Vector2 delta)
        {
            if (_virtualLookFrame != Time.frameCount)
            {
                _virtualLook = Vector2.zero;
                _virtualLookFrame = Time.frameCount;
            }

            _virtualLook += delta;
        }

        /// <summary>Adds one frame of pinch zoom. Frame stamped, same reason as the look delta.</summary>
        public void AddVirtualZoom(float delta)
        {
            if (_virtualZoomFrame != Time.frameCount)
            {
                _virtualZoom = 0f;
                _virtualZoomFrame = Time.frameCount;
            }

            _virtualZoom += delta;
        }

        /// <summary>Jump button pressed. Read by PlayerController through ConsumeJumpPressedThisFrame.</summary>
        public void QueueJump() => _jumpPending = true;

        /// <summary>
        /// True once per press, for BOTH the keyboard/gamepad action and the touch button.
        /// Consuming (instead of peeking) is what stops one tap from jumping every frame.
        /// </summary>
        public bool ConsumeJumpPressedThisFrame()
        {
            bool pressed = (_jumpAction != null && _jumpAction.WasPressedThisFrame()) || _jumpPending;
            _jumpPending = false;
            return pressed;
        }

        /// <summary>Clears every virtual value. Called when the touch controls are hidden.</summary>
        public void ResetVirtualInput()
        {
            _virtualMove = Vector2.zero;
            _virtualSprint = false;
            _jumpPending = false;
            _virtualLook = Vector2.zero;
            _virtualZoom = 0f;
            SuppressMouseLook = false;
        }

        // ------------------------------------------------------------------
        //  Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameInput] A second GameInput was found on '{name}'. Destroying the duplicate.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildActions();

            if (persistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            if (_gameplayMap != null) _gameplayMap.Enable();
        }

        private void OnDisable()
        {
            if (_gameplayMap != null) _gameplayMap.Disable();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!logKeys) return;

            if (JumpPressedThisFrame) Debug.Log("[GameInput] Jump pressed.");
            if (PausePressedThisFrame) Debug.Log("[GameInput] Pause pressed.");
            if (SprintHeld) Debug.Log("[GameInput] Sprint held.");
        }

        /// <summary>
        /// Lets a full screen menu stop gameplay input without destroying the object.
        /// (Menus themselves are driven by the EventSystem, not by this map.)
        /// </summary>
        public void SetGameplayInputEnabled(bool enabled)
        {
            if (_gameplayMap == null) return;

            if (enabled) _gameplayMap.Enable();
            else _gameplayMap.Disable();
        }

        // ------------------------------------------------------------------
        //  Action construction
        // ------------------------------------------------------------------

        private void BuildActions()
        {
            if (_built) return;
            _built = true;

            _gameplayMap = new InputActionMap("Gameplay");

            // --- Move -----------------------------------------------------
            _moveAction = _gameplayMap.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");

            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            // Arrow keys: always available, and layout independent on AZERTY keyboards.
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            _moveAction.AddBinding("<Gamepad>/leftStick");

            // --- Look -----------------------------------------------------
            _lookAction = _gameplayMap.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            _lookAction.AddBinding("<Mouse>/delta");
            _lookAction.AddBinding("<Gamepad>/rightStick");

            // --- Zoom -----------------------------------------------------
            _zoomAction = _gameplayMap.AddAction("Zoom", InputActionType.Value, expectedControlLayout: "Axis");
            _zoomAction.AddBinding("<Mouse>/scroll/y");
            _zoomAction.AddCompositeBinding("1DAxis")
                .With("Positive", "<Gamepad>/dpad/up")
                .With("Negative", "<Gamepad>/dpad/down");

            // --- Jump -----------------------------------------------------
            _jumpAction = _gameplayMap.AddAction("Jump", InputActionType.Button);
            _jumpAction.AddBinding("<Keyboard>/space");
            _jumpAction.AddBinding("<Gamepad>/buttonSouth");

            // --- Sprint ---------------------------------------------------
            _sprintAction = _gameplayMap.AddAction("Sprint", InputActionType.Button);
            _sprintAction.AddBinding("<Keyboard>/leftShift");
            _sprintAction.AddBinding("<Keyboard>/rightShift");
            _sprintAction.AddBinding("<Gamepad>/leftStickPress");

            // --- Pause ----------------------------------------------------
            _pauseAction = _gameplayMap.AddAction("Pause", InputActionType.Button);
            _pauseAction.AddBinding("<Keyboard>/escape");
            _pauseAction.AddBinding("<Gamepad>/start");
        }
    }
}
