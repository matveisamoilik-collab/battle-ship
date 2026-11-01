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
    public GameObject islands4Root;
    public GameObject islands5Root;
    public Text levelText;

    [Header("Level 4")]
    public Text       timerText;
    public GameObject botHPGroup;
    public GameObject botShipPrefab;
    public Material   cloudySkybox;
    public GameObject cloudsRoot;
    public Light      directionalLight;

    public  const int   MaxLevel         = 5;
    private const float SurvivalDuration = 90f;
    private const float SpawnInterval    = 18f;

    private bool  gameOver = false;
    public  bool  IsGameOver  => gameOver;
    public  int   PlayingLevel => playingLevel;
    private int   playingLevel;
    private bool  wonLastGame;
    private float survivalTimer;
    private float spawnTimer;
    private int   purpleAlive;     // Level 5: purple ships still afloat after the red ship sinks
    private bool  purpleSpawned;   // Level 5: red ship already split into purples

    private const int   PurpleSplitCount  = 2;
    private const float PurpleSplitOffset = 4f; // sideways offset from the red ship's wreck

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
        if (islands4Root != null) islands4Root.SetActive(playingLevel == 4);
        if (islands5Root != null) islands5Root.SetActive(playingLevel == 5);
        if (cloudsRoot   != null) cloudsRoot.SetActive(playingLevel == 4);
    }

    void Start()
    {
        UpdateCoinsText();
        if (levelText != null)
            levelText.text = "LEVEL: " + playingLevel;

        if (playingLevel == 4)
        {
            if (cloudySkybox != null)
            {
                RenderSettings.skybox = cloudySkybox;
                DynamicGI.UpdateEnvironment();
            }
            if (directionalLight != null)
            {
                directionalLight.intensity = 0.5f;
                directionalLight.color     = new Color(0.75f, 0.78f, 0.85f); // cool gray overcast
            }
            survivalTimer = SurvivalDuration;
            spawnTimer    = SpawnInterval;
            if (botHPGroup != null) botHPGroup.SetActive(false);
            if (timerText  != null)
            {
                timerText.gameObject.SetActive(true);
                UpdateTimerUI();
            }
        }
        else
        {
            if (timerText != null) timerText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (playingLevel != 4 || gameOver) return;

        survivalTimer -= Time.deltaTime;
        spawnTimer    -= Time.deltaTime;
        UpdateTimerUI();

        if (survivalTimer <= 0f)
        {
            // Destroy all remaining bots before showing victory
            foreach (var bot in FindObjectsByType<BotShip>(FindObjectsSortMode.None))
                if (bot != null) Destroy(bot.gameObject);
            TriggerVictory();
            return;
        }

        if (spawnTimer <= 0f)
        {
            SpawnBlackShip();
            spawnTimer = SpawnInterval;
        }
    }

    void UpdateTimerUI()
    {
        if (timerText == null) return;
        int secs = Mathf.CeilToInt(Mathf.Max(0f, survivalTimer));
        timerText.text = string.Format("{0}:{1:00}", secs / 60, secs % 60);
    }

    void SpawnBlackShip()
    {
        if (botShipPrefab == null) return;
        Vector3 pos   = GetSpawnPosition();
        float   angle = Mathf.Atan2(pos.x, pos.z) * Mathf.Rad2Deg + 180f; // bow toward center
        Instantiate(botShipPrefab, pos, Quaternion.Euler(0f, angle, 0f));
    }

    Vector3 GetSpawnPosition()
    {
        GameObject playerGO = GameObject.FindWithTag("Player");
        Vector3    playerPos = playerGO != null ? new Vector3(playerGO.transform.position.x, 0f, playerGO.transform.position.z) : Vector3.zero;

        Vector3 pos = Vector3.zero;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float a    = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(65f, 82f);
            pos = new Vector3(Mathf.Sin(a) * dist, 0f, Mathf.Cos(a) * dist);
            pos.x = Mathf.Clamp(pos.x, -90f, 90f);
            pos.z = Mathf.Clamp(pos.z, -90f, 90f);
            if (Vector3.Distance(pos, playerPos) >= 30f) break;
        }
        return pos;
    }

    void TriggerVictory()
    {
        if (gameOver) return;
        gameOver    = true;
        wonLastGame = true;
        CoinManager.Instance?.AddCoins(20);
        UpdateCoinsText();

        int stored = PlayerPrefs.GetInt("CurrentLevel", 1);
        if (stored < playingLevel + 1 && stored < MaxLevel)
        {
            PlayerPrefs.SetInt("CurrentLevel", playingLevel + 1);
            PlayerPrefs.Save();
        }

        ShowEndPanel("VICTORY!");
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

    public void BotDefeated(BotShip bot)
    {
        if (playingLevel == 4)
        {
            // In Level 4 bots respawn — just remove the destroyed ship
            if (bot != null) Destroy(bot.gameObject);
            return;
        }

        if (playingLevel == 5 && bot != null)
        {
            if (bot.IsPurple)
            {
                Destroy(bot.gameObject);
                purpleAlive--;
                if (purpleAlive <= 0) TriggerVictory();
                return;
            }
            if (!purpleSpawned)
            {
                // Red ship sinks and splits into 2 purple ships at the same spot
                purpleSpawned = true;
                Vector3    pos = bot.transform.position;
                Quaternion rot = bot.transform.rotation;
                Vector3    right = bot.transform.right;
                Destroy(bot.gameObject);
                SpawnPurpleShips(pos, rot, right);
                if (purpleAlive > 0) return;
            }
        }
        TriggerVictory();
    }

    void SpawnPurpleShips(Vector3 pos, Quaternion rot, Vector3 right)
    {
        if (botShipPrefab == null) return;
        ShakeCamera(0.3f, 0.3f);
        if (botHPGroup != null) botHPGroup.SetActive(false);
        right.y = 0f;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        for (int i = 0; i < PurpleSplitCount; i++)
        {
            float   side     = i == 0 ? -1f : 1f;
            Vector3 spawnPos = pos + right * side * PurpleSplitOffset;
            spawnPos.y = 0f;
            var go  = Instantiate(botShipPrefab, spawnPos, rot);
            var bs  = go.GetComponent<BotShip>();
            if (bs != null) bs.spawnAsPurple = true; // read in BotShip.Start(), which runs next frame
            purpleAlive++;
        }
    }

    void ShowEndPanel(string message)
    {
        ShakeCamera(0.5f, 0.4f);
        if (resultText != null) resultText.text = message;
        if (endPanel   != null) endPanel.SetActive(true);
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
        MainMenu.levelToPlay = wonLastGame && playingLevel < MaxLevel ? playingLevel + 1 : playingLevel;
        SceneManager.LoadScene("GameScene");
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
