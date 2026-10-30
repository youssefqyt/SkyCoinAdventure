using System;
using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// Music and sound effects.
    ///
    /// THE SOUNDS ARE GENERATED IN CODE, on purpose: the project must be buildable with
    /// no downloads, and placeholder audio is far better than silence while you build.
    /// Each clip can be replaced with a real file at any time by assigning the matching
    /// "Override" field in the Inspector - no code change needed.
    ///
    /// Volume routing: final volume = master x channel, exactly like the Options screen
    /// promises. Audio keeps playing while the game is paused (the pause menu does not
    /// touch AudioListener.pause), which is intentional for a title screen and a pause menu.
    /// </summary>
    [DefaultExecutionOrder(-440)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const int SampleRate = 44100;

        [Header("Real clips (optional). Leave empty to use the generated placeholders.")]
        [SerializeField] private AudioClip coinOverride;
        [SerializeField] private AudioClip clickOverride;
        [SerializeField] private AudioClip victoryOverride;
        [SerializeField] private AudioClip defeatOverride;
        [SerializeField] private AudioClip musicOverride;

        [Header("Music")]
        [Tooltip("Music volume is master x music x this value.")]
        [Range(0f, 1f)]
        [SerializeField] private float musicHeadroom = 0.5f;

        [SerializeField] private bool playMusicOnStart = true;

        private AudioSource _musicSource;
        private AudioSource _sfxSource;

        private AudioClip _coinClip;
        private AudioClip _clickClip;
        private AudioClip _victoryClip;
        private AudioClip _defeatClip;
        private AudioClip _musicClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.spatialBlend = 0f;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;

            _coinClip = coinOverride != null ? coinOverride : BuildCoinClip();
            _clickClip = clickOverride != null ? clickOverride : BuildClickClip();
            _victoryClip = victoryOverride != null ? victoryOverride : BuildVictoryClip();
            _defeatClip = defeatOverride != null ? defeatOverride : BuildDefeatClip();
            _musicClip = musicOverride != null ? musicOverride : BuildMusicLoop();
        }

        private void Start()
        {
            ApplyVolumes();

            if (SaveService.Instance != null)
            {
                SaveService.Instance.SettingsChanged += ApplyVolumes;
            }

            if (playMusicOnStart) PlayMusic();
        }

        private void OnDestroy()
        {
            // Always unsubscribe. An unbounded subscription here would throw after a few scene loads.
            if (SaveService.Instance != null)
            {
                SaveService.Instance.SettingsChanged -= ApplyVolumes;
            }

            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        //  Playback
        // ------------------------------------------------------------------

        public void PlayCoin() => PlaySfx(_coinClip, 1f);
        public void PlayClick() => PlaySfx(_clickClip, 0.7f);
        public void PlayVictory() => PlaySfx(_victoryClip, 1f);
        public void PlayDefeat() => PlaySfx(_defeatClip, 1f);

        public void PlayMusic()
        {
            if (_musicClip == null || _musicSource == null) return;

            _musicSource.clip = _musicClip;
            _musicSource.Play();
        }

        public void ApplyVolumes()
        {
            float master = SaveService.Instance != null ? SaveService.Instance.MasterVolume : 1f;
            float music = SaveService.Instance != null ? SaveService.Instance.MusicVolume : 0.6f;
            float sfx = SaveService.Instance != null ? SaveService.Instance.SfxVolume : 1f;

            if (_musicSource != null) _musicSource.volume = Mathf.Clamp01(master * music) * musicHeadroom;
            if (_sfxSource != null) _sfxSource.volume = Mathf.Clamp01(master * sfx);
        }

        private void PlaySfx(AudioClip clip, float volume)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        // ------------------------------------------------------------------
        //  Procedural placeholder clips
        // ------------------------------------------------------------------

        private static AudioClip BuildClip(string name, float duration, Func<float, float> sampleFunction)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                data[i] = Mathf.Clamp(sampleFunction(time), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Short attack then exponential decay inside a [start, end] window.</summary>
        private static float Envelope(float time, float start, float end, float decay)
        {
            if (time < start || time > end) return 0f;

            float local = (time - start) / Mathf.Max(0.0001f, end - start);
            float attack = Mathf.Clamp01(local / 0.08f);
            return attack * Mathf.Exp(-decay * local);
        }

        private static float Sine(float frequency, float time)
        {
            return Mathf.Sin(2f * Mathf.PI * frequency * time);
        }

        private static AudioClip BuildCoinClip()
        {
            // Two quick bright notes: the classic "ding".
            return BuildClip("SFX_Coin_Generated", 0.24f, time =>
            {
                float first = Sine(1046.5f, time) * Envelope(time, 0f, 0.10f, 5f);
                float second = Sine(1568f, time) * Envelope(time, 0.09f, 0.24f, 5f);
                return (first + second) * 0.5f;
            });
        }

        private static AudioClip BuildClickClip()
        {
            return BuildClip("SFX_Click_Generated", 0.06f, time => Sine(1800f, time) * Envelope(time, 0f, 0.06f, 8f) * 0.8f);
        }

        private static AudioClip BuildVictoryClip()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };   // C5 E5 G5 C6
            const float step = 0.22f;

            return BuildClip("SFX_Victory_Generated", step * notes.Length + 0.3f, time =>
            {
                int index = Mathf.Clamp((int)(time / step), 0, notes.Length - 1);
                float start = index * step;
                return Sine(notes[index], time) * Envelope(time, start, start + step + 0.3f, 4f) * 0.55f;
            });
        }

        private static AudioClip BuildDefeatClip()
        {
            float[] notes = { 392f, 329.63f, 261.63f };   // G4 E4 C4
            const float step = 0.26f;

            return BuildClip("SFX_Defeat_Generated", step * notes.Length + 0.4f, time =>
            {
                int index = Mathf.Clamp((int)(time / step), 0, notes.Length - 1);
                float start = index * step;
                return Sine(notes[index], time) * Envelope(time, start, start + step + 0.4f, 3f) * 0.6f;
            });
        }

        private static AudioClip BuildMusicLoop()
        {
            // A soft three tone pad. Frequencies are quantised to a whole number of cycles
            // inside the loop, so the loop point is seamless instead of a click.
            const float duration = 8f;
            const float tremoloFrequency = 0.5f;   // 4 whole cycles in 8 seconds

            float low = QuantiseToLoop(110f, duration);
            float mid = QuantiseToLoop(164.81f, duration);
            float high = QuantiseToLoop(220f, duration);

            return BuildClip("Music_Loop_Generated", duration, time =>
            {
                float chord =
                    (Sine(low, time) + (0.7f * Sine(mid, time)) + (0.5f * Sine(high, time))) / 2.2f;

                float swell = 0.6f + (0.4f * Sine(tremoloFrequency, time));
                return chord * swell * 0.35f;
            });
        }

        /// <summary>Rounds a frequency so the waveform completes whole cycles inside the loop.</summary>
        private static float QuantiseToLoop(float frequency, float duration)
        {
            float step = 1f / duration;
            return Mathf.Max(step, Mathf.Round(frequency / step) * step);
        }
    }
}
