using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Serializable class for audio clip entries. Used in Inspector to assign clips with names.
    /// </summary>
    [System.Serializable]
    public class AudioClipEntry
    {
        [Tooltip("Unique identifier/name for this audio clip (e.g., 'ButtonClick', 'LevelComplete')")]
        public string id;
        
        [Tooltip("The audio clip to play")]
        public AudioClip clip;
        
        [Tooltip("Volume multiplier for this specific clip (0-1). Final volume = SoundVolume * volumeMultiplier")]
        [Range(0f, 1f)]
        public float volumeMultiplier = 1f;
    }

    /// <summary>
    /// Manages all audio playback with separate pools for sound effects and music.
    /// Supports independent volume controls for Sound and Music settings.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Pool Settings")]
        [SerializeField] private int soundEffectPoolSize = 10;
        [SerializeField] private int musicPoolSize = 2;

        [Header("Audio Clips Database")]
        [Tooltip("Assign sound effects here. Use unique IDs to reference them in code (e.g., PlaySound(\"ButtonClick\"))")]
        [SerializeField] private List<AudioClipEntry> soundEffects = new List<AudioClipEntry>();
        
        [Tooltip("Assign music tracks here. Use unique IDs to reference them in code (e.g., PlayMusic(\"MainMenu\"))")]
        [SerializeField] private List<AudioClipEntry> musicTracks = new List<AudioClipEntry>();

        [Header("Default Volumes")]
        [Range(0f, 1f)]
        [SerializeField] private float defaultSoundVolume = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float defaultMusicVolume = 0.7f;

        // Runtime dictionaries for fast lookup
        private Dictionary<string, AudioClipEntry> _soundEffectDict;
        private Dictionary<string, AudioClipEntry> _musicDict;

        // Audio source pools
        private Queue<AudioSource> _availableSoundSources;
        private Queue<AudioSource> _availableMusicSources;
        private List<AudioSource> _activeSoundSources;
        private List<AudioSource> _activeMusicSources;

        // Currently playing music
        private AudioSource _currentMusicSource;
        private AudioClip _currentMusicClip;

        // Settings keys
        private const string SOUND_ENABLED_KEY = "SoundEnabled";
        private const string MUSIC_ENABLED_KEY = "MusicEnabled";
        private const string SOUND_VOLUME_KEY = "SoundVolume";
        private const string MUSIC_VOLUME_KEY = "MusicVolume";

        // Settings properties
        public bool SoundEnabled
        {
            get => PlayerPrefs.GetInt(SOUND_ENABLED_KEY, 1) == 1;
            private set
            {
                PlayerPrefs.SetInt(SOUND_ENABLED_KEY, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public bool MusicEnabled
        {
            get => PlayerPrefs.GetInt(MUSIC_ENABLED_KEY, 1) == 1;
            private set
            {
                PlayerPrefs.SetInt(MUSIC_ENABLED_KEY, value ? 1 : 0);
                PlayerPrefs.Save();
                if (!value && _currentMusicSource != null)
                {
                    _currentMusicSource.Stop();
                }
            }
        }

        public float SoundVolume
        {
            get => PlayerPrefs.GetFloat(SOUND_VOLUME_KEY, defaultSoundVolume);
            private set
            {
                PlayerPrefs.SetFloat(SOUND_VOLUME_KEY, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                UpdateAllSoundVolumes();
            }
        }

        public float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, defaultMusicVolume);
            private set
            {
                PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                UpdateAllMusicVolumes();
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioDatabase();
            InitializePools();
        }

        private void InitializeAudioDatabase()
        {
            // Initialize sound effects dictionary
            _soundEffectDict = new Dictionary<string, AudioClipEntry>();
            foreach (var entry in soundEffects)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id) || entry.clip == null)
                    continue;

                if (_soundEffectDict.ContainsKey(entry.id))
                {
                    Debug.LogWarning($"[SoundManager] Duplicate sound effect ID: '{entry.id}'. Skipping duplicate.");
                    continue;
                }

                _soundEffectDict[entry.id] = entry;
            }

            // Initialize music dictionary
            _musicDict = new Dictionary<string, AudioClipEntry>();
            foreach (var entry in musicTracks)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id) || entry.clip == null)
                    continue;

                if (_musicDict.ContainsKey(entry.id))
                {
                    Debug.LogWarning($"[SoundManager] Duplicate music track ID: '{entry.id}'. Skipping duplicate.");
                    continue;
                }

                _musicDict[entry.id] = entry;
            }

            Debug.Log($"[SoundManager] Initialized {_soundEffectDict.Count} sound effects and {_musicDict.Count} music tracks.");
        }

        private void InitializePools()
        {
            // Initialize sound effect pool
            _availableSoundSources = new Queue<AudioSource>(soundEffectPoolSize);
            _activeSoundSources = new List<AudioSource>(soundEffectPoolSize);

            GameObject soundPoolParent = new GameObject("SoundEffectPool");
            soundPoolParent.transform.SetParent(transform);

            for (int i = 0; i < soundEffectPoolSize; i++)
            {
                AudioSource source = CreateAudioSource("SoundEffect_" + i, soundPoolParent.transform);
                source.playOnAwake = false;
                source.loop = false;
                _availableSoundSources.Enqueue(source);
            }

            // Initialize music pool
            _availableMusicSources = new Queue<AudioSource>(musicPoolSize);
            _activeMusicSources = new List<AudioSource>(musicPoolSize);

            GameObject musicPoolParent = new GameObject("MusicPool");
            musicPoolParent.transform.SetParent(transform);

            for (int i = 0; i < musicPoolSize; i++)
            {
                AudioSource source = CreateAudioSource("Music_" + i, musicPoolParent.transform);
                source.playOnAwake = false;
                source.loop = true;
                _availableMusicSources.Enqueue(source);
            }
        }

        private AudioSource CreateAudioSource(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            AudioSource source = go.AddComponent<AudioSource>();
            source.spatialBlend = 0f; // 2D sound
            return source;
        }

        /// <summary>
        /// Plays a sound effect by ID from the assigned audio clips. Returns the AudioSource if you need to control it further.
        /// </summary>
        public AudioSource PlaySound(string soundId)
        {
            if (string.IsNullOrEmpty(soundId))
            {
                Debug.LogWarning("[SoundManager] Sound ID is null or empty.");
                return null;
            }

            if (_soundEffectDict == null || !_soundEffectDict.ContainsKey(soundId))
            {
                Debug.LogWarning($"[SoundManager] Sound effect '{soundId}' not found in database. Make sure it's assigned in the Inspector.");
                return null;
            }

            var entry = _soundEffectDict[soundId];
            return PlaySound(entry.clip, entry.volumeMultiplier);
        }

        /// <summary>
        /// Plays a sound effect. Returns the AudioSource if you need to control it further.
        /// </summary>
        public AudioSource PlaySound(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (clip == null || !SoundEnabled) return null;

            AudioSource source = GetSoundSource();
            if (source == null) return null;

            source.clip = clip;
            source.volume = SoundVolume * volumeMultiplier;
            source.Play();

            StartCoroutine(ReturnSoundSourceWhenFinished(source, clip.length));
            return source;
        }

        /// <summary>
        /// Plays a sound effect at a specific position (for 3D sounds if needed).
        /// </summary>
        public AudioSource PlaySoundAtPosition(AudioClip clip, Vector3 position, float volumeMultiplier = 1f)
        {
            if (clip == null || !SoundEnabled) return null;

            AudioSource source = GetSoundSource();
            if (source == null) return null;

            source.transform.position = position;
            source.clip = clip;
            source.volume = SoundVolume * volumeMultiplier;
            source.Play();

            StartCoroutine(ReturnSoundSourceWhenFinished(source, clip.length));
            return source;
        }

        /// <summary>
        /// Plays background music by ID from the assigned audio clips. Only one music track can play at a time.
        /// </summary>
        public void PlayMusic(string musicId, bool loop = true, float fadeInDuration = 0f)
        {
            if (string.IsNullOrEmpty(musicId))
            {
                Debug.LogWarning("[SoundManager] Music ID is null or empty.");
                return;
            }

            if (_musicDict == null || !_musicDict.ContainsKey(musicId))
            {
                Debug.LogWarning($"[SoundManager] Music track '{musicId}' not found in database. Make sure it's assigned in the Inspector.");
                return;
            }

            var entry = _musicDict[musicId];
            PlayMusic(entry.clip, loop, fadeInDuration, entry.volumeMultiplier);
        }

        /// <summary>
        /// Plays background music. Only one music track can play at a time.
        /// </summary>
        public void PlayMusic(AudioClip clip, bool loop = true, float fadeInDuration = 0f)
        {
            PlayMusic(clip, loop, fadeInDuration, 1f);
        }

        /// <summary>
        /// Plays background music with volume multiplier. Only one music track can play at a time.
        /// </summary>
        private void PlayMusic(AudioClip clip, bool loop, float fadeInDuration, float volumeMultiplier)
        {
            if (clip == null) return;

            // If same music is already playing, do nothing
            if (_currentMusicSource != null && _currentMusicSource.isPlaying && _currentMusicClip == clip)
                return;

            // Stop current music if playing
            StopMusic(fadeInDuration > 0f ? fadeInDuration * 0.5f : 0f);

            if (!MusicEnabled) return;

            AudioSource source = GetMusicSource();
            if (source == null) return;

            _currentMusicSource = source;
            _currentMusicClip = clip;

            source.clip = clip;
            source.loop = loop;
            source.volume = 0f; // Start at 0 for fade in
            source.Play();

            float targetVolume = MusicVolume * volumeMultiplier;

            if (fadeInDuration > 0f)
            {
                StartCoroutine(FadeInMusic(source, fadeInDuration, targetVolume));
            }
            else
            {
                source.volume = targetVolume;
            }
        }

        /// <summary>
        /// Stops the currently playing music.
        /// </summary>
        public void StopMusic(float fadeOutDuration = 0f)
        {
            if (_currentMusicSource == null || !_currentMusicSource.isPlaying) return;

            if (fadeOutDuration > 0f)
            {
                StartCoroutine(FadeOutAndStopMusic(_currentMusicSource, fadeOutDuration));
            }
            else
            {
                _currentMusicSource.Stop();
                ReturnMusicSource(_currentMusicSource);
                _currentMusicSource = null;
                _currentMusicClip = null;
            }
        }

        /// <summary>
        /// Pauses the currently playing music.
        /// </summary>
        public void PauseMusic()
        {
            if (_currentMusicSource != null && _currentMusicSource.isPlaying)
            {
                _currentMusicSource.Pause();
            }
        }

        /// <summary>
        /// Resumes the paused music.
        /// </summary>
        public void ResumeMusic()
        {
            if (_currentMusicSource != null && !_currentMusicSource.isPlaying && MusicEnabled)
            {
                _currentMusicSource.UnPause();
            }
        }

        /// <summary>
        /// Toggles sound effects on/off.
        /// </summary>
        public void ToggleSound()
        {
            SoundEnabled = !SoundEnabled;
        }

        /// <summary>
        /// Toggles music on/off.
        /// </summary>
        public void ToggleMusic()
        {
            MusicEnabled = !MusicEnabled;
            if (MusicEnabled && _currentMusicClip != null)
            {
                PlayMusic(_currentMusicClip);
            }
        }

        /// <summary>
        /// Sets sound effects volume (0-1).
        /// </summary>
        public void SetSoundVolume(float volume)
        {
            SoundVolume = volume;
        }

        /// <summary>
        /// Sets music volume (0-1).
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            MusicVolume = volume;
        }

        private AudioSource GetSoundSource()
        {
            AudioSource source = null;

            if (_availableSoundSources.Count > 0)
            {
                source = _availableSoundSources.Dequeue();
            }
            else
            {
                // Pool exhausted, create a new one (shouldn't happen often)
                Debug.LogWarning("[SoundManager] Sound effect pool exhausted, creating new source.");
                source = CreateAudioSource("SoundEffect_Extra", transform.Find("SoundEffectPool"));
                source.playOnAwake = false;
                source.loop = false;
            }

            _activeSoundSources.Add(source);
            return source;
        }

        private AudioSource GetMusicSource()
        {
            AudioSource source = null;

            if (_availableMusicSources.Count > 0)
            {
                source = _availableMusicSources.Dequeue();
            }
            else
            {
                // Pool exhausted, create a new one
                Debug.LogWarning("[SoundManager] Music pool exhausted, creating new source.");
                source = CreateAudioSource("Music_Extra", transform.Find("MusicPool"));
                source.playOnAwake = false;
                source.loop = true;
            }

            _activeMusicSources.Add(source);
            return source;
        }

        private void ReturnSoundSource(AudioSource source)
        {
            if (source == null) return;

            source.Stop();
            source.clip = null;
            _activeSoundSources.Remove(source);
            _availableSoundSources.Enqueue(source);
        }

        private void ReturnMusicSource(AudioSource source)
        {
            if (source == null) return;

            source.Stop();
            source.clip = null;
            _activeMusicSources.Remove(source);
            _availableMusicSources.Enqueue(source);
        }

        private IEnumerator ReturnSoundSourceWhenFinished(AudioSource source, float duration)
        {
            yield return new WaitForSeconds(duration);
            ReturnSoundSource(source);
        }

        private IEnumerator FadeInMusic(AudioSource source, float duration, float targetVolume = -1f)
        {
            if (targetVolume < 0f) targetVolume = MusicVolume;

            float elapsed = 0f;

            while (elapsed < duration && source != null && source.isPlaying)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
                yield return null;
            }

            if (source != null)
            {
                source.volume = targetVolume;
            }
        }

        private IEnumerator FadeOutAndStopMusic(AudioSource source, float duration)
        {
            float startVolume = source.volume;
            float elapsed = 0f;

            while (elapsed < duration && source != null && source.isPlaying)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            if (source != null)
            {
                ReturnMusicSource(source);
                if (source == _currentMusicSource)
                {
                    _currentMusicSource = null;
                    _currentMusicClip = null;
                }
            }
        }

        private void UpdateAllSoundVolumes()
        {
            foreach (var source in _activeSoundSources)
            {
                if (source != null && source.isPlaying)
                {
                    source.volume = SoundVolume;
                }
            }
        }

        private void UpdateAllMusicVolumes()
        {
            if (_currentMusicSource != null && _currentMusicSource.isPlaying)
            {
                _currentMusicSource.volume = MusicVolume;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}

