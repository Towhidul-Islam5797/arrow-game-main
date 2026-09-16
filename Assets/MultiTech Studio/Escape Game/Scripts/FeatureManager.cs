using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace MultiTechStudio.EscapeGame
{
    public class FeatureManager : MonoBehaviour
    {
        private UiManager uiManager;
        [SerializeField] private Button hintButton;
        [SerializeField] private TextMeshProUGUI hintCountText;
        [SerializeField] private Button gridButton;
        [SerializeField] private TextMeshProUGUI gridCountText;

        [Header("Buy Hint UI")]
        [SerializeField] private GameObject buyHintUI;
        [SerializeField] private TextMeshProUGUI hintPriceText;
        [SerializeField] private Button buyHintButton;
        [SerializeField] private Button buyHintByAdsButton;
        [SerializeField] private Button closeBuyHintButton;
        [Header("Buy Grid UI")]
        [SerializeField] private GameObject buyGridUI;
        [SerializeField] private TextMeshProUGUI gridPriceText;
        [SerializeField] private Button buyGridButton;
        [SerializeField] private Button buyGridByAdsButton;
        [SerializeField] private Button closeBuyGridButton;
        [SerializeField] private int hintPrice = 50;
        [SerializeField] private int gridPrice = 50;

        private int hintCount = 0;
        private int gridCount = 0;

        private LevelState previousLevelState;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            uiManager = LevelManager.Instance.uiManager;

            hintCount = PlayerPrefs.GetInt("HintCount", 0);
            gridCount = PlayerPrefs.GetInt("GridCount", 0);

            hintCountText.text = hintCount.ToString();
            gridCountText.text = gridCount.ToString();

            hintPriceText.text = hintPrice.ToString();
            gridPriceText.text = gridPrice.ToString();

            hintButton?.onClick.AddListener(HintButtonClicked);
            gridButton?.onClick.AddListener(GridButtonClicked);

            buyHintButton?.onClick.AddListener(BuyHintButtonClicked);
            buyHintByAdsButton?.onClick.AddListener(BuyHintByAdsButtonClicked);
            closeBuyHintButton?.onClick.AddListener(CloseBuyHintUI);
            buyGridButton?.onClick.AddListener(BuyGridButtonClicked);
            buyGridByAdsButton?.onClick.AddListener(BuyGridByAdsButtonClicked);
            closeBuyGridButton?.onClick.AddListener(CloseBuyGridUI);
        }
        public bool CanShowHint()
        {
            return hintCount > 0;
        }
        public bool CanShowGrid()
        {
            return gridCount > 0;
        }
        private void AddHint()
        {
            hintCount++;
            PlayerPrefs.SetInt("HintCount", hintCount);
            PlayerPrefs.Save();
            hintCountText.text = hintCount.ToString();
        }
        private void AddGrid()
        {
            gridCount++;
            PlayerPrefs.SetInt("GridCount", gridCount);
            PlayerPrefs.Save();
            gridCountText.text = gridCount.ToString();
        }

        public void ShowHint()
        {
            Debug.Log("Show Hint");
            hintCount--;
            PlayerPrefs.SetInt("HintCount", hintCount);
            hintCountText.text = hintCount.ToString();
            LevelManager.Instance.FindLineCanBeRemove();
        }
        public void ShowGrid()
        {
            Debug.Log("Show Grid");
            gridCount--;
            PlayerPrefs.SetInt("GridCount", gridCount);
            gridCountText.text = gridCount.ToString();
            LevelManager.Instance.ShowGrid();
        }

        #region UI Things
        private void ShowBuyHintUI(int _hintOrGrid = 0)//0 for hint, 1 for grid
        {
            SoundManager.Instance.PlaySound("button");
            previousLevelState = LevelManager.Instance.levelState;
            LevelManager.Instance.levelState = LevelState.Paused;

            if (AdsManager.Instance.IsRewardVideoReady())
            {
                buyHintByAdsButton.interactable = true;
                buyGridByAdsButton.interactable = true;
            }
            else
            {
                buyHintByAdsButton.interactable = false;
                buyGridByAdsButton.interactable = false;
            }


            if (_hintOrGrid == 0)
            {
                if (buyHintUI != null) buyHintUI.SetActive(true);
            }
            else if (_hintOrGrid == 1)
            {
                if (buyGridUI != null) buyGridUI.SetActive(true);
            }
        }


        private void HintButtonClicked()
        {
            if (CanShowHint())
            {
                ShowHint();
            }
            else
            {
                ShowBuyHintUI(0);
            }
        }
        private void GridButtonClicked()
        {
            if (CanShowGrid())
            {
                ShowGrid();
            }
            else
            {
                ShowBuyHintUI(1);
            }
        }

        private void CloseBuyHintUI()
        {
            if (buyHintUI != null) buyHintUI.SetActive(false);
            SoundManager.Instance.PlaySound("close");
            LevelManager.Instance.levelState = previousLevelState;
        }
        private void CloseBuyGridUI()
        {
            if (buyGridUI != null) buyGridUI.SetActive(false);
            SoundManager.Instance.PlaySound("close");
            LevelManager.Instance.levelState = previousLevelState;
        }
        private void BuyHintButtonClicked()
        {
            SoundManager.Instance.PlaySound("button");
            if (uiManager.coins >= hintPrice)
            {
                uiManager.SpendCoins(hintPrice);
                AddHint();
                CloseBuyHintUI();
            }
            else
            {
                uiManager.OpenShopUI();
                CloseBuyHintUI();
            }
        }
        private void BuyHintByAdsButtonClicked()
        {
            AdsManager ads = AdsManager.Instance;
            if (ads == null || !ads.IsRewardVideoReady()) return;

            ads.ShowRewardedAd(() =>
                {
                    AddHint();
                    CloseBuyHintUI();
                });
        }
        private void BuyGridButtonClicked()
        {
            SoundManager.Instance.PlaySound("button");
            if (uiManager.coins >= gridPrice)
            {
                uiManager.SpendCoins(gridPrice);
                AddGrid();
                CloseBuyGridUI();
            }
            else
            {
                uiManager.OpenShopUI();
                CloseBuyGridUI();
            }
        }
        private void BuyGridByAdsButtonClicked()
        {
            AdsManager ads = AdsManager.Instance;
            if (ads == null || !ads.IsRewardVideoReady()) return;

            ads.ShowRewardedAd(() =>
                {
                    AddGrid();
                    CloseBuyGridUI();
                });
        }
        #endregion
    }
}
