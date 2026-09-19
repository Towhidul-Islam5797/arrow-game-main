using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections;
#if MT_DOTWEEN
using DG.Tweening;
#endif

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Handles all user interface states for menus, gameplay HUD, and result screens.
    /// </summary>
    public class UiManager : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenu;
        [SerializeField] private GameObject playUI;
        [SerializeField] private GameObject levelCompleteUI;
        [SerializeField] private GameObject levelFailedUI;
        [SerializeField] private GameObject shopUI;
        [SerializeField] private SettingsUIManager settingsUI;
        [SerializeField] private FeatureManager featureManager;
        [Header("Main Menu UI")]
        [SerializeField] private TextMeshProUGUI levelNoText;
        [SerializeField] private TextMeshProUGUI levelDifficultyText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private Button playButton;
        [SerializeField] private Button closeShopButton;
        [SerializeField] private Button[] settingsButtons;
        [SerializeField] private Button[] shopButtons;
        [SerializeField] public GameObject[] adsButtons;

        [Header("Play UI")]
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private TextMeshProUGUI coinsTotalText;
        [SerializeField] private GameObject[] livesIcons;
        [SerializeField] private GameObject liveLoseAnim;
        [SerializeField] private Button mainMenuPlayButton;
        [Header("Level Complete UI")]
        [SerializeField] private TextMeshProUGUI coinsEarnedText;
        [SerializeField] private Button levelCompleteRvButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button mainMenuCompleteButton;

        [SerializeField] private GameObject confity;

        [Header("Coin Animation UI")]
        [SerializeField] private GameObject coinsEarnedAnim;
        [SerializeField] private RectTransform startRectTransform;
        [SerializeField] private float spreadRadius = 200f;// Calculate spread radius for initial positions
        [SerializeField] private float animationDuration = 1.5f;
        [SerializeField] private float delayBetweenCoins = 0.1f;


        [Header("Level Failed UI")]
        [SerializeField] private Button mainMenuFailedButton;
        [SerializeField] private Button retryByAdsButton;
        [SerializeField] private Button retryByCoinsButton;
        [SerializeField] private TextMeshProUGUI coinsRequiredText;

        private int lives;

        private AdsManager Ads => AdsManager.Instance;
        private LevelManager levelManager;

        public event Action<int> OnCoinsChanged;
        [Header("Coins")]
        public int coins
        {
            get => PlayerPrefs.GetInt("Coins", 0);
            private set => PlayerPrefs.SetInt("Coins", value);
        }
        public void AddCoins(int amount)
        {
            coins += amount;
            OnCoinsChanged?.Invoke(coins);
        }
        public void SpendCoins(int amount)
        {
            coins -= amount;
            OnCoinsChanged?.Invoke(coins);
        }

        void Start()
        {
            levelManager = LevelManager.Instance;

            playButton?.onClick.AddListener(PlayButtonClicked);
            levelCompleteRvButton?.onClick.AddListener(LevelCompleteRvButtonClicked);
            nextLevelButton?.onClick.AddListener(NextLevelButtonClicked);

            retryByCoinsButton?.onClick.AddListener(RetryByCoinsButtonClicked);
            retryByAdsButton?.onClick.AddListener(RetryByAdsButtonClicked);

            mainMenuFailedButton?.onClick.AddListener(MainMenuButtonClicked);
            mainMenuPlayButton?.onClick.AddListener(MainMenuButtonClicked);
            mainMenuCompleteButton?.onClick.AddListener(MainMenuButtonClicked);

            foreach (var button in settingsButtons)
            {
                button?.onClick.AddListener(OpenSettingsUI);
            }
#if MT_IAP
            foreach (var button in shopButtons)
            {
                button?.onClick.AddListener(OpenShopUI);
            }
            closeShopButton?.onClick.AddListener(CloseShopUI);
#else
            foreach (var button in shopButtons)
            {
                button?.gameObject.SetActive(false);
            }
            foreach (var button in adsButtons)
            {
                button?.gameObject.SetActive(false);
            }
            closeShopButton?.gameObject.SetActive(false);
#endif
            UpdateLevelText();
            UpdateCoinsText();
            if (confity != null) confity.SetActive(false);
            UpdateLives();
            OnCoinsChanged += OnCoinsChangedHandler;
            SoundManager.Instance.PlayMusic("music1", loop: true, fadeInDuration: 1f);
        }
        void OnDisable()
        {
            OnCoinsChanged -= OnCoinsChangedHandler;
            playButton?.onClick.RemoveListener(PlayButtonClicked);
            levelCompleteRvButton?.onClick.RemoveListener(LevelCompleteRvButtonClicked);
            nextLevelButton?.onClick.RemoveListener(NextLevelButtonClicked);
            foreach (var button in settingsButtons)
            {
                button?.onClick.RemoveListener(OpenSettingsUI);
            }

            retryByCoinsButton?.onClick.RemoveListener(RetryByCoinsButtonClicked);
            retryByAdsButton?.onClick.RemoveListener(RetryByAdsButtonClicked);

            mainMenuFailedButton?.onClick.RemoveListener(MainMenuButtonClicked);
            mainMenuPlayButton?.onClick.RemoveListener(MainMenuButtonClicked);
            mainMenuCompleteButton?.onClick.RemoveListener(MainMenuButtonClicked);
#if MT_IAP
            foreach (var button in shopButtons)
            {
                button?.onClick.RemoveListener(OpenShopUI);
            }
            closeShopButton?.onClick.RemoveListener(CloseShopUI);
#endif
        }

        void OnCoinsChangedHandler(int coins)
        {
            UpdateCoinsText();
        }

        void PlayButtonClicked()
        {
            if (confity != null) confity.SetActive(false);
            levelManager.firebaseManager?.SendLevelData();
            levelManager.ShowTutorial();

            if (mainMenu != null) mainMenu.SetActive(false);
            if (playUI != null) playUI.SetActive(true);
            if (levelCompleteUI != null) levelCompleteUI.SetActive(false);
            if (levelFailedUI != null) levelFailedUI.SetActive(false);
            if (shopUI != null) shopUI.SetActive(false);

            UpdateLevelText();
            UpdateCoinsText();
            UpdateLives();
            GridManager.I?.AnimateDotsOnLevelStart();
            levelManager.levelState = LevelState.Playing;
            SoundManager.Instance.PlaySound("button");
        }

        public void UpdateCoinsText()
        {
            if (coinsText != null) coinsText.text = coins.ToString();
            if (coinsTotalText != null) coinsTotalText.text = coins.ToString();
        }

        /// <summary>
        /// Refreshes the life indicator icons, optionally overriding the current value.
        /// </summary>
        public void UpdateLives(int livesOverride = -1)
        {
            if (livesIcons == null) return;

            if (livesOverride <= -1)
            {
                lives = levelManager != null ? levelManager.startLives : livesIcons.Length;
            }
            else
            {
                lives = livesOverride;
            }

            lives = Mathf.Clamp(lives, 0, livesIcons.Length);

            for (int i = 0; i < livesIcons.Length; i++)
            {
                if (livesIcons[i] != null)
                {
                    livesIcons[i].SetActive(i < lives);
                }
            }
        }

        public void RemoveLife()
        {
            ShowLiveLoseAnim();
            lives = Mathf.Max(0, lives - 1);

            if (livesIcons != null && livesIcons.Length > 0)
            {
                int index = Mathf.Clamp(lives, 0, livesIcons.Length - 1);
                if (livesIcons[index] != null) livesIcons[index].SetActive(false);
            }

            if (lives <= 0)
            {
                if (levelManager != null) levelManager.levelState = LevelState.Failed;
                Invoke(nameof(LevelFailed), 2f);
            }
        }

        public void ShowLiveLoseAnim()
        {
            if (liveLoseAnim == null) return;

            liveLoseAnim.SetActive(true);
            var animationComponent = liveLoseAnim.GetComponent<Animation>();
            if (animationComponent != null) animationComponent.Play();
            Invoke(nameof(HideLiveLoseAnim), 2f);
        }
        private void OpenSettingsUI()
        {
            settingsUI.OpenSettings();
        }

        private void HideLiveLoseAnim()
        {
            if (liveLoseAnim != null) liveLoseAnim.SetActive(false);
        }

        void LevelCompleteRvButtonClicked()
        {
            SoundManager.Instance.PlaySound("button");
            var ads = Ads;
            if (ads == null || !ads.IsRewardVideoReady()) return;

            ads.ShowRewardedAd(() =>
            {
                if (levelManager != null)
                {
                    AddCoins(levelManager.winCoinAmount * 2);
                    levelManager.LoadNextLevel();
                }
                PlayButtonClicked();
            });
        }

        void NextLevelButtonClicked()
        {
            var ads = Ads;

            if (levelManager != null && ads != null && ads.CanShowInterstitialAfterLevel(levelManager.currentLevelIndex))
            {
                ads.ShowInterstitialAd();
            }

            if (levelManager != null)
            {
                AddCoins(levelManager.winCoinAmount);
                levelManager.LoadNextLevel();
            }
            PlayButtonClicked();
        }
        public void LevelComplete()
        {
            if (confity != null)
            {
                confity.SetActive(true);
                var particles = confity.GetComponent<ParticleSystem>();
                if (particles != null) particles.Play();
            }

            GridManager.I?.AnimateDotsOnLevelComplete();
            levelManager?.IncreaseLevelNumber();
            SoundManager.Instance.PlaySound("level_complete");
            levelManager.levelState = LevelState.Completed;
            Invoke(nameof(ShowLevelCompleteUI), 1f);
        }

        private void ShowLevelCompleteUI()
        {
            var ads = Ads;

            if (coinsEarnedText != null && levelManager != null)
                coinsEarnedText.text = $"+{levelManager.winCoinAmount}";

            if (levelCompleteRvButton != null)
                levelCompleteRvButton.interactable = ads != null && ads.IsRewardVideoReady();
            StartCoinAnimation(levelManager.winCoinAmount);

            if (levelCompleteUI != null) levelCompleteUI.SetActive(true);
            if (levelFailedUI != null) levelFailedUI.SetActive(false);
            if (mainMenu != null) mainMenu.SetActive(false);
            if (shopUI != null) shopUI.SetActive(false);
        }

        private void UpdateLevelText()
        {
            if (levelManager == null || levelManager.levels == null || levelManager.levels.Count == 0) return;

            int levelNumber = levelManager.currentLevelIndex + 1;
            var levelAsset = levelManager.levels[levelManager.currentLevelIndex];
            LevelDifficulty difficulty = levelAsset.levelDifficulty;
            string difficultyString = difficulty.ToString();
            Color difficultyColor = GetDifficultyColor(difficulty);

            if (levelNoText != null) levelNoText.text = $"Level {levelNumber}";

            if (levelDifficultyText != null)
            {
                levelDifficultyText.text = difficultyString;
                var parentImage = levelDifficultyText.transform.parent.GetComponent<Image>();
                if (parentImage != null) parentImage.color = difficultyColor;
            }

            if (levelText != null) levelText.text = $"Level {levelNumber}";
            if (difficultyText != null)
            {
                difficultyText.text = difficultyString;
                difficultyText.color = difficultyColor;
            }
        }

        private void LevelFailed()
        {
            var ads = Ads;

            if (levelManager != null && coinsRequiredText != null)
                coinsRequiredText.text = $"{levelManager.coinsRequiredToRetry}";

            if (retryByCoinsButton != null && levelManager != null)
                retryByCoinsButton.interactable = coins >= levelManager.coinsRequiredToRetry;

            if (retryByAdsButton != null)
                retryByAdsButton.interactable = ads != null && ads.IsRewardVideoReady();

            if (levelFailedUI != null) levelFailedUI.SetActive(true);
            if (levelCompleteUI != null) levelCompleteUI.SetActive(false);
            if (mainMenu != null) mainMenu.SetActive(false);
            if (playUI != null) playUI.SetActive(false);
            if (shopUI != null) shopUI.SetActive(false);
            levelManager.levelState = LevelState.Failed;
        }

        private void RetryByCoinsButtonClicked()
        {
            if (levelManager == null) return;

            if (coins < levelManager.coinsRequiredToRetry) return;
            SpendCoins(levelManager.coinsRequiredToRetry);
            LoadLevel(levelManager.startLives);
            SoundManager.Instance.PlaySound("button");

            if (mainMenu != null) mainMenu.SetActive(false);
            if (playUI != null) playUI.SetActive(true);
            if (levelCompleteUI != null) levelCompleteUI.SetActive(false);
            if (levelFailedUI != null) levelFailedUI.SetActive(false);
            if (shopUI != null) shopUI.SetActive(false);
            levelManager.levelState = LevelState.Playing;
        }

        private void RetryByAdsButtonClicked()
        {
            SoundManager.Instance.PlaySound("button");
            var ads = Ads;

            if (ads == null || levelManager == null || !ads.IsRewardVideoReady()) return;

            ads.ShowRewardedAd(() =>
            {
                LoadLevel(1);
                if (mainMenu != null) mainMenu.SetActive(false);
                if (playUI != null) playUI.SetActive(true);
                if (levelCompleteUI != null) levelCompleteUI.SetActive(false);
                if (levelFailedUI != null) levelFailedUI.SetActive(false);
                if (shopUI != null) shopUI.SetActive(false);
                levelManager.levelState = LevelState.Playing;
            });
        }

        private void MainMenuButtonClicked()
        {
            SoundManager.Instance.PlaySound("button");
            levelManager?.ReloadLevel();
            LoadLevel();
            if (mainMenu != null) mainMenu.SetActive(true);
            if (playUI != null) playUI.SetActive(false);
            if (levelCompleteUI != null) levelCompleteUI.SetActive(false);
            if (levelFailedUI != null) levelFailedUI.SetActive(false);
            if (levelManager != null) levelManager.levelState = LevelState.NotStarted;
            if (shopUI != null) shopUI.SetActive(false);
        }

        public void LoadLevel(int setLives = -1)
        {
            UpdateLevelText();
            UpdateCoinsText();
            if (confity != null) confity.SetActive(false);
            UpdateLives(setLives);


            if (levelManager != null) levelManager.levelState = LevelState.Playing;
        }

        public void OpenShopUI()
        {
#if MT_IAP
            SoundManager.Instance.PlaySound("button");
            if (shopUI != null) shopUI.SetActive(true);
            if (mainMenu != null) mainMenu.SetActive(false);
            if (playUI != null) playUI.SetActive(false);
            if (levelCompleteUI != null) levelCompleteUI.SetActive(false);
            if (levelFailedUI != null) levelFailedUI.SetActive(false);
#endif
        }

        private void CloseShopUI()
        {
            SoundManager.Instance.PlaySound("close");
            if (shopUI != null) shopUI.SetActive(false);
            if (mainMenu != null) mainMenu.SetActive(true);
            if (playUI != null) playUI.SetActive(false);
            if (levelCompleteUI != null) levelCompleteUI.SetActive(false);
            if (levelFailedUI != null) levelFailedUI.SetActive(false);
        }
        public void SetAdsButtonActive(bool isActive)
        {
            if (adsButtons != null)
            {
                foreach (var button in adsButtons)
                {
                    if (button != null) button.SetActive(isActive);
                }
            }
        }

        private void StartCoinAnimation(int amount)
        {
            if (startRectTransform == null || coinsEarnedAnim == null || levelCompleteUI == null || coinsText == null)
                return;

            RectTransform targetRectTransform = coinsText.rectTransform;
            Canvas canvas = levelCompleteUI.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // Get world positions
            Vector3 startWorldPos = startRectTransform.position;
            Vector3 targetWorldPos = targetRectTransform.position;

            for (int i = 0; i < amount; i++)
            {
                // Instantiate coin
                GameObject coinInstance = Instantiate(coinsEarnedAnim, levelCompleteUI.transform);
                coinInstance.SetActive(true);

                RectTransform coinRect = coinInstance.GetComponent<RectTransform>();
                if (coinRect == null) continue;

                // Calculate spread offset
                float angle = (360f / amount) * i * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * spreadRadius;
                Vector3 spreadPosition = startWorldPos + offset;
                Vector3 scale = coinRect.transform.localScale;
                // coinRect.transform.localScale = Vector3.zero;

                // Set initial position at startWorldPos (no offset)
                coinRect.position = startWorldPos;

                // Add some random delay for staggered effect
                float delay = i * delayBetweenCoins + UnityEngine.Random.Range(0f, 0.1f);

                // First: Move from startWorldPos to spread position
                // Then: Continue to target position
#if MT_DOTWEEN
                Sequence coinSequence = DOTween.Sequence();
                coinSequence.AppendInterval(delay);
                coinSequence.Append(coinRect.DOMove(spreadPosition, animationDuration * 0.3f).SetEase(Ease.OutQuad)).Join(
                    coinRect.DOScale(scale, animationDuration * 0.3f).From(Vector3.zero)
                        .SetEase(Ease.OutQuad));
                coinSequence.Append(coinRect.DOMove(targetWorldPos, animationDuration).SetEase(Ease.InBack));
                coinSequence.OnComplete(() =>
                {
                    // Destroy coin after reaching target
                    if (coinInstance != null)
                        Destroy(coinInstance);
                });
#else
                // Fallback: simple instant move without tween
                coinRect.position = targetWorldPos;
                coinRect.localScale = scale;
                StartCoroutine(DestroyCoinAfterDelay(coinInstance, delay + animationDuration));
#endif
            }
        }

        private Color GetDifficultyColor(LevelDifficulty difficulty)
        {
            return difficulty switch
            {
                LevelDifficulty.Easy => new Color(0.2f, 0.8f, 0.2f),      // Green
                LevelDifficulty.Medium => new Color(1f, 0.8f, 0f),        // Yellow/Orange
                LevelDifficulty.Hard => new Color(1f, 0.2f, 0.2f),        // Red
                LevelDifficulty.VeryHard => new Color(0.8f, 0f, 0f),      // Dark Red
                _ => Color.white
            };
        }

#if !MT_DOTWEEN
        private System.Collections.IEnumerator DestroyCoinAfterDelay(GameObject coin, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (coin != null)
                Destroy(coin);
        }
#endif
    }
}
