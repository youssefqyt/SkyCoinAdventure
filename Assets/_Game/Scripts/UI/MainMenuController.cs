using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCoin
{
    /// <summary>
    /// The main menu.
    ///
    /// "Continuer" is disabled until a valid save exists, and it always loads the
    /// documented destination: the next playable level. It never claims to resume the
    /// middle of a run.
    ///
    /// "Nouvelle partie" asks for confirmation before it erases progress.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Main buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button levelSelectButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text continueLabel;

        [Header("Panels")]
        [SerializeField] private GameObject levelSelectPanel;
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private OptionsPanelController optionsPanel;

        [Header("Level select")]
        [SerializeField] private Button level1Button;
        [SerializeField] private Button level2Button;
        [SerializeField] private Button levelSelectCloseButton;

        [Header("Confirm new game")]
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        private void Awake()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
            if (levelSelectButton != null) levelSelectButton.onClick.AddListener(OnLevelSelectClicked);
            if (optionsButton != null) optionsButton.onClick.AddListener(OnOptionsClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

            if (level1Button != null) level1Button.onClick.AddListener(() => StartLevel(1));
            if (level2Button != null) level2Button.onClick.AddListener(() => StartLevel(2));
            if (levelSelectCloseButton != null) levelSelectCloseButton.onClick.AddListener(OnLevelSelectCloseClicked);

            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYesClicked);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNoClicked);

            if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (newGameButton != null) newGameButton.onClick.RemoveAllListeners();
            if (continueButton != null) continueButton.onClick.RemoveAllListeners();
            if (levelSelectButton != null) levelSelectButton.onClick.RemoveAllListeners();
            if (optionsButton != null) optionsButton.onClick.RemoveAllListeners();
            if (quitButton != null) quitButton.onClick.RemoveAllListeners();

            if (level1Button != null) level1Button.onClick.RemoveAllListeners();
            if (level2Button != null) level2Button.onClick.RemoveAllListeners();
            if (levelSelectCloseButton != null) levelSelectCloseButton.onClick.RemoveAllListeners();

            if (confirmYesButton != null) confirmYesButton.onClick.RemoveAllListeners();
            if (confirmNoButton != null) confirmNoButton.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            // The main menu is the one place that puts the game in the MainMenu state:
            // cursor visible and unlocked, timeScale 1.
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.MainMenu);

            RefreshSaveDependentUI();
        }

        private void RefreshSaveDependentUI()
        {
            SaveService save = SaveService.Instance;
            bool hasProgress = save != null && save.HasProgress;

            if (continueButton != null) continueButton.interactable = hasProgress;

            if (continueLabel != null)
            {
                if (hasProgress && save != null)
                {
                    continueLabel.text = $"Continuer  (Niveau {save.ContinueLevelNumber})";
                }
                else
                {
                    continueLabel.text = "Continuer  (aucune sauvegarde)";
                }
            }

            if (level2Button != null)
            {
                bool level2Unlocked = save != null && save.IsLevelUnlocked(2);
                level2Button.interactable = level2Unlocked;

                TMP_Text label = level2Button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = level2Unlocked ? "Niveau 2" : "Niveau 2  (verrouillé)";
                }
            }
        }

        // ------------------------------------------------------------------
        //  Button handlers
        // ------------------------------------------------------------------

        private void OnNewGameClicked()
        {
            PlayClick();

            SaveService save = SaveService.Instance;

            // Never destroy progress without asking.
            if (save != null && save.HasProgress)
            {
                if (confirmPanel != null) confirmPanel.SetActive(true);
                return;
            }

            StartLevel(1);
        }

        private void OnContinueClicked()
        {
            PlayClick();

            SaveService save = SaveService.Instance;
            if (save == null || !save.HasProgress) return;

            StartLevel(save.ContinueLevelNumber);
        }

        private void OnLevelSelectClicked()
        {
            PlayClick();
            RefreshSaveDependentUI();
            if (levelSelectPanel != null) levelSelectPanel.SetActive(true);
        }

        private void OnLevelSelectCloseClicked()
        {
            PlayClick();
            if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        }

        private void OnOptionsClicked()
        {
            PlayClick();
            if (optionsPanel != null) optionsPanel.Open();
        }

        private void OnConfirmYesClicked()
        {
            PlayClick();

            if (SaveService.Instance != null) SaveService.Instance.ResetProgress();

            if (confirmPanel != null) confirmPanel.SetActive(false);
            RefreshSaveDependentUI();

            StartLevel(1);
        }

        private void OnConfirmNoClicked()
        {
            PlayClick();
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }

        private void OnQuitClicked()
        {
            PlayClick();

#if UNITY_EDITOR
            Debug.Log("[MainMenu] Quitter pressed. Application.Quit() does nothing in the Editor; use the Play button to stop.");
#else
            Application.Quit();
#endif
        }

        private void StartLevel(int levelNumber)
        {
            string sceneName = GameScenes.ForLevelNumber(levelNumber);

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(sceneName);
                return;
            }

            Debug.LogError("[MainMenu] SceneLoader is missing. The scene cannot be loaded.");
        }

        private static void PlayClick()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayClick();
        }
    }
}
