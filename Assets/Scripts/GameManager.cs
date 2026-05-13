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

    private bool gameOver = false;
    public bool IsGameOver => gameOver;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        UpdateCoinsText();
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
        CoinManager.Instance?.AddCoins(20);
        UpdateCoinsText();
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
        SceneManager.LoadScene("GameScene");
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
