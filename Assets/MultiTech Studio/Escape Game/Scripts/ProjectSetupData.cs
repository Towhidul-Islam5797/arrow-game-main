using System.Collections.Generic;
using UnityEngine;

#if MT_IAP
using UnityEngine.Purchasing;
#endif

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// ScriptableObject that stores project configuration for IAP and Ads
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectSetupData", menuName = "Tools/MultiTech Studio/Escape Game Project Setup")]
    public class ProjectSetupData : ScriptableObject
    {
        [Header("IAP Configuration")]
        [Tooltip("List of IAP products with their details")]
        public List<IapProductData> iapProducts = new List<IapProductData>();

        [Header("Ads Configuration")]
        [Tooltip("Banner Ad Unit ID")]
        public string bannerId = "ca-app-pub-3940256099942544/6300978111";
        
        [Tooltip("Interstitial Ad Unit ID")]
        public string interId = "ca-app-pub-3940256099942544/1033173712";
        
        [Tooltip("Rewarded Interstitial Ad Unit ID")]
        public string rewardedInterId = "ca-app-pub-3940256099942544/8691691433";
        
        [Tooltip("Rewarded Ad Unit ID")]
        public string rewardedId = "ca-app-pub-3940256099942544/5224354917";

        [Header("Interstitial Settings")]
        [Tooltip("Minimum level number before interstitial ads can be shown")]
        public int interstitialLevelThreshold = 3;
        
        [Tooltip("Enable/disable interstitial ads after the threshold level")]
        public bool canShowInterstitialAfterLevel = true;

        /// <summary>
        /// IAP Product Data - stores the configurable fields from IapDetails
        /// </summary>
        [System.Serializable]
        public class IapProductData
        {
            [Tooltip("Type of IAP product")]
            public IapThing iapThing;
            
            [Tooltip("Product ID (must match store configuration)")]
            public string id;
            
#if MT_IAP
            [Tooltip("Product type (Consumable, NonConsumable, Subscription)")]
            public ProductType productType;
#else
            [Tooltip("Product type (0=Consumable, 1=NonConsumable, 2=Subscription)")]
            public int productType; // Placeholder when IAP is not available
#endif
            
            [Tooltip("Reward price/amount for this IAP")]
            public int rewardPrice;
        }
    }
}

