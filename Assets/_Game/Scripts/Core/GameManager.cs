using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCoin
{
    /// <summary>
    /// The single authority for the game state.
    ///
    /// Rules enforced here (read these before adding new systems):
    /// 1. Only GameManager sets Time.timeScale and the mouse cursor state.
    /// 2. A level run can only produce ONE final result. The first call to
    ///    RequestVictory / RequestGameOver wins and every later call in the same run
    ///    is ignored. This is the "one final result" guarantee.
    /// 3. Determinism when two conditions happen in the same frame: LevelController
    ///    evaluates ALL loss conditions before the victory condition, so a defeat
    ///    always wins a same-frame tie. GameManager additionally ignores any result
    ///    request that arrives while the game is not in the Playing state.
    ///
    /// Time.timeScale by state:
    ///   Boot / MainMenu / Playing -> 1
    ///   Paused / Victory / GameOver -> 0
    /// Anything that must keep animating while frozen (menu animations, UI tweens)
    /// has to use unscaled time (Time.unscaledDeltaTime).
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class GameManager : MonoBehaviour
    {
        /// <summary>Current instance. Null before the first GameManager wakes up.</summary>
        public static GameManager Instance { get; private set; }

        [Header("Startup")]
        [Tooltip("State the game enters when this object starts. Level scenes should use Playing.")]
        [SerializeField] private GameState initialState = GameState.Playing;

        [Header("Lifetime")]
        [Tooltip("Keep this object alive across scene loads. Turn this ON in Milestone 3 when the Bootstrap scene is added.")]
        [SerializeField] private bool persistAcrossScenes = false;

        [Header("Diagnostics")]
        [Tooltip("Log every state change together with the resulting Time.timeScale.")]
        [SerializeField] private bool logStateChanges = true;

        private GameState _state = GameState.Boot;
        private GameOverReason _lastGameOverReason = GameOverReason.None;

        // --- The "one final result" guard ---------------------------------
        private bool _resultDecided;
        private int _resultFrame = -1;

        public GameState State => _state;
        public GameOverReason LastGameOverReason => _lastGameOverReason;

        public bool IsPlaying => _state == GameState.Playing;
        public bool IsPaused => _state == GameState.Paused;
        public bool IsVictory => _state == GameState.Victory;
        public bool IsGameOver => _state == GameState.GameOver;

        /// <summary>
        /// True only while a level is actually running. Every gameplay system
        /// (player, enemy, hazards, timer, coin spawner, pickups) must check this.
        /// </summary>
        public bool IsGameplayRunning => _state == GameState.Playing;

        /// <summary>True once a run has produced its final result.</summary>
        public bool HasResult => _resultDecided;

        /// <summary>Raised after the state changed. UI panels subscribe to this.</summary>
        public event Action<GameState> StateChanged;

        /// <summary>Raised exactly once per run when the objective is completed.</summary>
        public event Action VictoryRaised;

        /// <summary>Raised exactly once per run when a loss condition happens.</summary>
        public event Action<GameOverReason> GameOverRaised;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameManager] A second GameManager was found on '{name}'. Destroying the duplicate.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes)
            {
                transform.SetParent(null);        // a persistent object cannot be a child of a scene object
                DontDestroyOnLoad(gameObject);
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                Instance = null;
            }
        }

        private void Start()
        {
            ApplyState(initialState, force: true);
        }

        private void Update()
        {
            // Escape toggles pause, but only while a level is actually running.
            if (GameInput.Instance != null && GameInput.Instance.PausePressedThisFrame)
            {
                TogglePause();
            }
        }

        // ------------------------------------------------------------------
        //  Pause
        // ------------------------------------------------------------------

        public void TogglePause()
        {
            if (IsPlaying)
            {
                Pause();
            }
            else if (IsPaused)
            {
                Resume();
            }
        }

        public void Pause()
        {
            if (!IsPlaying) return;
            ApplyState(GameState.Paused);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            ApplyState(GameState.Playing);
        }

        // ------------------------------------------------------------------
        //  Results (victory / defeat)
        // ------------------------------------------------------------------

        /// <summary>
        /// Ends the run as a victory. Returns false when the request was ignored
        /// (because the game is not Playing, or a result already exists).
        /// </summary>
        public bool RequestVictory()
        {
            if (!IsPlaying) return false;
            if (_resultDecided)
            {
                if (_resultFrame == Time.frameCount)
                {
                    Debug.Log("[GameManager] Ignored a same-frame duplicate result request (victory). The first result already won.");
                }
                return false;
            }

            _resultDecided = true;
            _resultFrame = Time.frameCount;
            ApplyState(GameState.Victory);

            if (logStateChanges) Debug.Log("[GameManager] VICTORY.");
            VictoryRaised?.Invoke();
            return true;
        }

        /// <summary>
        /// Ends the run as a loss. Returns false when the request was ignored.
        /// </summary>
        public bool RequestGameOver(GameOverReason reason)
        {
            if (!IsPlaying) return false;
            if (_resultDecided)
            {
                if (_resultFrame == Time.frameCount)
                {
                    Debug.Log($"[GameManager] Ignored a same-frame duplicate result request ({reason}). The first result already won.");
                }
                return false;
            }

            _resultDecided = true;
            _resultFrame = Time.frameCount;
            _lastGameOverReason = reason;
            ApplyState(GameState.GameOver);

            if (logStateChanges) Debug.Log($"[GameManager] GAME OVER ({reason}).");
            GameOverRaised?.Invoke(reason);
            return true;
        }

        /// <summary>
        /// Called by LevelController when a level starts a fresh run.
        /// Without this the run would start already "decided" after a retry.
        /// </summary>
        public void ResetResultGuard()
        {
            _resultDecided = false;
            _resultFrame = -1;
            _lastGameOverReason = GameOverReason.None;
        }

        /// <summary>
        /// Must be called before loading another scene. Guarantees that a scene can
        /// never start frozen because a previous scene left Time.timeScale at zero.
        /// </summary>
        public void ResetForNewScene()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ResetResultGuard();
        }

        /// <summary>Public setter used by menus that switch straight to a known state.</summary>
        public void SetState(GameState next)
        {
            ApplyState(next, force: false);
        }

        // ------------------------------------------------------------------
        //  Internals
        // ------------------------------------------------------------------

        private void ApplyState(GameState next, bool force = false)
        {
            if (!force && next == _state) return;

            _state = next;

            switch (next)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    SetCursorLocked(true);
                    break;

                case GameState.Boot:
                case GameState.MainMenu:
                    Time.timeScale = 1f;
                    SetCursorLocked(false);
                    break;

                case GameState.Paused:
                case GameState.Victory:
                case GameState.GameOver:
                    Time.timeScale = 0f;
                    SetCursorLocked(false);
                    break;
            }

            if (logStateChanges)
            {
                Debug.Log($"[GameManager] State -> {next} (Time.timeScale = {Time.timeScale:0.##}).");
            }

            StateChanged?.Invoke(next);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Defensive: only unfreeze if this object believes gameplay should be running.
            // Paused / Victory / GameOver intentionally keep timeScale at 0.
            if (_state == GameState.Boot || _state == GameState.MainMenu || _state == GameState.Playing)
            {
                Time.timeScale = 1f;
            }
        }
    }
}
