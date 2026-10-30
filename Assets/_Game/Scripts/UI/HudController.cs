using TMPro;
using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// The in-game HUD: coin counter, level name, objective line and the Level 2 countdown.
    ///
    /// It PULLS its initial values and then LISTENS. It never searches the scene per frame
    /// and it always unsubscribes, so entering and leaving levels repeatedly cannot leak.
    /// Text is only rebuilt when a displayed value actually changed, which keeps the
    /// garbage collector quiet during play.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class HudController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LevelController level;
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text coinText;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text timerText;

        [Tooltip("Group containing the countdown. Hidden on levels without a timer.")]
        [SerializeField] private GameObject timerGroup;

        [Tooltip("Controls panel shown at the start of Level 1.")]
        [SerializeField] private GameObject tutorialPanel;

        [Header("Text")]
        [SerializeField] private string coinFormat = "Pièces : {0} / {1}";

        [Header("Tutorial panel")]
        [Tooltip("Seconds before the controls panel hides itself.")]
        [SerializeField] private float tutorialAutoHideSeconds = 12f;

        [Tooltip("Hide the controls panel as soon as the first coin is collected.")]
        [SerializeField] private bool hideTutorialOnFirstCoin = true;

        private float _tutorialTimer;
        private bool _tutorialVisible;

        private int _lastCoins = -1;
        private int _lastRequired = -1;
        private int _lastWholeSecond = int.MinValue;
        private bool _subscribed;

        private void Start()
        {
            if (level == null) level = LevelController.Instance;

            if (level == null)
            {
                Debug.LogError("[HudController] No LevelController found in the scene. The HUD will stay empty.", this);
                return;
            }

            level.CoinCountChanged += HandleCoinCountChanged;
            level.RemainingTimeChanged += HandleRemainingTimeChanged;
            level.LevelStarted += HandleLevelStarted;
            _subscribed = true;

            HandleLevelStarted();

            // Pull the current values so the HUD is correct even if it started later
            // than the level did.
            HandleCoinCountChanged(level.CollectedCoins, level.RequiredCoins);
            HandleRemainingTimeChanged(level.RemainingTime);
        }

        private void OnDestroy()
        {
            if (!_subscribed || level == null) return;

            level.CoinCountChanged -= HandleCoinCountChanged;
            level.RemainingTimeChanged -= HandleRemainingTimeChanged;
            level.LevelStarted -= HandleLevelStarted;
            _subscribed = false;
        }

        private void Update()
        {
            if (!_tutorialVisible || tutorialPanel == null) return;

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning) return;

            _tutorialTimer -= Time.deltaTime;

            if (_tutorialTimer <= 0f) HideTutorial();
        }

        // ------------------------------------------------------------------
        //  Event handlers
        // ------------------------------------------------------------------

        private void HandleLevelStarted()
        {
            if (level == null) return;
            if (level.Definition == null) return;

            if (hudRoot != null) hudRoot.SetActive(true);

            if (levelText != null) levelText.text = level.Definition.displayName;
            if (objectiveText != null) objectiveText.text = level.Definition.ObjectiveText;

            if (timerGroup != null) timerGroup.SetActive(level.UsesTimer);

            if (tutorialPanel != null)
            {
                _tutorialVisible = true;
                _tutorialTimer = tutorialAutoHideSeconds;
                ApplyTutorialTextForPlatform();
                tutorialPanel.SetActive(true);
            }
        }

        /// <summary>
        /// The tutorial card is written by the builder with keyboard controls. On a phone those
        /// lines are wrong, so only the first block (the controls) is replaced and the level
        /// objective below it is kept untouched.
        /// </summary>
        private void ApplyTutorialTextForPlatform()
        {
            if (!Application.isMobilePlatform || tutorialPanel == null) return;

            TMP_Text text = tutorialPanel.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return;

            string[] blocks = text.text.Split(new[] { "\n\n" }, System.StringSplitOptions.None);

            string controls = "Joystick : se déplacer        Glisse l'écran : regarder\n" +
                              "Boutons : sauter / courir        II : pause";

            text.text = blocks.Length > 1
                ? controls + "\n\n" + blocks[blocks.Length - 1]
                : controls;
        }

        private void HandleCoinCountChanged(int collected, int required)
        {
            if (coinText == null) return;
            if (collected == _lastCoins && required == _lastRequired) return;

            _lastCoins = collected;
            _lastRequired = required;

            coinText.text = string.Format(coinFormat, collected, required);

            if (hideTutorialOnFirstCoin && collected > 0) HideTutorial();
        }

        private void HandleRemainingTimeChanged(float remaining)
        {
            if (timerText == null) return;

            int wholeSecond = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            if (wholeSecond == _lastWholeSecond) return;

            _lastWholeSecond = wholeSecond;
            timerText.text = $"Temps : {LevelController.FormatTime(remaining)}";

            // Turn the countdown red in the last 20 seconds: a readable, no-animation warning.
            timerText.color = remaining <= 20f ? new Color(1f, 0.35f, 0.3f) : Color.white;
        }

        public void HideTutorial()
        {
            _tutorialVisible = false;
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
        }
    }
}
