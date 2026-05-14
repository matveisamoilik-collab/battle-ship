using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public Text coinsText;

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

    public InputField promoCodeInput;
    public Text       promoFeedbackText;

    void Start()
    {
        RefreshShopUI();
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
        var validCodes = new[] { "pizza1","pizza2","pizza3","pizza4","pizza5","pizza6","pizza7","pizza8" };

        bool isValid = System.Array.IndexOf(validCodes, code) >= 0;
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

        CoinManager.Instance?.AddCoins(5);
        PlayerPrefs.SetInt(prefsKey, 1);
        PlayerPrefs.Save();
        promoCodeInput.text     = "";
        promoFeedbackText.color = Color.green;
        promoFeedbackText.text  = "+5 COINS!";
        RefreshShopUI();
    }
}
