using UnityEngine;
using UnityEngine.UI;

// Globe icon in the top-right corner of the main menu (built at runtime — no Build All needed).
// Tapping it opens a dropdown with the available languages; tapping a language switches to it,
// tapping anywhere else closes the menu. The globe is drawn in code (no image asset).
public class LanguageSelector : MonoBehaviour
{
    // Shown in each language's own name, in the order of Loc.Lang.
    static readonly string[] Names = { "ENGLISH", "РУССКИЙ", "POLSKI", "VAĐĐA (BETA)" };
    static readonly string[] Codes = { "EN", "RU", "PL", "VOT" };

    static readonly Color ButtonColor = new Color(0.2f, 0.4f, 0.8f, 1f);
    static readonly Color MenuColor   = new Color(0.08f, 0.10f, 0.22f, 0.96f);
    static readonly Color RowColor    = new Color(0.14f, 0.18f, 0.36f, 1f);
    static readonly Color RowSelected = new Color(1f, 0.78f, 0.15f, 1f);

    const float IconSize = 84f;
    const float RowW     = 300f;
    const float RowH     = 64f;
    const float Pad      = 8f;

    GameObject blocker;
    GameObject menu;
    Image[]    rowBgs;
    Text[]     rowTexts;
    Text       codeBadge;
    Font       font;

    static Sprite s_globe;

    public static LanguageSelector Build(Transform canvas)
    {
        // Full-screen container without a graphic: it never blocks clicks itself.
        var root = NewUI("LanguageSelector", canvas, Vector2.zero, Vector2.one);
        var sel  = root.AddComponent<LanguageSelector>();
        sel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Invisible full-screen catcher behind the menu: a tap outside closes it.
        sel.blocker = NewUI("Blocker", root.transform, Vector2.zero, Vector2.one);
        var blockImg = sel.blocker.AddComponent<Image>();
        blockImg.color = new Color(0f, 0f, 0f, 0f);
        sel.blocker.AddComponent<Button>().onClick.AddListener(sel.Close);
        sel.blocker.SetActive(false);

        // Globe button
        var globe = NewUI("GlobeButton", root.transform, Vector2.one, Vector2.one);
        var grt   = globe.GetComponent<RectTransform>();
        grt.pivot            = Vector2.one;
        grt.sizeDelta        = new Vector2(IconSize, IconSize);
        grt.anchoredPosition = new Vector2(-20f, -20f);
        var gImg = globe.AddComponent<Image>();
        gImg.sprite         = GlobeSprite();
        gImg.preserveAspect = true;
        var gBtn = globe.AddComponent<Button>();
        var gc = gBtn.colors;
        gc.highlightedColor = new Color(0.85f, 0.92f, 1f);
        gc.pressedColor     = new Color(0.65f, 0.75f, 0.9f);
        gBtn.colors = gc;
        gBtn.onClick.AddListener(() => { SoundManager.Play("click", 0.8f, 0f); sel.Toggle(); });

        // Current language code, small badge at the globe's bottom-left
        var badge = NewUI("Code", globe.transform, new Vector2(0f, 0f), new Vector2(0f, 0f));
        var brt = badge.GetComponent<RectTransform>();
        brt.pivot            = new Vector2(0.5f, 0.5f);
        brt.sizeDelta        = new Vector2(58f, 30f);
        brt.anchoredPosition = new Vector2(8f, 10f);
        var bImg = badge.AddComponent<Image>();
        bImg.color         = RowSelected;
        bImg.raycastTarget = false;
        sel.codeBadge = sel.NewText(badge.transform, "", 20, Color.black);
        sel.codeBadge.fontStyle = FontStyle.Bold;

        // Dropdown menu under the globe
        int n = Names.Length;
        sel.menu = NewUI("LanguageMenu", root.transform, Vector2.one, Vector2.one);
        var mrt = sel.menu.GetComponent<RectTransform>();
        mrt.pivot            = Vector2.one;
        mrt.sizeDelta        = new Vector2(RowW + 2f * Pad, n * RowH + (n + 1) * Pad);
        mrt.anchoredPosition = new Vector2(-20f, -20f - IconSize - 8f);
        sel.menu.AddComponent<Image>().color = MenuColor;

        sel.rowBgs   = new Image[n];
        sel.rowTexts = new Text[n];
        for (int i = 0; i < n; i++)
        {
            var row = NewUI("Lang_" + Codes[i], sel.menu.transform, new Vector2(0f, 1f), new Vector2(0f, 1f));
            var rrt = row.GetComponent<RectTransform>();
            rrt.pivot            = new Vector2(0f, 1f);
            rrt.sizeDelta        = new Vector2(RowW, RowH);
            rrt.anchoredPosition = new Vector2(Pad, -Pad - i * (RowH + Pad));
            sel.rowBgs[i] = row.AddComponent<Image>();
            var lang = (Loc.Lang)i;
            row.AddComponent<Button>().onClick.AddListener(() =>
            {
                SoundManager.Play("click", 0.8f, 0f);
                Loc.Set(lang);
                sel.Close();
            });
            var t = sel.NewText(row.transform, Names[i], 28, Color.white);
            t.fontStyle = FontStyle.Bold;
            sel.rowTexts[i] = t;
        }
        sel.menu.SetActive(false);

        sel.Refresh();
        return sel;
    }

    void OnEnable()  { Loc.Changed += Refresh; }
    void OnDisable() { Loc.Changed -= Refresh; }

    void Toggle()
    {
        if (menu.activeSelf) Close();
        else
        {
            Refresh();
            blocker.SetActive(true);
            menu.SetActive(true);
        }
    }

    void Close()
    {
        if (menu    != null) menu.SetActive(false);
        if (blocker != null) blocker.SetActive(false);
    }

    void Refresh()
    {
        if (rowBgs == null) return;
        int cur = (int)Loc.Current;
        for (int i = 0; i < rowBgs.Length; i++)
        {
            bool on = i == cur;
            rowBgs[i].color   = on ? RowSelected : RowColor;
            rowTexts[i].color = on ? Color.black : Color.white;
            rowTexts[i].text  = (on ? "✓ " : "") + Names[i];
        }
        if (codeBadge != null) codeBadge.text = Codes[cur];
    }

    // ---- UI helpers ----

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

    Text NewText(Transform parent, string text, int size, Color color)
    {
        var t = NewUI("Text", parent, Vector2.zero, Vector2.one).AddComponent<Text>();
        t.font          = font;
        t.text          = text;
        t.fontSize      = size;
        t.color         = color;
        t.alignment     = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        return t;
    }

    // ---- Procedural globe icon: blue disc, white rim, equator, two parallels and three meridians ----

    static Sprite GlobeSprite()
    {
        if (s_globe != null) return s_globe;
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px  = new Color[S * S];
        float R = S * 0.5f - 2f;           // outer radius (rim outer edge)
        float r = R - 7f;                  // inner globe radius
        const float line = 2.6f;           // half line width in pixels
        Color fill = ButtonColor, white = Color.white;

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx = x + 0.5f - S * 0.5f, dy = y + 0.5f - S * 0.5f;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);
            float outer = Mathf.Clamp01(R - d + 0.5f);            // anti-aliased disc edge
            if (outer <= 0f) { px[y * S + x] = Color.clear; continue; }

            float ink = Mathf.Clamp01(line + 1.5f - Mathf.Abs(d - (r + 3.5f)));   // rim
            if (d < r)
            {
                ink = Mathf.Max(ink, Coverage(Mathf.Abs(dy), line));                       // equator
                float py = r * 0.52f;                                                      // parallels
                ink = Mathf.Max(ink, Coverage(Mathf.Abs(Mathf.Abs(dy) - py), line));
                ink = Mathf.Max(ink, Coverage(Mathf.Abs(dx), line));                       // central meridian
                float ex = r * 0.5f;                                                       // side meridians (ellipse)
                float e  = Mathf.Sqrt((dx * dx) / (ex * ex) + (dy * dy) / (r * r));
                ink = Mathf.Max(ink, Coverage(Mathf.Abs(e - 1f) * ex, line));
            }
            var c = Color.Lerp(fill, white, ink);
            c.a = outer;
            px[y * S + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply();
        s_globe = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return s_globe;
    }

    static float Coverage(float dist, float halfWidth) => Mathf.Clamp01(halfWidth + 0.5f - dist);
}
