using System;
using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// Persisted progress and settings, stored with PlayerPrefs.
    ///
    /// WHAT IS SAVED (and nothing else):
    ///   - whether any real progress exists,
    ///   - the highest unlocked level number,
    ///   - the level to continue with,
    ///   - master / music / SFX volume,
    ///   - mouse sensitivity.
    ///
    /// WHAT IS NOT SAVED: the exact middle of a run (position, coins collected, timer
    /// remaining). "Continuer" therefore loads the start of the next playable level, and
    /// the UI is worded to say exactly that. Treating it as a mid-run resume would be a
    /// promise this project cannot keep.
    ///
    /// During development, reset it with:
    ///   Sky Coin Adventure > Setup > 5. Reset Saved Progress
    /// or PlayerPrefs.DeleteAll() from any script.
    /// </summary>
    [DefaultExecutionOrder(-450)]
    public class SaveService : MonoBehaviour
    {
        public static SaveService Instance { get; private set; }

        private const string KeyHasProgress = "SkyCoin.HasProgress";
        private const string KeyUnlockedLevel = "SkyCoin.UnlockedLevel";
        private const string KeyLastPlayedLevel = "SkyCoin.LastPlayedLevel";
        private const string KeyMasterVolume = "SkyCoin.Volume.Master";
        private const string KeyMusicVolume = "SkyCoin.Volume.Music";
        private const string KeySfxVolume = "SkyCoin.Volume.Sfx";
        private const string KeyMouseSensitivity = "SkyCoin.MouseSensitivity";

        /// <summary>Raised whenever a setting changes so audio / camera can react immediately.</summary>
        public event Action SettingsChanged;

        public bool HasProgress { get; private set; }
        public int UnlockedLevel { get; private set; } = 1;
        public int LastPlayedLevel { get; private set; } = 1;

        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 0.6f;
        public float SfxVolume { get; private set; } = 1f;
        public float MouseSensitivity { get; private set; } = 0.14f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        //  Progress
        // ------------------------------------------------------------------

        /// <summary>Called by LevelController when a level is completed.</summary>
        public void CompleteLevel(int completedLevelNumber, int nextLevelNumber)
        {
            HasProgress = true;
            LastPlayedLevel = Mathf.Max(1, nextLevelNumber);
            UnlockedLevel = Mathf.Max(UnlockedLevel, nextLevelNumber);
            Save();
        }

        /// <summary>"Continuer" destination: the next level to play, or level 1 when nothing was unlocked.</summary>
        public int ContinueLevelNumber => HasProgress ? Mathf.Max(1, LastPlayedLevel) : 1;

        public bool IsLevelUnlocked(int levelNumber) => levelNumber <= UnlockedLevel;

        public void ResetProgress()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            HasProgress = false;
            UnlockedLevel = 1;
            LastPlayedLevel = 1;

            // Volumes and sensitivity go back to their defaults too, then are re-saved.
            MasterVolume = 1f;
            MusicVolume = 0.6f;
            SfxVolume = 1f;
            MouseSensitivity = 0.14f;

            Save();
            SettingsChanged?.Invoke();
        }

        // ------------------------------------------------------------------
        //  Settings
        // ------------------------------------------------------------------

        public void SetVolumes(float master, float music, float sfx)
        {
            MasterVolume = Mathf.Clamp01(master);
            MusicVolume = Mathf.Clamp01(music);
            SfxVolume = Mathf.Clamp01(sfx);

            Save();
            SettingsChanged?.Invoke();
        }

        public void SetMouseSensitivity(float degreesPerPixel)
        {
            MouseSensitivity = Mathf.Clamp(degreesPerPixel, 0.02f, 0.6f);

            Save();
            SettingsChanged?.Invoke();
        }

        /// <summary>Writes the settings immediately. Used when a slider is released.</summary>
        public void SaveSettings()
        {
            Save();
            SettingsChanged?.Invoke();
        }

        // ------------------------------------------------------------------
        //  Storage
        // ------------------------------------------------------------------

        private void Load()
        {
            HasProgress = PlayerPrefs.GetInt(KeyHasProgress, 0) == 1;
            UnlockedLevel = Mathf.Max(1, PlayerPrefs.GetInt(KeyUnlockedLevel, 1));
            LastPlayedLevel = Mathf.Max(1, PlayerPrefs.GetInt(KeyLastPlayedLevel, 1));

            MasterVolume = PlayerPrefs.GetFloat(KeyMasterVolume, 1f);
            MusicVolume = PlayerPrefs.GetFloat(KeyMusicVolume, 0.6f);
            SfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, 1f);
            MouseSensitivity = PlayerPrefs.GetFloat(KeyMouseSensitivity, 0.14f);
        }

        private void Save()
        {
            PlayerPrefs.SetInt(KeyHasProgress, HasProgress ? 1 : 0);
            PlayerPrefs.SetInt(KeyUnlockedLevel, UnlockedLevel);
            PlayerPrefs.SetInt(KeyLastPlayedLevel, LastPlayedLevel);

            PlayerPrefs.SetFloat(KeyMasterVolume, MasterVolume);
            PlayerPrefs.SetFloat(KeyMusicVolume, MusicVolume);
            PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
            PlayerPrefs.SetFloat(KeyMouseSensitivity, MouseSensitivity);

            PlayerPrefs.Save();
        }
    }
}
