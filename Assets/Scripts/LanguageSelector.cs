using UnityEngine;
using UnityEngine.UI;

// EN / RU / PL buttons in the top-right corner of the main menu (built at runtime — no Build All needed).
public class LanguageSelector : MonoBehaviour
{
    static readonly string[] Labels = { "EN", "RU", "PL" };
    static readonly Color Normal   = new Color(0.2f, 0.4f, 0.8f, 1f);
    static readonly Color Selected = new Color(1f, 0.78f, 0.15f, 1f);

    Image[] backgrounds;
    Text[]  texts;

    public static LanguageSelector Build(Transform canvas)
    {
        var root = new GameObject("LanguageSelector", typeof(RectTransform));
        root.transform.SetParent(canvas, false);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-20f, -20f);
        rt.sizeDelta        = new Vector2(3 * 100f + 2 * 10f, 64f);

        var sel = root.AddComponent<LanguageSelector>();
        sel.backgrounds = new Image[Labels.Length];
        sel.texts       = new Text[Labels.Length];
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        for (int i = 0; i < Labels.Length; i++)
        {
            var btnGO = new GameObject("Lang_" + Labels[i], typeof(RectTransform));
            btnGO.transform.SetParent(root.transform, false);
            var brt = btnGO.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 0.5f);
            brt.sizeDelta        = new Vector2(100f, 64f);
            brt.anchoredPosition = new Vector2(i * 110f, 0f);
            var img = btnGO.AddComponent<Image>();
            var btn = btnGO.AddComponent<Button>();
            var lang = (Loc.Lang)i;
            btn.onClick.AddListener(() => { SoundManager.Play("click", 0.8f, 0f); Loc.Set(lang); });

            var txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.transform.SetParent(btnGO.transform, false);
            var trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var txt = txtGO.AddComponent<Text>();
            txt.font          = font;
            txt.text          = Labels[i];
            txt.fontSize      = 28;
            txt.fontStyle     = FontStyle.Bold;
            txt.alignment     = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;

            sel.backgrounds[i] = img;
            sel.texts[i]       = txt;
        }
        sel.Refresh();
        return sel;
    }

    void OnEnable()  { Loc.Changed += Refresh; }
    void OnDisable() { Loc.Changed -= Refresh; }

    void Refresh()
    {
        if (backgrounds == null) return;
        for (int i = 0; i < backgrounds.Length; i++)
        {
            bool on = (int)Loc.Current == i;
            backgrounds[i].color = on ? Selected : Normal;
            texts[i].color       = on ? Color.black : Color.white;
        }
    }
}
