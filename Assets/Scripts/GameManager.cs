using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("End Panel")]
    public GameObject endPanel;
    public Text resultText;
    public Button playAgainButton;
    public Button mainMenuButton;

    [Header("Camera")]
    public CameraFollow cameraFollow;

    [Header("Coins")]
    public Text coinsText;

    [Header("Level")]
    public GameObject islandsRoot;
    public GameObject islands3Root;
    public Text levelText;

    private bool gameOver = false;
    public bool IsGameOver => gameOver;
    private int playingLevel;
    private bool wonLastGame;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        playingLevel = MainMenu.levelToPlay >= 1
            ? MainMenu.levelToPlay
            : PlayerPrefs.GetInt("CurrentLevel", 1);
        MainMenu.levelToPlay = -1;

        if (islandsRoot  != null) islandsRoot.SetActive(playingLevel == 2);
        if (islands3Root != null) islands3Root.SetActive(playingLevel == 3);
    }

    void Start()
    {
        UpdateCoinsText();
        if (levelText != null)
            levelText.text = "LEVEL: " + playingLevel;
    }

    void UpdateCoinsText()
    {
        if (coinsText != null && CoinManager.Instance != null)
            coinsText.text = "COINS: " + CoinManager.Instance.Coins;
    }

    public void PlayerDefeated()
    {
        if (gameOver) return;
        gameOver = true;
        CoinManager.Instance?.AddCoins(1);
        UpdateCoinsText();
        ShowEndPanel("DEFEATED");
    }

    public void BotDefeated()
    {
        if (gameOver) return;
        gameOver = true;
        wonLastGame = true;
        CoinManager.Instance?.AddCoins(20);
        UpdateCoinsText();

        int stored = PlayerPrefs.GetInt("CurrentLevel", 1);
        if (stored < playingLevel + 1 && stored < 3)
        {
            PlayerPrefs.SetInt("CurrentLevel", playingLevel + 1);
            PlayerPrefs.Save();
        }

        ShowEndPanel("VICTORY!");
    }

    void ShowEndPanel(string message)
    {
        ShakeCamera(0.5f, 0.4f);
        if (resultText != null) resultText.text = message;
        if (endPanel != null) endPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ShakeCamera(float duration, float magnitude)
    {
        if (cameraFollow != null)
            StartCoroutine(cameraFollow.Shake(duration, magnitude));
    }

    public void PlayAgain()
    {
        Time.timeScale = 1f;
        MainMenu.levelToPlay = wonLastGame && playingLevel < 3 ? playingLevel + 1 : playingLevel;
        SceneManager.LoadScene("GameScene");
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
