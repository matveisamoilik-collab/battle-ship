using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public Text coinsText;
    public Text levelText;

    public GameObject shopPanel;
    public GameObject toBuyContent;
    public GameObject boughtContent;

    public GameObject yellowShipToBuyCard;
    public GameObject yellowShipBoughtCard;
    public Button     buyYellowShipButton;

    public Button blueShipSelectButton;
    public Text   blueShipSelectedLabel;
    public Button yellowShipSelectButton;
    public Text   yellowShipSelectedLabel;
    public Button yellowShipSellButton;

    public GameObject yellowRedShipBoughtCard;
    public Button     yellowRedShipSelectButton;
    public Text       yellowRedShipSelectedLabel;

    public InputField promoCodeInput;
    public Text       promoFeedbackText;

    // Map panel
    public static int levelToPlay = -1;
    public GameObject mapPanel;
    public Image      mapImage;
    public Button     island2Button;

    private static readonly string[] s_validPromoCodes =
        { "pizza1","pizza2","pizza3","pizza4","pizza5","pizza6","pizza7","pizza8" };
    private const int PromoCodeReward = 5;

    void Start()
    {
        levelToPlay = -1;
        RefreshShopUI();
        if (levelText != null)
            levelText.text = "LEVEL: " + PlayerPrefs.GetInt("CurrentLevel", 1);
    }

    public void OnPlayClicked()
    {
        SceneManager.LoadScene("GameScene");
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnShopClicked()
    {
        if (shopPanel != null) shopPanel.SetActive(true);
        RefreshShopUI();
        ShowBoughtTab();
    }

    public void OnCloseShopClicked()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    public void ShowToBuyTab()
    {
        if (toBuyContent  != null) toBuyContent.SetActive(true);
        if (boughtContent != null) boughtContent.SetActive(false);
    }

    public void ShowBoughtTab()
    {
        if (toBuyContent  != null) toBuyContent.SetActive(false);
        if (boughtContent != null) boughtContent.SetActive(true);
    }

    public void RefreshShopUI()
    {
        bool yellowOwned = PlayerPrefs.GetInt("YellowShipOwned", 0) == 1;

        if (yellowShipToBuyCard  != null) yellowShipToBuyCard.SetActive(!yellowOwned);
        if (yellowShipBoughtCard != null) yellowShipBoughtCard.SetActive(yellowOwned);

        if (coinsText != null && CoinManager.Instance != null)
            coinsText.text = "COINS: " + CoinManager.Instance.Coins;

        if (buyYellowShipButton != null)
            buyYellowShipButton.interactable =
                !yellowOwned &&
                CoinManager.Instance != null &&
                CoinManager.Instance.Coins >= 150;

        string selected     = PlayerPrefs.GetString("SelectedShip", "blue");
        bool blueSelected   = selected == "blue";
        bool yellowSelected = selected == "yellow";

        if (blueShipSelectButton   != null) blueShipSelectButton.gameObject.SetActive(!blueSelected);
        if (blueShipSelectedLabel  != null) blueShipSelectedLabel.gameObject.SetActive(blueSelected);
        if (yellowShipSelectButton  != null) yellowShipSelectButton.gameObject.SetActive(yellowOwned && !yellowSelected);
        if (yellowShipSellButton    != null) yellowShipSellButton.gameObject.SetActive(yellowOwned && !yellowSelected);
        if (yellowShipSelectedLabel != null) yellowShipSelectedLabel.gameObject.SetActive(yellowOwned && yellowSelected);

        bool yellowRedOwned    = PlayerPrefs.GetInt("YellowRedShipOwned", 0) == 1;
        bool yellowRedSelected = selected == "yellowred";

        if (yellowRedShipBoughtCard    != null) yellowRedShipBoughtCard.SetActive(yellowRedOwned);
        if (yellowRedShipSelectButton  != null) yellowRedShipSelectButton.gameObject.SetActive(yellowRedOwned && !yellowRedSelected);
        if (yellowRedShipSelectedLabel != null) yellowRedShipSelectedLabel.gameObject.SetActive(yellowRedOwned && yellowRedSelected);
    }

    public void OnSelectBlueShip()
    {
        PlayerPrefs.SetString("SelectedShip", "blue");
        PlayerPrefs.Save();
        RefreshShopUI();
    }

    public void OnSelectYellowShip()
    {
        PlayerPrefs.SetString("SelectedShip", "yellow");
        PlayerPrefs.Save();
        RefreshShopUI();
    }

    public void OnSelectYellowRedShip()
    {
        PlayerPrefs.SetString("SelectedShip", "yellowred");
        PlayerPrefs.Save();
        RefreshShopUI();
    }

    public void OnSellYellowShipClicked()
    {
        if (PlayerPrefs.GetString("SelectedShip", "blue") == "yellow") return;

        CoinManager.Instance?.AddCoins(75);
        PlayerPrefs.SetInt("YellowShipOwned", 0);
        PlayerPrefs.Save();

        RefreshShopUI();
        ShowToBuyTab();
    }

    public void OnBuyYellowShipClicked()
    {
        if (CoinManager.Instance == null || CoinManager.Instance.Coins < 150) return;

        CoinManager.Instance.AddCoins(-150);
        PlayerPrefs.SetInt("YellowShipOwned", 1);
        PlayerPrefs.Save();

        RefreshShopUI();
        ShowBoughtTab();
    }

    public void OnRedeemPromoCode()
    {
        if (promoCodeInput == null || promoFeedbackText == null) return;

        string code = promoCodeInput.text.Trim().ToLower();

        if (code == "resett")
        {
            int refunded = 0;
            bool pizza1WasReset = false;
            foreach (var c in s_validPromoCodes)
            {
                string key = "Promo_" + c;
                if (PlayerPrefs.GetInt(key, 0) == 1)
                {
                    CoinManager.Instance?.AddCoins(-PromoCodeReward);
                    PlayerPrefs.SetInt(key, 0);
                    refunded++;
                    if (c == "pizza1") pizza1WasReset = true;
                }
            }
            if (pizza1WasReset)
            {
                PlayerPrefs.SetInt("YellowRedShipOwned", 0);
                if (PlayerPrefs.GetString("SelectedShip", "blue") == "yellowred")
                    PlayerPrefs.SetString("SelectedShip", "blue");
            }
            PlayerPrefs.Save();
            promoCodeInput.text     = "";
            promoFeedbackText.color = Color.cyan;
            promoFeedbackText.text  = refunded > 0 ? $"Reset {refunded} code(s)." : "Nothing to reset.";
            RefreshShopUI();
            return;
        }

        bool isValid = System.Array.IndexOf(s_validPromoCodes, code) >= 0;
        if (!isValid)
        {
            promoFeedbackText.color = Color.red;
            promoFeedbackText.text  = "Invalid code.";
            return;
        }

        string prefsKey = "Promo_" + code;
        if (PlayerPrefs.GetInt(prefsKey, 0) == 1)
        {
            promoFeedbackText.color = new Color(1f, 0.6f, 0f);
            promoFeedbackText.text  = "Already used.";
            return;
        }

        CoinManager.Instance?.AddCoins(PromoCodeReward);
        PlayerPrefs.SetInt(prefsKey, 1);
        PlayerPrefs.Save();
        if (code == "pizza1")
        {
            PlayerPrefs.SetInt("YellowRedShipOwned", 1);
            PlayerPrefs.Save();
            promoCodeInput.text     = "";
            promoFeedbackText.color = Color.green;
            promoFeedbackText.text  = $"+{PromoCodeReward} COINS + SHIP!";
            RefreshShopUI();
            return;
        }
        promoCodeInput.text     = "";
        promoFeedbackText.color = Color.green;
        promoFeedbackText.text  = $"+{PromoCodeReward} COINS!";
        RefreshShopUI();
    }

    public void OnMapClicked()
    {
        RefreshMapImage();
        if (island2Button != null)
            island2Button.interactable = PlayerPrefs.GetInt("CurrentLevel", 1) >= 2;
        if (mapPanel != null) mapPanel.SetActive(true);
    }

    public void OnCloseMapClicked()
    {
        if (mapPanel != null) mapPanel.SetActive(false);
    }

    public void OnIsland1Clicked()
    {
        levelToPlay = 1;
        SceneManager.LoadScene("GameScene");
    }

    public void OnIsland2Clicked()
    {
        levelToPlay = 2;
        SceneManager.LoadScene("GameScene");
    }

    void RefreshMapImage()
    {
        if (mapImage == null) return;
        string spriteName = PlayerPrefs.GetInt("CurrentLevel", 1) >= 2 ? "Level_2" : "Level_1";
        var sprite = Resources.Load<Sprite>(spriteName);
        if (sprite != null) mapImage.sprite = sprite;
    }
}
