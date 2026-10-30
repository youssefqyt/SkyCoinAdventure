using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCoin
{
    /// <summary>
    /// The pause menu. Visible only while GameManager is in the Paused state.
    ///
    /// Buttons are wired with onClick.AddListener in code rather than through serialized
    /// "persistent" callbacks in the Inspector. That is deliberate: runtime listeners are
    /// always removed in OnDestroy, so repeated scene loads cannot build up duplicate
    /// callbacks that fire a button twice.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button mainMenuButton;

        [Tooltip("Options panel opened by the Options button. It closes back onto this menu.")]
        [SerializeField] private OptionsPanelController optionsPanel;

        private bool _subscribed;

        private void Awake()
        {
            RegisterButtons();

            if (panel != null) panel.SetActive(false);
        }

        private void Start()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.StateChanged += HandleStateChanged;
            _subscribed = true;

            HandleStateChanged(GameManager.Instance.State);
        }

        private void OnDestroy()
        {
            UnregisterButtons();

            if (_subscribed && GameManager.Instance != null)
            {
                GameManager.Instance.StateChanged -= HandleStateChanged;
                _subscribed = false;
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (panel == null) return;

            bool shouldShow = state == GameState.Paused;
            panel.SetActive(shouldShow);

            if (shouldShow) ApplyMobileHint();

            // Make sure a leftover options panel cannot block the pause menu.
            if (!shouldShow && optionsPanel != null) optionsPanel.Close();
        }

        /// <summary>
        /// The pause card is built with "Échap pour reprendre". A phone has no Escape key, so
        /// that line is replaced with one that matches the buttons actually on screen.
        /// </summary>
        private void ApplyMobileHint()
        {
            if (!Application.isMobilePlatform || panel == null) return;

            TMP_Text[] labels = panel.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text label in labels)
            {
                if (label.name != "Hint") continue;
                label.text = "Appuie sur Reprendre";
                return;
            }
        }

        private void RegisterButtons()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
            if (optionsButton != null) optionsButton.onClick.AddListener(OnOptionsClicked);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        private void UnregisterButtons()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
            if (optionsButton != null) optionsButton.onClick.RemoveListener(OnOptionsClicked);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
        }

        // ------------------------------------------------------------------
        //  Button handlers
        // ------------------------------------------------------------------

        private void OnResumeClicked()
        {
            PlayClick();
            if (GameManager.Instance != null) GameManager.Instance.Resume();
        }

        private void OnRestartClicked()
        {
            PlayClick();

            // Leave the paused state first so the scene never loads while frozen.
            if (GameManager.Instance != null) GameManager.Instance.Resume();

            if (LevelController.Instance != null)
            {
                LevelController.Instance.RestartLevel();
            }
        }

        private void OnOptionsClicked()
        {
            PlayClick();
            if (optionsPanel != null) optionsPanel.Open();
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
