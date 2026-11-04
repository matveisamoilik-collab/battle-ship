using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Shop loot box ("SEA CHEST"): costs 65 coins, unlimited purchases.
// Opening plays a case-roulette animation: a horizontal strip of reward tiles scrolls past a centre
// marker, decelerates and stops on the rolled reward, then the reward is revealed.
// Lives on the ShopPanel root (built by GameSetup.BuildShopPanel); the roulette overlay is built at
// runtime on first use because its tiles are generated per spin anyway.
public class LootBox : MonoBehaviour
{
    public enum DropKind { Coins, YellowShip, PiratShip }

    public class Drop
    {
        public readonly string   name;
        public readonly DropKind kind;
        public readonly int      coins;    // coin amount for Coins drops
        public readonly float    chance;   // percent
        public readonly Color    rarity;
        public Drop(string name, DropKind kind, int coins, float chance, Color rarity)
        { this.name = name; this.kind = kind; this.coins = coins; this.chance = chance; this.rarity = rarity; }
    }

    public const int Price = 65;

    // Free boxes (granted by the "100box" promo code) are spent before coins.
    public const string FreeBoxesKey = "FreeBoxes";
    public static int  FreeBoxes => PlayerPrefs.GetInt(FreeBoxesKey, 0);
    public static void AddFreeBoxes(int amount)
    {
        PlayerPrefs.SetInt(FreeBoxesKey, Mathf.Max(0, FreeBoxes + amount));
        PlayerPrefs.Save();
    }
    static bool CanOpen => FreeBoxes > 0 || (CoinManager.Instance != null && CoinManager.Instance.Coins >= Price);

    // Ships already owned are refunded at their sell price instead.
    public const int YellowDuplicateCoins = 75;
    public const int PiratDuplicateCoins  = 100;

    static readonly Color Common    = new Color(0.62f, 0.66f, 0.72f);
    static readonly Color Uncommon  = new Color(0.29f, 0.62f, 1.00f);
    static readonly Color Rare      = new Color(0.25f, 0.32f, 0.95f);
    static readonly Color Epic      = new Color(0.62f, 0.30f, 0.95f);
    static readonly Color Mythic    = new Color(0.95f, 0.30f, 0.75f);
    static readonly Color Legendary = new Color(1.00f, 0.75f, 0.15f);
    static readonly Color Arcana    = new Color(1.00f, 0.22f, 0.18f);

    // Chances must sum to 100.
    public static readonly Drop[] Drops =
    {
        new Drop("15 COINS",    DropKind.Coins,      15,   49.5f, Common),
        new Drop("50 COINS",    DropKind.Coins,      50,   16.5f, Uncommon),
        new Drop("100 COINS",   DropKind.Coins,      100,  12.5f, Rare),
        new Drop("YELLOW SHIP", DropKind.YellowShip, 0,    11.5f, Epic),
        new Drop("PIRAT SHIP",  DropKind.PiratShip,  0,    5.5f,  Mythic),
        new Drop("250 COINS",   DropKind.Coins,      250,  4f,    Legendary),
        new Drop("1000 COINS",  DropKind.Coins,      1000, 0.5f,  Arcana),
    };

    [Header("Wired by GameSetup")]
    public MainMenu menu;
    public Button   openButton;

    // Roulette tuning
    const int   TileCount   = 60;
    const int   WinnerIndex = 50;
    const float TileWidth   = 220f;
    const float TileGap     = 10f;
    const float SpinTime    = 6.5f;
    const float MinTickGap  = 0.045f;

    GameObject    overlay;
    RectTransform viewport;
    RectTransform strip;
    Text          resultText;
    GameObject    okButton;
    Button        againButton;
    Font          font;
    bool          spinning;
    Text          counterText;

    void OnEnable() { RefreshButton(); }

    public void RefreshButton()
    {
        int free = FreeBoxes;
        if (openButton != null)
        {
            openButton.interactable = !spinning && CanOpen;
            var label = openButton.GetComponentInChildren<Text>();
            if (label != null) label.text = free > 0 ? "OPEN (FREE)" : "OPEN";
        }
        EnsureCounter();
        if (counterText != null)
        {
            counterText.text  = "BOXES: " + free;
            counterText.color = free > 0 ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }
    }

    // Small "BOXES: N" badge in the top-right corner of the chest card (built at runtime — no Build All needed).
    void EnsureCounter()
    {
        if (counterText != null || openButton == null) return;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var badge = NewUI("FreeBoxCounter", openButton.transform.parent, new Vector2(0.62f, 0.73f), new Vector2(0.97f, 0.84f));
        badge.AddComponent<Image>().color = new Color(0.75f, 0.15f, 0.15f, 0.9f);
        badge.GetComponent<Image>().raycastTarget = false;
        counterText = NewText("Text", badge.transform, "", 20, Color.white, Vector2.zero, Vector2.one);
        counterText.fontStyle = FontStyle.Bold;
    }

    public void OnOpenClicked()
    {
        if (spinning || !CanOpen) return;
        StartCoroutine(OpenRoutine());
    }

    // ---- Rolling & rewards ---------------------------------------------------------------------

    static Drop Roll()
    {
        float r = Random.value * 100f, acc = 0f;
        foreach (var d in Drops)
        {
            acc += d.chance;
            if (r < acc) return d;
        }
        return Drops[0];
    }

    // Applies the reward immediately (so quitting mid-animation never loses it); returns the label to show
    // and the coins granted (shown on the counter only after the reveal).
    static string Grant(Drop drop, out int coinsGranted)
    {
        coinsGranted = 0;
        switch (drop.kind)
        {
            case DropKind.YellowShip:
                if (PlayerPrefs.GetInt("YellowShipOwned", 0) == 1) { coinsGranted = YellowDuplicateCoins; break; }
                PlayerPrefs.SetInt("YellowShipOwned", 1);
                PlayerPrefs.Save();
                return "YELLOW SHIP!";
            case DropKind.PiratShip:
                if (PlayerPrefs.GetInt("PiratShipOwned", 0) == 1) { coinsGranted = PiratDuplicateCoins; break; }
                PlayerPrefs.SetInt("PiratShipOwned", 1);
                PlayerPrefs.Save();
                return "PIRAT SHIP!";
            default:
                coinsGranted = drop.coins;
                CoinManager.Instance.AddCoins(coinsGranted, false);
                return "+" + coinsGranted + " COINS";
        }
        CoinManager.Instance.AddCoins(coinsGranted, false);
        return drop.name + " (OWNED) -> +" + coinsGranted + " COINS";
    }

    // ---- Animation -----------------------------------------------------------------------------

    IEnumerator OpenRoutine()
    {
        spinning = true;
        RefreshButton();

        if (FreeBoxes > 0) AddFreeBoxes(-1);
        else               CoinManager.Instance.AddCoins(-Price);
        Drop   win   = Roll();
        string label = Grant(win, out int granted);
        SetCoinsText(CoinManager.Instance.Coins - granted); // hide the prize until the reveal

        EnsureOverlay();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        resultText.text = "";
        okButton.SetActive(false);
        againButton.gameObject.SetActive(false);

        // Build a fresh strip
        for (int i = strip.childCount - 1; i >= 0; i--) Destroy(strip.GetChild(i).gameObject);
        RectTransform winnerTile = null;
        for (int i = 0; i < TileCount; i++)
        {
            var tile = BuildTile(i == WinnerIndex ? win : Roll(), i);
            if (i == WinnerIndex) winnerTile = tile;
        }

        Canvas.ForceUpdateCanvases();
        float step    = TileWidth + TileGap;
        float centre  = viewport.rect.width * 0.5f;
        float jitter  = Random.Range(-0.42f, 0.42f) * TileWidth; // stop somewhere inside the tile, not dead centre
        float startX  = centre - TileWidth * 0.5f - step * 2f;
        float endX    = centre - (WinnerIndex * step + TileWidth * 0.5f + jitter);

        int   lastIdx   = -1;
        float lastTick  = -1f;
        float t         = 0f;
        while (t < SpinTime)
        {
            t += Time.unscaledDeltaTime;
            float k    = Mathf.Clamp01(t / SpinTime);
            float ease = 1f - Mathf.Pow(1f - k, 4f);       // fast start, long slow-down
            float x    = Mathf.LerpUnclamped(startX, endX, ease);
            strip.anchoredPosition = new Vector2(x, 0f);

            int idx = Mathf.FloorToInt((centre - x) / step);
            if (idx != lastIdx && Time.unscaledTime - lastTick >= MinTickGap)
            {
                lastIdx  = idx;
                lastTick = Time.unscaledTime;
                SoundManager.Play("click", 0.45f, 0.03f, 1.35f);
            }
            yield return null;
        }
        strip.anchoredPosition = new Vector2(endX, 0f);

        // Reveal
        bool big = win.chance <= 10f || win.kind != DropKind.Coins;
        SoundManager.Play(big ? "victory" : "coins", big ? 0.8f : 0.9f, 0f);
        resultText.color = win.rarity;
        resultText.text  = "YOU GOT:  " + label;
        SetCoinsText(CoinManager.Instance.Coins);

        float p = 0f;
        while (p < 0.6f)
        {
            p += Time.unscaledDeltaTime;
            float s = 1f + 0.14f * Mathf.Sin(Mathf.Clamp01(p / 0.6f) * Mathf.PI);
            winnerTile.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        winnerTile.localScale = Vector3.one;

        spinning = false;
        okButton.SetActive(true);
        againButton.gameObject.SetActive(true);
        againButton.interactable = CanOpen;
        againButton.GetComponentInChildren<Text>().text =
            FreeBoxes > 0 ? "OPEN AGAIN (FREE)" : "OPEN AGAIN (" + Price + ")";
        if (menu != null) menu.RefreshShopUI();
        RefreshButton();
    }

    void SetCoinsText(int coins)
    {
        if (menu != null && menu.coinsText != null) menu.coinsText.text = "COINS: " + coins;
    }

    void CloseOverlay()
    {
        if (spinning) return;
        overlay.SetActive(false);
        if (menu != null) menu.RefreshShopUI();
        RefreshButton();
    }

    void OpenAgain()
    {
        if (spinning) return;
        OnOpenClicked();
    }

    // ---- Runtime UI ----------------------------------------------------------------------------

    void EnsureOverlay()
    {
        if (overlay != null) return;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        overlay = NewUI("LootBoxOverlay", transform, Vector2.zero, Vector2.one);
        overlay.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.06f, 0.97f); // also blocks clicks behind

        var title = NewText("Title", overlay.transform, "SEA CHEST", 44, Color.white,
                            new Vector2(0f, 0.80f), new Vector2(1f, 0.95f));
        title.fontStyle = FontStyle.Bold;

        var vpGO = NewUI("Viewport", overlay.transform, new Vector2(0.02f, 0.42f), new Vector2(0.98f, 0.76f));
        vpGO.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.11f, 1f);
        vpGO.AddComponent<RectMask2D>();
        viewport = vpGO.GetComponent<RectTransform>();

        var stripGO = new GameObject("Strip", typeof(RectTransform));
        stripGO.transform.SetParent(viewport, false);
        strip = stripGO.GetComponent<RectTransform>();
        strip.anchorMin = new Vector2(0f, 0f);
        strip.anchorMax = new Vector2(0f, 1f);
        strip.pivot     = new Vector2(0f, 0.5f);
        strip.sizeDelta = new Vector2(TileCount * (TileWidth + TileGap), 0f);

        // Centre marker: line + top/bottom pointers
        var line = NewUI("Marker", viewport, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
        line.GetComponent<RectTransform>().sizeDelta = new Vector2(6f, 0f);
        line.AddComponent<Image>().color = new Color(1f, 0.85f, 0.1f);
        foreach (float y in new[] { 1f, 0f })
        {
            var tri = NewUI("Pointer", viewport, new Vector2(0.5f, y), new Vector2(0.5f, y));
            var rt  = tri.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(30f, 30f);
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tri.AddComponent<Image>().color = new Color(1f, 0.85f, 0.1f);
        }

        resultText = NewText("Result", overlay.transform, "", 40, Color.white,
                             new Vector2(0f, 0.24f), new Vector2(1f, 0.38f));
        resultText.fontStyle = FontStyle.Bold;

        okButton = NewButton("OkButton", overlay.transform, "OK",
                             new Vector2(0.18f, 0.06f), new Vector2(0.48f, 0.19f), CloseOverlay);
        againButton = NewButton("OpenAgainButton", overlay.transform, "OPEN AGAIN (" + Price + ")",
                                new Vector2(0.52f, 0.06f), new Vector2(0.82f, 0.19f), OpenAgain)
                      .GetComponent<Button>();
    }

    RectTransform BuildTile(Drop drop, int index)
    {
        var go = new GameObject("Tile" + index, typeof(RectTransform));
        go.transform.SetParent(strip, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(TileWidth, -24f);
        rt.anchoredPosition = new Vector2(index * (TileWidth + TileGap) + TileWidth * 0.5f, 0f);
        go.AddComponent<Image>().color = Color.Lerp(new Color(0.13f, 0.13f, 0.18f), drop.rarity, 0.22f);

        // Rarity bar along the bottom edge
        NewUI("Rarity", go.transform, new Vector2(0f, 0f), new Vector2(1f, 0.07f))
            .AddComponent<Image>().color = drop.rarity;

        switch (drop.kind)
        {
            case DropKind.Coins:
            {
                var coin = NewUI("Coin", go.transform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f));
                var crt  = coin.GetComponent<RectTransform>();
                crt.sizeDelta     = new Vector2(84f, 84f);
                crt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                coin.AddComponent<Image>().color = new Color(1f, 0.82f, 0.15f);
                var amt = NewText("Amount", go.transform, drop.coins.ToString(), 30, new Color(0.25f, 0.15f, 0f),
                                  new Vector2(0f, 0.50f), new Vector2(1f, 0.74f));
                amt.fontStyle = FontStyle.Bold;
                break;
            }
            case DropKind.YellowShip:
                ShipIcon(go.transform, new Color(1f, 0.85f, 0f), new Color(0.85f, 0.70f, 0f), false);
                break;
            case DropKind.PiratShip:
                ShipIcon(go.transform, new Color(0.45f, 0.30f, 0.15f), new Color(0.30f, 0.20f, 0.10f), true);
                break;
        }

        NewText("Name", go.transform, drop.name, 22, Color.white, new Vector2(0f, 0.10f), new Vector2(1f, 0.30f))
            .fontStyle = FontStyle.Bold;
        return rt;
    }

    void ShipIcon(Transform parent, Color hull, Color cabin, bool sail)
    {
        NewUI("Hull", parent, new Vector2(0.12f, 0.40f), new Vector2(0.88f, 0.54f)).AddComponent<Image>().color = hull;
        NewUI("Cabin", parent, new Vector2(0.38f, 0.54f), new Vector2(0.64f, 0.66f)).AddComponent<Image>().color = cabin;
        if (sail)
        {
            NewUI("Mast", parent, new Vector2(0.49f, 0.54f), new Vector2(0.52f, 0.88f)).AddComponent<Image>().color = cabin;
            NewUI("Sail", parent, new Vector2(0.30f, 0.66f), new Vector2(0.70f, 0.84f)).AddComponent<Image>().color =
                new Color(0.95f, 0.92f, 0.85f);
        }
    }

    static GameObject NewUI(string name, Transform parent, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    Text NewText(string name, Transform parent, string text, int size, Color color, Vector2 aMin, Vector2 aMax)
    {
        var t = NewUI(name, parent, aMin, aMax).AddComponent<Text>();
        t.font          = font;
        t.text          = text;
        t.fontSize      = size;
        t.color         = color;
        t.alignment     = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    GameObject NewButton(string name, Transform parent, string label, Vector2 aMin, Vector2 aMax,
                         UnityEngine.Events.UnityAction onClick)
    {
        var go = NewUI(name, parent, aMin, aMax);
        go.AddComponent<Image>().color = new Color(0.2f, 0.4f, 0.8f, 1f);
        var btn = go.AddComponent<Button>();
        var c = btn.colors;
        c.highlightedColor = new Color(0.3f, 0.55f, 1f);
        c.pressedColor     = new Color(0.1f, 0.25f, 0.6f);
        btn.colors = c;
        btn.onClick.AddListener(onClick);
        btn.onClick.AddListener(() => SoundManager.Play("click", 0.8f, 0f)); // runtime buttons miss SoundManager's scene hook
        NewText("Text", go.transform, label, 28, Color.white, Vector2.zero, Vector2.one);
        return go;
    }
}
