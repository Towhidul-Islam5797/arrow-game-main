using UnityEngine;
using UnityEngine.UI;
#if MT_DOTWEEN
using DG.Tweening;
#endif

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Manages the settings UI panel with toggle switches for Sound, Music, and other settings.
    /// </summary>
    public class SettingsUIManager : MonoBehaviour
    {
        private SoundManager SoundManager => SoundManager.Instance;
        [Header("Settings Panel")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button closeButton;

        [Header("Sound Toggle")]
        [SerializeField] private Toggle soundToggle;
        [SerializeField] private Image soundToggleBackground;
        [SerializeField] private RectTransform soundToggleHandle;
        [SerializeField] private Color soundToggleOnColor = new Color(0.2f, 0.8f, 0.2f); // Green
        [SerializeField] private Color soundToggleOffColor = new Color(0.5f, 0.7f, 0.9f); // Light Blue

        [Header("Music Toggle")]
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Image musicToggleBackground;
        [SerializeField] private RectTransform musicToggleHandle;
        [SerializeField] private Color musicToggleOnColor = new Color(0.2f, 0.8f, 0.2f); // Green
        [SerializeField] private Color musicToggleOffColor = new Color(0.5f, 0.7f, 0.9f); // Light Blue

        [Header("Animation Settings")]
        [SerializeField] private float toggleAnimationDuration = 0.2f;
#if MT_DOTWEEN
        [SerializeField] private Ease toggleEase = Ease.OutQuad;
#endif

        private Vector2 _toggleHandleOnPosition;
        private Vector2 _toggleHandleOffPosition;

        private LevelState previousLevelState;

        void Start()
        {
            InitializeToggles();
            SetupButtonListeners();
        }

        private void InitializeToggles()
        {
            // Get SoundManager instance
            if (SoundManager == null)
            {
                Debug.LogWarning("[SettingsUIManager] SoundManager instance not found!");
                return;
            }
            // Calculate toggle handle positions
            if (soundToggleHandle != null)
                CalculateTogglePositions(soundToggleHandle);
            else if (musicToggleHandle != null)
                CalculateTogglePositions(musicToggleHandle);

            // Initialize sound toggle
            if (soundToggle != null)
            {
                soundToggle.isOn = SoundManager.SoundEnabled;
                UpdateSoundToggleVisual(soundToggle.isOn, false);
                soundToggle.onValueChanged.AddListener(OnSoundToggleChanged);
            }

            // Initialize music toggle
            if (musicToggle != null)
            {
                musicToggle.isOn = SoundManager.MusicEnabled;
                UpdateMusicToggleVisual(musicToggle.isOn, false);
                musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
            }

        }

        private void CalculateTogglePositions(RectTransform handle)
        {
            // Get the parent toggle background to calculate positions
            RectTransform toggleRect = handle.parent as RectTransform;
            if (toggleRect == null) return;

            float toggleWidth = toggleRect.rect.width;
            float handleWidth = handle.rect.width;
            float padding = (toggleWidth - handleWidth) * 0.5f;

            // ON position: handle on the right
            _toggleHandleOnPosition = new Vector2(padding, 0);
            // OFF position: handle on the left
            _toggleHandleOffPosition = new Vector2(-padding, 0);
        }

        private void SetupButtonListeners()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseSettings);
            }
        }

        private void OnSoundToggleChanged(bool isOn)
        {
            if (SoundManager == null) return;

            SoundManager.ToggleSound();
            UpdateSoundToggleVisual(isOn, true);

            // Play toggle sound (only if sound is enabled)
            if (SoundManager != null && SoundManager.SoundEnabled)
            {
                SoundManager.PlaySound("button");
            }
        }

        private void OnMusicToggleChanged(bool isOn)
        {
            if (SoundManager == null) return;

            SoundManager.ToggleMusic();
            UpdateMusicToggleVisual(isOn, true);

            // Play toggle sound (only if sound is enabled)
            if (SoundManager != null && SoundManager.SoundEnabled)
            {
                SoundManager.PlaySound("button");
            }
        }

        private void UpdateSoundToggleVisual(bool isOn, bool animate = true)
        {
            if (soundToggleBackground == null || soundToggleHandle == null) return;

            Color targetColor = isOn ? soundToggleOnColor : soundToggleOffColor;
            Vector2 targetPosition = isOn ? _toggleHandleOnPosition : _toggleHandleOffPosition;

            if (animate)
            {
#if MT_DOTWEEN
                soundToggleBackground.DOColor(targetColor, toggleAnimationDuration).SetEase(toggleEase);
                soundToggleHandle.DOAnchorPos(targetPosition, toggleAnimationDuration).SetEase(toggleEase);
#else
                // Fallback: instant update without tween
                soundToggleBackground.color = targetColor;
                soundToggleHandle.anchoredPosition = targetPosition;
#endif
            }
            else
            {
                soundToggleBackground.color = targetColor;
                soundToggleHandle.anchoredPosition = targetPosition;
            }
            Debug.Log("UpdateSoundToggleVisual: " + isOn);
        }

        private void UpdateMusicToggleVisual(bool isOn, bool animate = true)
        {
            if (musicToggleBackground == null || musicToggleHandle == null) return;

            Color targetColor = isOn ? musicToggleOnColor : musicToggleOffColor;
            Vector2 targetPosition = isOn ? _toggleHandleOnPosition : _toggleHandleOffPosition;

            if (animate)
            {
#if MT_DOTWEEN
                musicToggleBackground.DOColor(targetColor, toggleAnimationDuration).SetEase(toggleEase);
                musicToggleHandle.DOAnchorPos(targetPosition, toggleAnimationDuration).SetEase(toggleEase);
#else
                // Fallback: instant update without tween
                musicToggleBackground.color = targetColor;
                musicToggleHandle.anchoredPosition = targetPosition;
#endif
            }
            else
            {
                musicToggleBackground.color = targetColor;
                musicToggleHandle.anchoredPosition = targetPosition;
            }
        }

        public void OpenSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
            }
            SoundManager.PlaySound("button");
            previousLevelState = LevelManager.Instance.levelState;
            LevelManager.Instance.levelState = LevelState.NotStarted;

            // Refresh toggle states in case they changed elsewhere
            RefreshToggleStates();
        }

        public void CloseSettings()
        {
            if (SoundManager != null)
                SoundManager.PlaySound("close");

            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            LevelManager.Instance.levelState = previousLevelState;
        }

        private void RefreshToggleStates()
        {
            if (SoundManager == null) return;

            if (soundToggle != null)
            {
                soundToggle.isOn = SoundManager.SoundEnabled;
                UpdateSoundToggleVisual(soundToggle.isOn, false);
            }

            if (musicToggle != null)
            {
                musicToggle.isOn = SoundManager.MusicEnabled;
                UpdateMusicToggleVisual(musicToggle.isOn, false);
            }
        }

        void OnDestroy()
        {
            if (soundToggle != null)
            {
                soundToggle.onValueChanged.RemoveListener(OnSoundToggleChanged);
            }

            if (musicToggle != null)
            {
                musicToggle.onValueChanged.RemoveListener(OnMusicToggleChanged);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseSettings);
            }
        }
    }
}

