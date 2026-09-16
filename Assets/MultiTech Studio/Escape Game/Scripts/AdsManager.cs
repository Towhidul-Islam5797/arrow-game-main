using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

#if MT_ADMOB
using GoogleMobileAds.Api;
#endif

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Centralises ad loading, caching, and display logic for supported providers.
    /// </summary>
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance;
        [SerializeField] private bool enableDebugLogging = false;

        [Header("Project Setup")]
        [Tooltip("Optional: Assign ProjectSetupData to use centralized Ads configuration")]
        public ProjectSetupData projectSetupData;

        private string noAdsPurchase = "NoAdsPurchase";

#if MT_ADMOB
        BannerView bannerView;
        InterstitialAd interstitialAd;
        RewardedAd rewardedAd;
        RewardedInterstitialAd rewardedInterstitialAd;
#endif

        // These are loaded from ProjectSetupData at runtime
        private string bannerId;
        private string interId;
        private string rewardedInterId;
        private string rewardedId;
        private int interstitialLevelThreshold;
        private bool canShowInterstitialAfterLevel;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            // Load all configuration from ProjectSetupData (required)
            if (projectSetupData != null)
            {
                bannerId = projectSetupData.bannerId;
                interId = projectSetupData.interId;
                rewardedInterId = projectSetupData.rewardedInterId;
                rewardedId = projectSetupData.rewardedId;
                interstitialLevelThreshold = projectSetupData.interstitialLevelThreshold;
                canShowInterstitialAfterLevel = projectSetupData.canShowInterstitialAfterLevel;
            }
            else
            {
                Debug.LogError("[AdsManager] ProjectSetupData is not assigned! Please assign a ProjectSetupData ScriptableObject to AdsManager.");
            }

#if MT_ADMOB
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.Initialize(initStatus =>
            {
                Log("Ads initialised.");
                LoadInterstitialAd();
                // LoadRewardedInterstitialAd();
                LoadRewardedAd();
            });
#endif
            SetNoAdsPurchase(PlayerPrefs.GetInt(noAdsPurchase, 0) == 1);
        }

        public void SetNoAdsPurchase(bool isNoAds)
        {
            PlayerPrefs.SetInt(noAdsPurchase, isNoAds ? 1 : 0);
            LevelManager.Instance.uiManager.SetAdsButtonActive(!isNoAds);
        }

        public bool CanShowInterstitialAfterLevel(int levelIndex)
        {
            if (PlayerPrefs.GetInt(noAdsPurchase, 0) == 1)
                return false;
#if MT_ADMOB
            return levelIndex >= interstitialLevelThreshold && canShowInterstitialAfterLevel && IsInterstitialReady();
#else
            return false;
#endif
        }

        #region Banner

        public void LoadBannerAd()
        {
            if (PlayerPrefs.GetInt(noAdsPurchase, 0) == 1) return;
#if MT_ADMOB
            //create a banner
            CreateBannerView();

            //listen to banner events
            ListenToBannerEvents();

            //load the banner
            if (bannerView == null)
            {
                CreateBannerView();
            }

            var adRequest = new AdRequest();
            adRequest.Keywords.Add("unity-admob-sample");

            bannerView.LoadAd(adRequest);//show the banner on the screen
#endif
        }
        void CreateBannerView()
        {
#if MT_ADMOB
            if (bannerView != null)
            {
                DestroyBannerAd();
            }
            bannerView = new BannerView(bannerId, AdSize.Banner, AdPosition.Bottom);
#endif
        }
        void ListenToBannerEvents()
        {
#if MT_ADMOB
            bannerView.OnBannerAdLoaded += () =>
            {
                Log($"Banner view loaded. Response: {bannerView.GetResponseInfo()}");
            };
            // Raised when an ad fails to load into the banner view.
            bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
            {
                Log($"Banner view failed to load an ad with error: {error}");
            };
            // Raised when the ad is estimated to have earned money.
            bannerView.OnAdPaid += (AdValue adValue) =>
            {
                Log($"Banner view paid {adValue.Value} {adValue.CurrencyCode}.");
            };
            // Raised when an impression is recorded for an ad.
            bannerView.OnAdImpressionRecorded += () =>
            {
                Log("Banner view recorded an impression.");
            };
            // Raised when a click is recorded for an ad.
            bannerView.OnAdClicked += () =>
            {
                Log("Banner view was clicked.");
            };
            // Raised when an ad opened full screen content.
            bannerView.OnAdFullScreenContentOpened += () =>
            {
                Log("Banner view full screen content opened.");
            };
            // Raised when the ad closed full screen content.
            bannerView.OnAdFullScreenContentClosed += () =>
            {
                Log("Banner view full screen content closed.");
            };
#endif
        }
        public void DestroyBannerAd()
        {
#if MT_ADMOB
            if (bannerView != null)
            {
                Log("Destroying banner ad.");
                bannerView.Destroy();
                bannerView = null;
            }
#endif
        }
        #endregion

        #region Interstitial

        public void LoadInterstitialAd()
        {
#if MT_ADMOB
            if (interstitialAd != null)
            {
                interstitialAd.Destroy();
                interstitialAd = null;
            }
            var adRequest = new AdRequest();

            InterstitialAd.Load(interId, adRequest, (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Log($"Interstitial ad failed to load: {error}");
                    return;
                }

                Log($"Interstitial ad loaded. Response: {ad.GetResponseInfo()}");

                interstitialAd = ad;
                InterstitialEvent(interstitialAd);
            });
#endif
        }
        public void ShowInterstitialAd()
        {
            if (PlayerPrefs.GetInt(noAdsPurchase, 0) == 1) return;
#if MT_ADMOB
            if (interstitialAd != null && interstitialAd.CanShowAd())
            {
                interstitialAd.Show();
            }
            else
            {
                Log("Interstitial ad not ready. Requesting reload.");
                LoadInterstitialAd();
            }
#endif
        }
#if MT_ADMOB
        public void InterstitialEvent(InterstitialAd ad)
        {
            // Raised when the ad is estimated to have earned money.
            ad.OnAdPaid += (AdValue adValue) =>
            {
                InterstitialCompleted();
                Log($"Interstitial ad paid {adValue.Value} {adValue.CurrencyCode}.");
            };
            // Raised when an impression is recorded for an ad.
            ad.OnAdImpressionRecorded += () =>
            {
                Log("Interstitial ad recorded an impression.");
            };
            // Raised when a click is recorded for an ad.
            ad.OnAdClicked += () =>
            {
                Log("Interstitial ad was clicked.");
            };
            // Raised when an ad opened full screen content.
            ad.OnAdFullScreenContentOpened += () =>
            {
                Log("Interstitial ad full screen content opened.");
            };
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                InterstitialCompleted();
                Log("Interstitial ad full screen content closed.");
            };
            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Log($"Interstitial ad failed to open full screen content with error: {error}");
            };
        }
#endif

        public bool IsInterstitialReady()
        {
#if MT_ADMOB
            if (interstitialAd != null && interstitialAd.CanShowAd())
            {
                return true;
            }

            LoadInterstitialAd();
            return false;
#else
            return false;
#endif
        }

        private void InterstitialCompleted()
        {
#if MT_ADMOB
            LoadInterstitialAd();
#endif
        }
        #endregion

        #region Rewarded Interstitial
        public void LoadRewardedInterstitialAd()
        {
            if (PlayerPrefs.GetInt(noAdsPurchase, 0) == 1) return;
#if MT_ADMOB
            // Clean up the old ad before loading a new one.
            if (rewardedInterstitialAd != null)
            {
                rewardedInterstitialAd.Destroy();
                rewardedInterstitialAd = null;
            }

            Log("Loading rewarded interstitial ad.");

            // create our request used to load the ad.
            var adRequest = new AdRequest();
            adRequest.Keywords.Add("unity-admob-sample");

            // send the request to load the ad.
            RewardedInterstitialAd.Load(rewardedInterId, adRequest,
                (RewardedInterstitialAd ad, LoadAdError error) =>
                {
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        Log($"Rewarded interstitial ad failed to load with error: {error}");
                        return;
                    }

                    Log($"Rewarded interstitial ad loaded. Response: {ad.GetResponseInfo()}");

                    rewardedInterstitialAd = ad;
                    RegisterReloadHandler(rewardedInterstitialAd);
                });
#endif
        }

        public void ShowRewardedInterstitialAd(UnityAction adEnd)
        {
            if (PlayerPrefs.GetInt(noAdsPurchase, 0) == 1) return;
#if MT_ADMOB
            const string rewardMsg =
                "Rewarded interstitial ad rewarded the user. Type: {0}, amount: {1}.";

            if (rewardedInterstitialAd != null && rewardedInterstitialAd.CanShowAd())
            {
                rewardedInterstitialAd.Show((Reward reward) =>
                {
                    adEnd.Invoke();
                    Log(string.Format(rewardMsg, reward.Type, reward.Amount));
                });
            }
#endif
        }

        public bool IsRewardedInterstitialReady()
        {
#if MT_ADMOB
            if (rewardedInterstitialAd != null && rewardedInterstitialAd.CanShowAd())
            {
                return true;
            }

            LoadRewardedInterstitialAd();
            return false;
#else
            return false;
#endif
        }

#if MT_ADMOB
        private void RegisterReloadHandler(RewardedInterstitialAd ad)
        {
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                Log("Rewarded interstitial ad full screen content closed.");

                // Reload the ad so that we can show another as soon as possible.
                LoadRewardedInterstitialAd();
            };
            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Log($"Rewarded interstitial ad failed to open full screen content with error: {error}");

                // Reload the ad so that we can show another as soon as possible.
                LoadRewardedInterstitialAd();
            };
        }
#endif
        #endregion

        #region Rewarded

        public void LoadRewardedAd()
        {
#if MT_ADMOB
            if (rewardedAd != null)
            {
                rewardedAd.Destroy();
                rewardedAd = null;
            }
            var adRequest = new AdRequest();

            RewardedAd.Load(rewardedId, adRequest, (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Log($"Rewarded ad failed to load: {error}");
                    return;
                }

                Log("Rewarded ad loaded.");
                rewardedAd = ad;
                RewardedAdEvents(rewardedAd);
            });
#endif
        }
        public void ShowRewardedAd(System.Action adViewed)
        {
#if MT_ADMOB
            if (rewardedAd != null && rewardedAd.CanShowAd())
            {
                rewardedAd.Show((Reward reward) =>
                {
                    Log("Reward granted to player.");
                    adViewed.Invoke();
                    LoadRewardedAd();
                });
            }
            else
            {
                Log("Rewarded ad not ready. Requesting reload.");
                LoadRewardedAd();
            }
#endif
        }
#if MT_ADMOB
        public void RewardedAdEvents(RewardedAd ad)
        {
            // Raised when the ad is estimated to have earned money.
            ad.OnAdPaid += (AdValue adValue) =>
            {
                Log($"Rewarded ad paid {adValue.Value} {adValue.CurrencyCode}.");
            };
            // Raised when an impression is recorded for an ad.
            ad.OnAdImpressionRecorded += () =>
            {
                Log("Rewarded ad recorded an impression.");
            };
            // Raised when a click is recorded for an ad.
            ad.OnAdClicked += () =>
            {
                Log("Rewarded ad was clicked.");
            };
            // Raised when an ad opened full screen content.
            ad.OnAdFullScreenContentOpened += () =>
            {
                Log("Rewarded ad full screen content opened.");
            };
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                Log("Rewarded ad full screen content closed.");
            };
            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Log($"Rewarded ad failed to open full screen content with error: {error}");
            };
        }
#endif

        public bool IsRewardVideoReady()
        {
#if MT_ADMOB
            if (rewardedAd != null && rewardedAd.CanShowAd())
            {
                return true;
            }

            LoadRewardedAd();
            return false;
#else
            return false;
#endif
        }

        #endregion

        // [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void Log(string message)
        {
            if (!enableDebugLogging) return;
            UnityEngine.Debug.Log($"[AdsManager] {message}");
        }
    }
}

