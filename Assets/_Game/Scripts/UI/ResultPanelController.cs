using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCoin
{
    /// <summary>
    /// The victory / game over panel.
    ///
    /// It reacts to the GameManager events, so it can never show two different results:
    /// GameManager only ever raises one of them per run. It also cannot appear twice,
    /// because it hides itself on every state change before deciding to show.
    ///
    /// French labels, English identifiers - the same convention as the rest of the project.
    /// </summary>
    public class ResultPanelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text reasonText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Text")]
        [SerializeField] private string victoryTitle = "Victoire !";
        [SerializeField] private string gameOverTitle = "Game Over";
        [SerializeField] private string replayLabel = "Réessayer";
        [SerializeField] private string nextLevelLabel = "Niveau suivant";

        private bool _subscribed;

        private void Awake()
        {
            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            if (replayButton != null) replayButton.onClick.AddListener(OnReplayClicked);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);

            if (panel != null) panel.SetActive(false);
        }

        private void Start()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.VictoryRaised += HandleVictory;
            GameManager.Instance.GameOverRaised += HandleGameOver;
            GameManager.Instance.StateChanged += HandleStateChanged;
            _subscribed = true;

            HandleStateChanged(GameManager.Instance.State);
        }

        private void OnDestroy()
        {
            if (nextLevelButton != null) nextLevelButton.onClick.RemoveListener(OnNextLevelClicked);
            if (replayButton != null) replayButton.onClick.RemoveListener(OnReplayClicked);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);

            if (_subscribed && GameManager.Instance != null)
            {
                GameManager.Instance.VictoryRaised -= HandleVictory;
                GameManager.Instance.GameOverRaised -= HandleGameOver;
                GameManager.Instance.StateChanged -= HandleStateChanged;
                _subscribed = false;
            }
        }

        private void HandleStateChanged(GameState state)
        {
            // Hide on anything that is not a finished run. This is what makes the panel
            // impossible to leave on screen while a level is playing.
            if (state != GameState.Victory && state != GameState.GameOver)
            {
                if (panel != null) panel.SetActive(false);
            }
        }

        private void HandleVictory()
        {
            if (titleText != null) titleText.text = victoryTitle;
            if (reasonText != null) reasonText.text = "";

            ShowStats();

            LevelDefinition definition = GetDefinition();
            bool hasNext = definition != null && definition.HasNextLevel;

            if (nextLevelButton != null)
            {
                nextLevelButton.gameObject.SetActive(hasNext);
                TMP_Text label = nextLevelButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = nextLevelLabel;
            }

            if (replayButton != null)
            {
                TMP_Text label = replayButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = replayLabel;
            }

            if (panel != null) panel.SetActive(true);
        }

        private void HandleGameOver(GameOverReason reason)
        {
            if (titleText != null) titleText.text = gameOverTitle;
            if (reasonText != null) reasonText.text = DescribeReason(reason);

            ShowStats();

            // No "next level" after a defeat.
            if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(false);

            if (replayButton != null)
            {
                TMP_Text label = replayButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = replayLabel;
            }

            if (panel != null) panel.SetActive(true);
        }

        private void ShowStats()
        {
            if (statsText == null) return;

            LevelController level = LevelController.Instance;

            if (level == null)
            {
                statsText.text = "";
                return;
            }

            string coins = $"Pièces : {level.CollectedCoins} / {level.RequiredCoins}";
            string time = $"Temps : {LevelController.FormatTime(level.ElapsedTime)}";

            statsText.text = $"{coins}     {time}";
        }

        private static string DescribeReason(GameOverReason reason)
        {
            switch (reason)
            {
                case GameOverReason.Caught: return "Attrapé par l'ennemi !";
                case GameOverReason.Fallen: return "Tu es tombé dans le vide !";
                case GameOverReason.TimeExpired: return "Temps écoulé !";
                default: return "";
            }
        }

        private static LevelDefinition GetDefinition()
        {
            return LevelController.Instance != null ? LevelController.Instance.Definition : null;
        }

        // ------------------------------------------------------------------
        //  Buttons
        // ------------------------------------------------------------------

        private void OnNextLevelClicked()
        {
            PlayClick();
            if (LevelController.Instance != null) LevelController.Instance.GoToNextLevel();
        }

        private void OnReplayClicked()
        {
            PlayClick();
            if (LevelController.Instance != null) LevelController.Instance.RestartLevel();
        }

        private void OnMainMenuClicked()
        {
            PlayClick();
            if (LevelController.Instance != null) LevelController.Instance.GoToMainMenu();
        }

        private static void PlayClick()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayClick();
        }
    }
}
