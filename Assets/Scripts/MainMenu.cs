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

    public GameObject whiteShipBoughtCard;
    public Button     whiteShipSelectButton;
    public Text       whiteShipSelectedLabel;

    public GameObject piratShipToBuyCard;
    public GameObject piratShipBoughtCard;
    public Button     buyPiratShipButton;
    public Button     piratShipSelectButton;
    public Text       piratShipSelectedLabel;
    public Button     piratShipSellButton;

    public InputField promoCodeInput;
    public Text       promoFeedbackText;

    public GameObject statsContent;

    // Map panel
    public static int levelToPlay = -1;
    public GameObject mapPanel;
    public Image      mapImage;
    public Button     island2Button;
    public Button     island3Button;
    public Button     island4Button;

    private static readonly string[] s_validPromoCodes =
        { "pizza1","pizza2","pizza3","pizza4","pizza5","pizza6","pizza7","pizza8","godship" };
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
        if (statsContent  != null) statsContent.SetActive(false);
    }

    public void ShowBoughtTab()
    {
        if (toBuyContent  != null) toBuyContent.SetActive(false);
        if (boughtContent != null) boughtContent.SetActive(true);
        if (statsContent  != null) statsContent.SetActive(false);
    }

    public void ShowStatsTab()
    {
        if (toBuyContent  != null) toBuyContent.SetActive(false);
        if (boughtContent != null) boughtContent.SetActive(false);
        if (statsContent  != null) statsContent.SetActive(true);
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

        bool whiteOwned    = PlayerPrefs.GetInt("WhiteShipOwned", 0) == 1;
        bool whiteSelected = selected == "white";

        if (whiteShipBoughtCard    != null) whiteShipBoughtCard.SetActive(whiteOwned);
        if (whiteShipSelectButton  != null) whiteShipSelectButton.gameObject.SetActive(whiteOwned && !whiteSelected);
        if (whiteShipSelectedLabel != null) whiteShipSelectedLabel.gameObject.SetActive(whiteOwned && whiteSelected);

        bool piratOwned    = PlayerPrefs.GetInt("PiratShipOwned", 0) == 1;
        bool piratSelected = selected == "pirat";

        if (piratShipToBuyCard  != null) piratShipToBuyCard.SetActive(!piratOwned);
        if (piratShipBoughtCard != null) piratShipBoughtCard.SetActive(piratOwned);
        if (buyPiratShipButton  != null)
            buyPiratShipButton.interactable =
                !piratOwned &&
                CoinManager.Instance != null &&
                CoinManager.Instance.Coins >= 200;
        if (piratShipSelectButton  != null) piratShipSelectButton.gameObject.SetActive(piratOwned && !piratSelected);
        if (piratShipSellButton    != null) piratShipSellButton.gameObject.SetActive(piratOwned && !piratSelected);
        if (piratShipSelectedLabel != null) piratShipSelectedLabel.gameObject.SetActive(piratOwned && piratSelected);
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

    public void OnSelectWhiteShip()
    {
        PlayerPrefs.SetString("SelectedShip", "white");
        PlayerPrefs.Save();
        RefreshShopUI();
    }

    public void OnSelectPiratShip()
    {
        PlayerPrefs.SetString("SelectedShip", "pirat");
        PlayerPrefs.Save();
        RefreshShopUI();
    }

    public void OnBuyPiratShipClicked()
    {
        if (CoinManager.Instance == null || CoinManager.Instance.Coins < 200) return;
        CoinManager.Instance.AddCoins(-200);
        PlayerPrefs.SetInt("PiratShipOwned", 1);
        PlayerPrefs.Save();
        RefreshShopUI();
        ShowBoughtTab();
    }

    public void OnSellPiratShipClicked()
    {
        if (PlayerPrefs.GetString("SelectedShip", "blue") == "pirat") return;
        CoinManager.Instance?.AddCoins(100);
        PlayerPrefs.SetInt("PiratShipOwned", 0);
        PlayerPrefs.Save();
        RefreshShopUI();
        ShowToBuyTab();
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
            bool pizza1WasReset   = false;
            bool godshipWasReset  = false;
            foreach (var c in s_validPromoCodes)
            {
                string key = "Promo_" + c;
                if (PlayerPrefs.GetInt(key, 0) == 1)
                {
                    CoinManager.Instance?.AddCoins(-PromoCodeReward);
                    PlayerPrefs.SetInt(key, 0);
                    refunded++;
                    if (c == "pizza1")  pizza1WasReset  = true;
                    if (c == "godship") godshipWasReset = true;
                }
            }
            if (pizza1WasReset)
            {
                PlayerPrefs.SetInt("YellowRedShipOwned", 0);
                if (PlayerPrefs.GetString("SelectedShip", "blue") == "yellowred")
                    PlayerPrefs.SetString("SelectedShip", "blue");
            }
            if (godshipWasReset)
            {
                PlayerPrefs.SetInt("WhiteShipOwned", 0);
                if (PlayerPrefs.GetString("SelectedShip", "blue") == "white")
                    PlayerPrefs.SetString("SelectedShip", "blue");
            }
            if (PlayerPrefs.GetInt("PiratShipOwned", 0) == 1)
            {
                CoinManager.Instance?.AddCoins(-200);
                PlayerPrefs.SetInt("PiratShipOwned", 0);
                if (PlayerPrefs.GetString("SelectedShip", "blue") == "pirat")
                    PlayerPrefs.SetString("SelectedShip", "blue");
                refunded++;
            }
            PlayerPrefs.Save();
            promoCodeInput.text     = "";
            promoFeedbackText.color = Color.cyan;
            promoFeedbackText.text  = refunded > 0 ? $"Reset {refunded} code(s)." : "Nothing to reset.";
            RefreshShopUI();
            return;
        }

        if (code == "resetl")
        {
            PlayerPrefs.SetInt("CurrentLevel", 1);
            PlayerPrefs.Save();
            promoCodeInput.text     = "";
            promoFeedbackText.color = Color.cyan;
            promoFeedbackText.text  = "Level reset to 1.";
            if (levelText != null)
                levelText.text = "LEVEL: 1";
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
        if (code == "godship")
        {
            PlayerPrefs.SetInt("WhiteShipOwned", 1);
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
        int lvl = PlayerPrefs.GetInt("CurrentLevel", 1);
        if (island2Button != null) island2Button.interactable = lvl >= 2;
        if (island3Button != null) island3Button.interactable = lvl >= 3;
        if (island4Button != null) island4Button.interactable = lvl >= 4;
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

    public void OnIsland3Clicked()
    {
        levelToPlay = 3;
        SceneManager.LoadScene("GameScene");
    }

    public void OnIsland4Clicked()
    {
        levelToPlay = 4;
        SceneManager.LoadScene("GameScene");
    }

    void RefreshMapImage()
    {
        if (mapImage == null) return;
        int lvl = PlayerPrefs.GetInt("CurrentLevel", 1);
        string spriteName = lvl >= 4 ? "Level_4"
                          : lvl >= 3 ? "Level_3"
                          : lvl >= 2 ? "Level_2"
                                     : "Level_1";
        var sprite = Resources.Load<Sprite>(spriteName);
        if (sprite != null) mapImage.sprite = sprite;
    }
}
