using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

#if MT_IAP
using UnityEngine.Purchasing;
using Unity.Services.Core;
#endif

namespace MultiTechStudio.EscapeGame
{

    public enum IapThing
    {
        NoAds, PremiumBundle, MegaBundle, CoinBag, Coin3, Coin2, Coin1
    }

    // [Obsolete]
    public class IapManager : MonoBehaviour
#if MT_IAP
        , IDetailedStoreListener
#endif
    {
#if MT_IAP
        IStoreController m_StoreController; // The Unity Purchasing system.
#endif

        [Header("Project Setup")]
        [Tooltip("Optional: Assign ProjectSetupData to use centralized IAP configuration")]
        public ProjectSetupData projectSetupData;

        [System.Serializable]
        public class IapDetails
        {
            public IapThing iapThing;
            public string id;
#if MT_IAP
            public ProductType productType;
#else
            public int productType; // Placeholder when IAP is not available
#endif
            public Button Btn;
            public Button[] BtnSecondary;
            public TextMeshProUGUI PriceText;
            public int rewardPrice;
        }

        [Header("IAP Configuration")]
        public IapDetails[] allIaps;

        // Start is called before the first frame update
        void Start()
        {
            // Load IAP data from ProjectSetupData if available
            if (projectSetupData != null && projectSetupData.iapProducts != null && projectSetupData.iapProducts.Count > 0)
            {
                LoadIapDataFromProjectSetup();
            }

            Invoke(nameof(InitializeIAP), 1f);
        }

        private void LoadIapDataFromProjectSetup()
        {
            if (projectSetupData == null || projectSetupData.iapProducts == null) return;

            // Convert ProjectSetupData.IapProductData to IapDetails
            List<IapDetails> iapDetailsList = new List<IapDetails>();

            foreach (var productData in projectSetupData.iapProducts)
            {
                IapDetails details = new IapDetails
                {
                    iapThing = productData.iapThing,
                    id = productData.id,
                    productType = productData.productType,
                    rewardPrice = productData.rewardPrice,
                    // Btn and PriceText need to be assigned manually in the inspector
                    // They are UI references that can't be stored in ScriptableObject
                    Btn = null,
                    BtnSecondary = null,
                    PriceText = null
                };
                iapDetailsList.Add(details);
            }

            // Merge with existing allIaps if they exist (preserve UI references)
            if (allIaps != null && allIaps.Length > 0)
            {
                // Try to match by ID and preserve UI references
                for (int i = 0; i < iapDetailsList.Count; i++)
                {
                    var existing = System.Array.Find(allIaps, iap => iap.iapThing == iapDetailsList[i].iapThing);
                    if (existing != null)
                    {
                        iapDetailsList[i].Btn = existing.Btn;
                        iapDetailsList[i].BtnSecondary = existing.BtnSecondary;
                        iapDetailsList[i].PriceText = existing.PriceText;
                    }
                }
            }

            allIaps = iapDetailsList.ToArray();
        }
        private void SetButtonDetails()
        {
#if MT_IAP
            for (int i = 0; i < allIaps.Length; i++)
            {
                int _no = i;
                Product cproduct = m_StoreController.products.WithID(allIaps[i].id);
                if (cproduct != null)
                {
                    if (allIaps[i].PriceText != null && cproduct.metadata.localizedPriceString != null)
                        allIaps[i].PriceText.text = cproduct.metadata.localizedPriceString.ToString();
                    allIaps[i].Btn.onClick.AddListener(() => BtnIapPurchase(_no));
                    foreach (var btn in allIaps[i].BtnSecondary)
                        btn.onClick.AddListener(() => BtnIapPurchase(_no));
                }
            }
#endif
        }

        public void BtnIapPurchase(int _no)
        {
#if MT_IAP
            if (m_StoreController != null)
            {
                m_StoreController.InitiatePurchase(allIaps[_no].id);
            }
#endif
        }

        private void BuyProduct(int _no)
        {
#if MT_IAP
            if (allIaps[_no].productType == ProductType.Consumable || (allIaps[_no].productType == ProductType.NonConsumable && PlayerPrefs.GetInt(allIaps[_no].id.ToString(), 0) == 0))
            {
                switch (allIaps[_no].iapThing)
                {
                    case IapThing.Coin1:
                    case IapThing.Coin2:
                    case IapThing.Coin3:
                    case IapThing.CoinBag:
                        LevelManager.Instance.uiManager.AddCoins(allIaps[_no].rewardPrice);
                        break;
                    case IapThing.PremiumBundle:
                    case IapThing.MegaBundle:
                        LevelManager.Instance.uiManager.AddCoins(allIaps[_no].rewardPrice);
                        AdsManager.Instance.SetNoAdsPurchase(true);
                        break;
                    case IapThing.NoAds:
                        AdsManager.Instance.SetNoAdsPurchase(true);
                        break;
                }

            }
#endif
        }

        private void RestorePurchase()
        {
#if MT_IAP
            if (m_StoreController != null)
            {
                // IAP v5: The deprecated IAppleExtensions API is no longer available
                // Restored purchases will automatically come through ProcessPurchase when IAP initializes
                // Check for existing purchases after a short delay to allow initialization to complete
                StartCoroutine(CheckRestoredPurchases());
            }
#endif
        }

        private System.Collections.IEnumerator CheckRestoredPurchases()
        {
#if MT_IAP
            // Wait a moment for IAP initialization and automatic restoration to complete
            yield return new WaitForSeconds(0.5f);

            // Note: In IAP v5, restored purchases automatically come through ProcessPurchase
            // This method is kept for backward compatibility, but restored purchases should
            // be handled automatically via ProcessPurchase when IAP initializes
            // 
            // For non-consumables, we rely on ProcessPurchase to handle restored purchases.
            // The BuyProduct method already checks PlayerPrefs to prevent duplicate grants.
            Debug.Log("Purchase restoration check completed. Restored purchases will be processed via ProcessPurchase.");
#else
            yield return null;
#endif
        }

        private void InitializeIAP()
        {
#if MT_IAP
            Debug.Log("IAP init");
            InitializeUGS();
            InitializePurchasing();
#endif
        }
#if MT_IAP
        async void InitializeUGS()
        {
            try
            {
                var options = new InitializationOptions();

                await UnityServices.InitializeAsync(options);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Error initializing UGS: {exception}");
                // An error occurred during initialization.
            }
        }
        public void InitializePurchasing()
        {
            // Note: ConfigurationBuilder and StandardPurchasingModule are deprecated in IAP v5
            // but still required when using the legacy IStoreListener API pattern.
            // A full migration to IAP v5 would require switching to the new event-driven API.
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            //Add products that will be purchasable and indicate its type.
            for (int i = 0; i < allIaps.Length; i++)
            {
                builder.AddProduct(allIaps[i].id, allIaps[i].productType);
            }

            UnityPurchasing.Initialize(this, builder);
        }

        void IDetailedStoreListener.OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            Debug.Log($"Purchase failed - Product: '{product.definition.id}', PurchaseFailureReason: {failureDescription}");
        }

        [Obsolete]
        void IStoreListener.OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.Log($"In-App Purchasing initialize failed: {error}");
        }

        [Obsolete]
        void IStoreListener.OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.Log($"In-App Purchasing initialize failed: {error}");
        }

        [Obsolete]
        PurchaseProcessingResult IStoreListener.ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            //Retrieve the purchased product
            var product = purchaseEvent.purchasedProduct;

            //Add the purchased product to the players inventory
            for (int i = 0; i < allIaps.Length; i++)
            {
                if (product.definition.id == allIaps[i].id)
                {
                    BuyProduct(i);
                    break;
                }
            }

            Debug.Log($"Purchase Complete - Product: {product.definition.id}");

            //We return Complete, informing IAP that the processing on our side is done and the transaction can be closed.
            return PurchaseProcessingResult.Complete;
        }

        [Obsolete]
        void IStoreListener.OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Debug.Log($"Purchase failed - Product: '{product.definition.id}', PurchaseFailureReason: {failureReason}");
        }

        [Obsolete]
        void IStoreListener.OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            Debug.Log("In-App Purchasing successfully initialized");
            m_StoreController = controller;
            RestorePurchase();
            SetButtonDetails();
        }
#endif
    }
}