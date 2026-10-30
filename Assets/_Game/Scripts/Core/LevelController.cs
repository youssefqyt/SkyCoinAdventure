using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCoin
{
    /// <summary>
    /// Owns everything that is specific to ONE level: the coins, the objective counter,
    /// the countdown, the fall limit, and the decision to win or lose.
    ///
    /// THE MOST IMPORTANT RULE IN THIS FILE:
    /// all loss conditions are evaluated in a fixed order and the victory check happens
    /// LAST, inside this single Update. The script runs at execution order 500 so its
    /// Update happens after every other gameplay script. That is what makes the outcome
    /// deterministic when two conditions collide in the same frame:
    ///
    ///     1. fallen below the death height   -> Game Over (tombé)
    ///     2. enemy contact reported this frame -> Game Over (attrapé)
    ///     3. countdown reached zero          -> Game Over (temps écoulé)
    ///     4. objective completed             -> Victoire
    ///
    /// So a defeat always wins a same frame tie, and there is exactly one place where the
    /// level can end. GameManager still has the final word and refuses a second result.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class LevelController : MonoBehaviour
    {
        public static LevelController Instance { get; private set; }

        [Header("Level configuration")]
        [SerializeField] private LevelDefinition definition;

        [Header("Scene references (auto resolved when left empty)")]
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerSpawnPoint playerSpawn;

        [Tooltip("Practice mode only: where a fall returns the player. Must be authored above real ground.")]
        [SerializeField] private Transform checkpoint;

        [Header("Diagnostics")]
        [SerializeField] private bool logEvents = true;

        /// <summary>(collected, required)</summary>
        public event Action<int, int> CoinCountChanged;

        /// <summary>Seconds remaining. Raised only when the whole second changes.</summary>
        public event Action<float> RemainingTimeChanged;

        public event Action LevelStarted;

        private Coin[] _coins;
        private ThirdPersonCamera _camera;

        private int _collectedCoins;
        private float _remainingTime;
        private float _elapsedTime;
        private int _lastRaisedSecond = -1;

        private bool _victoryPending;
        private bool _enemyCaptureReported;

        public LevelDefinition Definition => definition;
        public PlayerController Player => player;
        public int CollectedCoins => _collectedCoins;
        public int RequiredCoins => definition != null ? definition.requiredCoins : 0;
        public int CoinsInLevel => _coins != null ? _coins.Length : 0;
        public float RemainingTime => _remainingTime;
        public float ElapsedTime => _elapsedTime;
        public bool UsesTimer => definition != null && definition.useCountdownTimer;

        // ------------------------------------------------------------------
        //  Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[LevelController] Two level controllers in the scene ('{Instance.name}' and '{name}'). Disabling this one.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (definition == null)
            {
                Debug.LogError($"[LevelController] No LevelDefinition assigned. Assign one in the Inspector.", this);
                enabled = false;
                return;
            }

            _remainingTime = definition.timeLimitSeconds;

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (playerSpawn == null) playerSpawn = FindAnyObjectByType<PlayerSpawnPoint>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();

            // One scene-wide search, once, at level start. Never per frame, never per spawn.
            _coins = FindObjectsByType<Coin>(FindObjectsInactive.Exclude);

            ValidateSetup();
        }

        private void Start()
        {
            if (definition == null) return;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResetResultGuard();
                GameManager.Instance.SetState(GameState.Playing);
            }

            PlacePlayerAtSpawn();
            SnapCameraToPlayer();

            _collectedCoins = 0;
            _elapsedTime = 0f;
            _remainingTime = definition.timeLimitSeconds;
            _lastRaisedSecond = -1;
            _victoryPending = false;
            _enemyCaptureReported = false;

            if (logEvents)
            {
                string timerInfo = UsesTimer ? $"{definition.timeLimitSeconds:0}s timer" : "no timer";
                Debug.Log($"[LevelController] '{definition.displayName}' started. {CoinsInLevel} coins placed, {RequiredCoins} required, {timerInfo}.");
            }

            CoinCountChanged?.Invoke(_collectedCoins, RequiredCoins);
            if (UsesTimer) RemainingTimeChanged?.Invoke(_remainingTime);
            LevelStarted?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        //  The single evaluation path
        // ------------------------------------------------------------------

        private void Update()
        {
            if (definition == null) return;

            if (GameManager.Instance == null || !GameManager.Instance.IsGameplayRunning)
            {
                _enemyCaptureReported = false;
                return;
            }

            float deltaTime = Time.deltaTime;
            _elapsedTime += deltaTime;

            // --- 1. Fallen -------------------------------------------------
            if (player != null && player.transform.position.y < definition.deathHeight)
            {
                if (definition.practiceMode && checkpoint != null)
                {
                    ReturnToCheckpoint();
                }
                else
                {
                    EndRun(GameOverReason.Fallen);
                    return;
                }
            }

            // --- 2. Enemy contact (reported by EnemyChaser during its own Update) ---
            if (_enemyCaptureReported)
            {
                EndRun(GameOverReason.Caught);
                return;
            }

            // --- 3. Countdown ---------------------------------------------
            if (UsesTimer)
            {
                _remainingTime -= deltaTime;

                int wholeSecond = Mathf.CeilToInt(Mathf.Max(0f, _remainingTime));
                if (wholeSecond != _lastRaisedSecond)
                {
                    _lastRaisedSecond = wholeSecond;
                    RemainingTimeChanged?.Invoke(Mathf.Max(0f, _remainingTime));
                }

                if (_remainingTime <= 0f)
                {
                    EndRun(GameOverReason.TimeExpired);
                    return;
                }
            }

            // --- 4. Victory (deliberately last) ---------------------------
            if (_victoryPending)
            {
                _victoryPending = false;
                CommitVictory();
                return;
            }

            // A capture report is only valid for the frame it was made in.
            _enemyCaptureReported = false;
        }

        // ------------------------------------------------------------------
        //  Called by other systems
        // ------------------------------------------------------------------

        /// <summary>
        /// Called by a coin when the player touches it. Returns false when the coin was
        /// NOT consumed (the run has already ended), in which case the coin stays in the level.
        /// </summary>
        public bool TryCollectCoin(Coin coin)
        {
            if (coin == null || definition == null) return false;
            if (GameManager.Instance == null || !GameManager.Instance.IsGameplayRunning) return false;

            _collectedCoins++;
            CoinCountChanged?.Invoke(_collectedCoins, RequiredCoins);

            if (logEvents)
            {
                Debug.Log($"[LevelController] Pièces : {_collectedCoins} / {RequiredCoins}");
            }

            // Victory is only QUEUED here. It is committed at the end of Update so that a
            // loss condition in the same frame still wins.
            if (_collectedCoins >= RequiredCoins)
            {
                _victoryPending = true;
            }

            return true;
        }

        /// <summary>Called once per frame by an active EnemyChaser that is touching the player.</summary>
        public void ReportEnemyCapture()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsGameplayRunning) return;
            _enemyCaptureReported = true;
        }

        // ------------------------------------------------------------------
        //  Scene flow (used by the UI)
        // ------------------------------------------------------------------

        public void RestartLevel()
        {
            Scene active = SceneManager.GetActiveScene();
            LoadSceneSafely(active.name);
        }

        public void GoToNextLevel()
        {
            if (definition == null || !definition.HasNextLevel) return;
            LoadSceneSafely(definition.nextLevelScene);
        }

        public void GoToMainMenu()
        {
            LoadSceneSafely(GameScenes.MainMenu);
        }

        // ------------------------------------------------------------------
        //  Internals
        // ------------------------------------------------------------------

        private void ValidateSetup()
        {
            if (player == null)
            {
                Debug.LogError("[LevelController] No PlayerController found in the scene. Nothing can be played.", this);
            }

            if (playerSpawn == null)
            {
                Debug.LogWarning("[LevelController] No PlayerSpawnPoint found. The player will start wherever it sits in the scene.", this);
            }

            // With a CoinSpawner in the scene the coins are created and moved at runtime, so the
            // number sitting in the scene right now is not the objective budget at all.
            if (FindAnyObjectByType<CoinSpawner>() == null)
            {
                if (_coins.Length < definition.requiredCoins)
                {
                    Debug.LogError($"[LevelController] '{definition.displayName}' requires {definition.requiredCoins} coins but only {_coins.Length} are placed in the scene. The level cannot be completed.", this);
                }
                else if (_coins.Length == definition.requiredCoins)
                {
                    Debug.LogWarning($"[LevelController] '{definition.displayName}' has exactly {_coins.Length} coins for a {definition.requiredCoins} coin objective. One missed coin makes the level impossible. Add 2 spare coins.", this);
                }
            }

            if (definition.practiceMode && checkpoint == null)
            {
                Debug.LogWarning("[LevelController] practiceMode is ON but no checkpoint is assigned. Falling will do nothing.", this);
            }
        }

        private void PlacePlayerAtSpawn()
        {
            if (player == null || playerSpawn == null) return;

            player.Teleport(playerSpawn.SpawnPosition, playerSpawn.SpawnRotation);
        }

        private void SnapCameraToPlayer()
        {
            if (_camera == null || player == null) return;
            _camera.SetTarget(player.transform, true);

            if (SaveService.Instance != null)
            {
                _camera.SetLookSensitivity(SaveService.Instance.MouseSensitivity);
            }
        }

        private void ReturnToCheckpoint()
        {
            if (player == null || checkpoint == null) return;

            player.Teleport(checkpoint.position, checkpoint.rotation);

            if (_camera != null) _camera.SnapToTarget();
        }

        private void CommitVictory()
        {
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.RequestVictory())
            {
                if (SaveService.Instance != null && definition != null)
                {
                    SaveService.Instance.CompleteLevel(definition.levelNumber, definition.levelNumber + 1);
                }

                if (AudioManager.Instance != null) AudioManager.Instance.PlayVictory();

                if (logEvents)
                {
                    Debug.Log($"[LevelController] Victoire ! {definition.displayName} terminé en {FormatTime(_elapsedTime)}.");
                }
            }
        }

        private void EndRun(GameOverReason reason)
        {
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.RequestGameOver(reason))
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDefeat();
            }
        }

        private void LoadSceneSafely(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[LevelController] Tried to load an empty scene name.", this);
                return;
            }

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(sceneName);
                return;
            }

            // Fallback, only reachable if the automatic manager layer is missing.
            if (GameManager.Instance != null) GameManager.Instance.ResetForNewScene();

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogError($"[LevelController] Scene '{sceneName}' is not in the build list. Add it in File > Build Profiles > Scene List.", this);
            }
        }

        /// <summary>mm:ss, used by the HUD and the victory panel.</summary>
        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int totalSeconds = Mathf.FloorToInt(seconds);
            int minutes = totalSeconds / 60;
            int remainder = totalSeconds % 60;
            return $"{minutes:00}:{remainder:00}";
        }
    }
}
