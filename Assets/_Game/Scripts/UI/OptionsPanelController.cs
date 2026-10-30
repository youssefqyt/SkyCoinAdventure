using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCoin
{
    /// <summary>
    /// The Options panel, shared by the main menu and the pause menu.
    ///
    /// It owns no state of its own: every value is read from and written to
    /// <see cref="SaveService"/>, which persists them with PlayerPrefs. That is why the
    /// settings survive between sessions and are identical no matter which panel opened it.
    ///
    /// The panel has a full screen backdrop image that blocks clicks, so the screen behind
    /// it cannot be interacted with while it is open (a classic overlapping-UI bug).
    /// </summary>
    public class OptionsPanelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private TMP_Text masterValueText;
        [SerializeField] private TMP_Text musicValueText;
        [SerializeField] private TMP_Text sfxValueText;
        [SerializeField] private TMP_Text sensitivityValueText;
        [SerializeField] private Button closeButton;

        [Header("Sensitivity range (degrees of rotation per pixel of mouse movement)")]
        [SerializeField] private float minSensitivity = 0.04f;
        [SerializeField] private float maxSensitivity = 0.4f;

        private void Awake()
        {
            if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);

            if (panel != null) panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            if (sensitivitySlider != null) sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
            if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        public void Open()
        {
            RefreshSlidersFromSave();

            if (panel == null) return;

            // uGUI draws siblings in hierarchy order: the last child is drawn on top.
            // The pause menu builds this panel BEFORE its own menu (so it can reference it),
            // which used to leave the options screen rendered *behind* the pause card and
            // therefore unclickable. Moving ourselves to the end of the canvas guarantees we
            // are on top no matter what order the scenes were built in.
            panel.transform.SetAsLastSibling();

            panel.SetActive(true);
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        public bool IsOpen => panel != null && panel.activeSelf;

        private void RefreshSlidersFromSave()
        {
            SaveService save = SaveService.Instance;
            if (save == null) return;

            // SetValueWithoutNotify avoids firing the change callbacks while we are
            // populating the panel, which would re-save settings every time it opens.
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(save.MasterVolume);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(save.MusicVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(save.SfxVolume);

            if (sensitivitySlider != null)
            {
                float normalised = Mathf.InverseLerp(minSensitivity, maxSensitivity, save.MouseSensitivity);
                sensitivitySlider.SetValueWithoutNotify(normalised);
            }

            UpdateValueLabels();
        }

        private void UpdateValueLabels()
        {
            SaveService save = SaveService.Instance;
            if (save == null) return;

            if (masterValueText != null) masterValueText.text = $"{Mathf.RoundToInt(save.MasterVolume * 100f)} %";
            if (musicValueText != null) musicValueText.text = $"{Mathf.RoundToInt(save.MusicVolume * 100f)} %";
            if (sfxValueText != null) sfxValueText.text = $"{Mathf.RoundToInt(save.SfxVolume * 100f)} %";

            if (sensitivityValueText != null)
            {
                sensitivityValueText.text = save.MouseSensitivity.ToString("0.00");
            }
        }

        private void PushVolumesToSave()
        {
            SaveService save = SaveService.Instance;
            if (save == null) return;

            float master = masterSlider != null ? masterSlider.value : save.MasterVolume;
            float music = musicSlider != null ? musicSlider.value : save.MusicVolume;
            float sfx = sfxSlider != null ? sfxSlider.value : save.SfxVolume;

            save.SetVolumes(master, music, sfx);
            UpdateValueLabels();
        }

        private void OnMasterChanged(float value) => PushVolumesToSave();
        private void OnMusicChanged(float value) => PushVolumesToSave();
        private void OnSfxChanged(float value) => PushVolumesToSave();

        private void OnSensitivityChanged(float normalised)
        {
            SaveService save = SaveService.Instance;
            if (save == null) return;

            float value = Mathf.Lerp(minSensitivity, maxSensitivity, normalised);
            save.SetMouseSensitivity(value);

            // Apply to the camera that is active right now, if any.
            ThirdPersonCamera camera = FindAnyObjectByType<ThirdPersonCamera>();
            if (camera != null) camera.SetLookSensitivity(value);

            UpdateValueLabels();
        }

        private void OnCloseClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayClick();
            Close();
        }
    }
}
