using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Game localization: English (source), Russian, Polish, Votic (draft — see AddVotic).
//
// Keys are the English strings themselves. Scene texts built by GameSetup keep their English text;
// on every scene load (and on every language switch) all Text components whose text matches a key
// in ANY language are rewritten in the current language — so no Build All is needed and no
// per-Text component. Texts that code sets at runtime go through Loc.T(key) / Loc.F(formatKey, args)
// and are refreshed by their owners on Loc.Changed.
//
// The selected language is stored in PlayerPrefs "Language" ("en" / "ru" / "pl" / "vot"); on first launch
// it follows the device language (Russian / Belarusian / Ukrainian → ru, Polish → pl, else en).
public static class Loc
{
    public enum Lang { EN = 0, RU = 1, PL = 2, VO = 3 }

    public const string PrefsKey = "Language";
    static readonly string[] Codes = { "en", "ru", "pl", "vot" };

    public static Lang Current { get; private set; } = Lang.EN;
    public static event Action Changed;

    // key (English) → { ru, pl, vot }  (a missing vot entry falls back to English)
    static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>();
    // any language's text → key (literal keys only, not format strings)
    static readonly Dictionary<string, string> reverse = new Dictionary<string, string>();

    static Loc()
    {
        // ---- Main menu ----
        Add("SEA BATTLE", "МОРСКОЙ БОЙ", "BITWA MORSKA");
        Add("PLAY", "ИГРАТЬ", "GRAJ");
        Add("MAP", "КАРТА", "MAPA");
        Add("SHOP", "МАГАЗИН", "SKLEP");
        Add("QUIT", "ВЫХОД", "WYJDŹ");

        // ---- Shop ----
        Add("TO BUY", "В ПРОДАЖЕ", "DO KUPIENIA");
        Add("BOUGHT", "КУПЛЕНО", "KUPIONE");
        Add("STATS", "ХАРАКТЕРИСТИКИ", "STATYSTYKI");
        Add("LOOT BOX", "СУНДУК", "SKRZYNIA");
        Add("BUY", "КУПИТЬ", "KUP");
        Add("SELECT", "ВЫБРАТЬ", "WYBIERZ");
        Add("SELL", "ПРОДАТЬ", "SPRZEDAJ");
        Add("✓ SELECTED", "✓ ВЫБРАН", "✓ WYBRANY");
        Add("BLUE SHIP", "СИНИЙ КОРАБЛЬ", "NIEBIESKI STATEK");
        Add("YELLOW SHIP", "ЖЁЛТЫЙ КОРАБЛЬ", "ŻÓŁTY STATEK");
        Add("YELLOW-RED SHIP", "ЖЁЛТО-КРАСНЫЙ КОРАБЛЬ", "ŻÓŁTO-CZERWONY STATEK");
        Add("WHITE SHIP", "БЕЛЫЙ КОРАБЛЬ", "BIAŁY STATEK");
        Add("PIRAT SHIP", "ПИРАТСКИЙ КОРАБЛЬ", "STATEK PIRACKI");
        Add("SHARK SHIP", "КОРАБЛЬ-АКУЛА", "STATEK REKIN");
        Add("PROMO CODE", "ПРОМОКОД", "KOD PROMOCYJNY");
        Add("Enter code...", "Введите код...", "Wpisz kod...");
        Add("REDEEM", "АКТИВИРОВАТЬ", "AKTYWUJ");

        // Stats table
        Add("SHIP", "КОРАБЛЬ", "STATEK");
        Add("SIZE", "РАЗМЕР", "ROZMIAR");
        Add("HP", "ПРОЧН.", "WYTRZ.");
        Add("SPD", "СКОР.", "PRĘDK.");
        Add("DMG", "УРОН", "OBR.");
        Add("DELAY", "ПЕРЕЗАР.", "PRZEŁAD.");
        Add("BLUE", "СИНИЙ", "NIEBIESKI");
        Add("YELLOW", "ЖЁЛТЫЙ", "ŻÓŁTY");
        Add("YELLOW-RED", "ЖЁЛТО-КРАСНЫЙ", "ŻÓŁTO-CZERWONY");
        Add("PIRATE", "ПИРАТСКИЙ", "PIRACKI");
        Add("SHARK", "АКУЛА", "REKIN");
        Add("BLACK (BOT)", "ЧЁРНЫЙ (БОТ)", "CZARNY (BOT)");

        // Loot box
        Add("SEA CHEST", "МОРСКОЙ СУНДУК", "MORSKA SKRZYNIA");
        Add("OPEN", "ОТКРЫТЬ", "OTWÓRZ");
        Add("OPEN (FREE)", "ОТКРЫТЬ (БЕСПЛАТНО)", "OTWÓRZ (ZA DARMO)");
        Add("OPEN AGAIN (FREE)", "ЕЩЁ РАЗ (БЕСПЛАТНО)", "JESZCZE RAZ (ZA DARMO)");
        Add("OPEN AGAIN ({0})", "ЕЩЁ РАЗ ({0})", "JESZCZE RAZ ({0})");
        Add("POSSIBLE DROPS", "ВОЗМОЖНЫЕ НАГРАДЫ", "MOŻLIWE NAGRODY");
        Add("BOXES: {0}", "СУНДУКИ: {0}", "SKRZYNIE: {0}");
        Add("YOU GOT:  {0}", "ВЫПАЛО:  {0}", "ZDOBYTO:  {0}");
        Add("{0} (OWNED) -> +{1} COINS", "{0} (УЖЕ ЕСТЬ) -> +{1} МОНЕТ", "{0} (POSIADANY) -> +{1} MONET");
        Add("OK", "ОК", "OK");

        // Coins (every amount used is 0 or ends in 0/5 → genitive plural in both RU and PL)
        Add("COINS: {0}", "МОНЕТЫ: {0}", "MONETY: {0}");
        Add("{0} COINS", "{0} МОНЕТ", "{0} MONET");
        Add("+{0} COINS", "+{0} МОНЕТ", "+{0} MONET");
        foreach (int n in new[] { 10, 50, 65, 100, 150, 200, 250, 1000 })
            Add(n + " COINS", n + " МОНЕТ", n + " MONET");

        // Promo codes
        Add("Reset {0} code(s).", "Сброшено кодов: {0}.", "Zresetowano kody: {0}.");
        Add("Nothing to reset.", "Нечего сбрасывать.", "Nie ma czego resetować.");
        Add("Level reset to 1.", "Уровень сброшен до 1.", "Poziom zresetowany do 1.");
        Add("Already used.", "Уже использован.", "Kod już użyty.");
        Add("Invalid code.", "Неверный код.", "Nieprawidłowy kod.");
        Add("+{0} FREE LOOT BOXES!", "+{0} БЕСПЛАТНЫХ СУНДУКОВ!", "+{0} DARMOWYCH SKRZYŃ!");
        Add("+{0} COINS + SHIP!", "+{0} МОНЕТ + КОРАБЛЬ!", "+{0} MONET + STATEK!");
        Add("+{0} COINS!", "+{0} МОНЕТ!", "+{0} MONET!");

        // ---- Game HUD ----
        Add("LEVEL: {0}", "УРОВЕНЬ: {0}", "POZIOM: {0}");
        Add("LEVEL: 1", "УРОВЕНЬ: 1", "POZIOM: 1");
        Add("YOUR SHIP", "ВАШ КОРАБЛЬ", "TWÓJ STATEK");
        Add("ENEMY SHIP", "КОРАБЛЬ ВРАГА", "STATEK WROGA");
        Add("VICTORY!", "ПОБЕДА!", "ZWYCIĘSTWO!");
        Add("DEFEATED", "ПОРАЖЕНИЕ", "PORAŻKA");
        Add("PLAY AGAIN", "ИГРАТЬ СНОВА", "ZAGRAJ PONOWNIE");
        Add("MAIN MENU", "ГЛАВНОЕ МЕНЮ", "MENU GŁÓWNE");
        Add("PILLARS LEFT: {0}", "ОСТАЛОСЬ КОЛОНН: {0}", "POZOSTAŁE FILARY: {0}");
        Add("PILLARS LEFT: 3", "ОСТАЛОСЬ КОЛОНН: 3", "POZOSTAŁE FILARY: 3");

        // ---- Level 7: Kraken's Throne ----
        Add("KRAKEN", "КРАКЕН", "KRAKEN");
        Add("SUNFISH", "РЫБА-ЛУНА", "SAMOGŁÓW");
        Add("KRAKEN KING", "КОРОЛЬ КРАКЕНОВ", "KRÓL KRAKENÓW");
        Add("PHASE 1 / 2", "ФАЗА 1 / 2", "FAZA 1 / 2");
        Add("PHASE 2 / 2", "ФАЗА 2 / 2", "FAZA 2 / 2");
        Add("KRAKEN'S THRONE", "ТРОН КРАКЕНА", "TRON KRAKENA");
        Add("PHASE 1 · DODGE THE TENTACLES  {0}:{1:00}", "ФАЗА 1 · УВОРАЧИВАЙТЕСЬ ОТ ЩУПАЛЕЦ  {0}:{1:00}", "FAZA 1 · UNIKAJ MACEK  {0}:{1:00}");
        Add("THE TENTACLES SINK...", "ЩУПАЛЬЦА УХОДЯТ ПОД ВОДУ...", "MACKI ZNIKAJĄ POD WODĄ...");
        Add("PHASE 1 · ESCAPE THE WHIRLPOOL!", "ФАЗА 1 · ВЫРВИТЕСЬ ИЗ ВОДОВОРОТА!", "FAZA 1 · UCIEKNIJ Z WIRU!");
        Add("PHASE 1 · THE KRAKEN IS COMING - GET AWAY!", "ФАЗА 1 · КРАКЕН ВСПЛЫВАЕТ - УПЛЫВАЙТЕ!", "FAZA 1 · KRAKEN NADCHODZI - UCIEKAJ!");
        Add("PHASE 1 · ATTACK THE KRAKEN!", "ФАЗА 1 · АТАКУЙТЕ КРАКЕНА!", "FAZA 1 · ATAKUJ KRAKENA!");
        Add("THE KRAKEN DIVES...", "КРАКЕН НЫРЯЕТ...", "KRAKEN NURKUJE...");
        Add("THE KRAKEN SINKS...", "КРАКЕН ТОНЕТ...", "KRAKEN TONIE...");
        Add("PHASE 2 · SINK THE SUNFISH", "ФАЗА 2 · ПОБЕДИТЕ РЫБУ-ЛУНУ", "FAZA 2 · POKONAJ SAMOGŁOWA");
        Add("PHASE 2 · THE KRAKEN KING RISES!", "ФАЗА 2 · КОРОЛЬ КРАКЕНОВ ВОССТАЁТ!", "FAZA 2 · KRÓL KRAKENÓW SIĘ WYNURZA!");
        Add("PHASE 2 · SINK THE JELLYFISH ({0} LEFT) - THE KING IS SHIELDED",
            "ФАЗА 2 · УНИЧТОЖЬТЕ МЕДУЗ (ОСТАЛОСЬ: {0}) - КОРОЛЬ ПОД ЩИТОМ",
            "FAZA 2 · ZNISZCZ MEDUZY (POZOSTAŁO: {0}) - KRÓL MA TARCZĘ");
        Add("PHASE 2 · SECOND WAVE! SINK THE JELLYFISH ({0} LEFT) - THE KING IS SHIELDED",
            "ФАЗА 2 · ВТОРАЯ ВОЛНА! УНИЧТОЖЬТЕ МЕДУЗ (ОСТАЛОСЬ: {0}) - КОРОЛЬ ПОД ЩИТОМ",
            "FAZA 2 · DRUGA FALA! ZNISZCZ MEDUZY (POZOSTAŁO: {0}) - KRÓL MA TARCZĘ");
        Add("PHASE 2 · ATTACK THE KRAKEN KING!", "ФАЗА 2 · АТАКУЙТЕ КОРОЛЯ КРАКЕНОВ!", "FAZA 2 · ATAKUJ KRÓLA KRAKENÓW!");
        Add("THE KRAKEN KING IS DEFEATED!", "КОРОЛЬ КРАКЕНОВ ПОВЕРЖЕН!", "KRÓL KRAKENÓW POKONANY!");

        AddVotic();
    }

    // ---- Votic (vađđa ceeli) ----
    // DRAFT, NOT VERIFIED BY A SPEAKER. Votic is nearly extinct and poorly documented; these lines are a
    // best-effort reconstruction (Finnic roots + Russian loans) and should be checked against the
    // "Vadja keele sõnaraamat" (Institute of the Estonian Language) or by a Votic speaker.
    static void AddVotic()
    {
        // Main menu
        Vo("SEA BATTLE", "MERESÕTA");
        Vo("PLAY", "MÄNGIÄ");
        Vo("MAP", "KARTTA");
        Vo("SHOP", "LAVKA");
        Vo("QUIT", "VÄLJÄ");

        // Shop
        Vo("TO BUY", "MÜÜNNIS");
        Vo("BOUGHT", "OSTETUD");
        Vo("STATS", "TIEDOD");
        Vo("LOOT BOX", "ARKKU");
        Vo("BUY", "OSSAA");
        Vo("SELECT", "VALITA");
        Vo("SELL", "MÜÜÄ");
        Vo("✓ SELECTED", "✓ VALITUD");
        Vo("BLUE SHIP", "SININ LAIVA");
        Vo("YELLOW SHIP", "KÕLTÕIN LAIVA");
        Vo("YELLOW-RED SHIP", "KÕLTÕ-PUNNÕIN LAIVA");
        Vo("WHITE SHIP", "VALKÕA LAIVA");
        Vo("PIRAT SHIP", "MERIROSVO LAIVA");
        Vo("SHARK SHIP", "AKULA-LAIVA");
        Vo("PROMO CODE", "PROMOKOODI");
        Vo("Enter code...", "Kirjuta koodi...");
        Vo("REDEEM", "OTA VASTAA");

        // Stats table
        Vo("SHIP", "LAIVA");
        Vo("SIZE", "SUURUZ");
        Vo("HP", "VÄKI");
        Vo("SPD", "KIIRUZ");
        Vo("DMG", "KAHJO");
        Vo("DELAY", "OOTUZ");
        Vo("BLUE", "SININ");
        Vo("YELLOW", "KÕLTÕIN");
        Vo("YELLOW-RED", "KÕLTÕ-PUNNÕIN");
        Vo("PIRATE", "MERIROSVO");
        Vo("SHARK", "AKULA");
        Vo("BLACK (BOT)", "MUSSA (BOTTI)");

        // Loot box
        Vo("SEA CHEST", "MERE ARKKU");
        Vo("OPEN", "AVAA");
        Vo("OPEN (FREE)", "AVAA (ILMAA)");
        Vo("OPEN AGAIN (FREE)", "VIEL KERTA (ILMAA)");
        Vo("OPEN AGAIN ({0})", "VIEL KERTA ({0})");
        Vo("POSSIBLE DROPS", "MITÄ VOIB SAADA");
        Vo("BOXES: {0}", "ARKUD: {0}");
        Vo("YOU GOT:  {0}", "SAIT:  {0}");
        Vo("{0} (OWNED) -> +{1} COINS", "{0} (ON JO) -> +{1} RAHAA");
        Vo("OK", "ÜVÄ");

        // Coins (numerals take the partitive: "10 rahaa")
        Vo("COINS: {0}", "RAHAD: {0}");
        Vo("{0} COINS", "{0} RAHAA");
        Vo("+{0} COINS", "+{0} RAHAA");
        foreach (int n in new[] { 10, 50, 65, 100, 150, 200, 250, 1000 })
            Vo(n + " COINS", n + " RAHAA");

        // Promo codes
        Vo("Reset {0} code(s).", "Koodia nollattu: {0}.");
        Vo("Nothing to reset.", "Eb ole mitä nollata.");
        Vo("Level reset to 1.", "Tasõ nollattu: 1.");
        Vo("Already used.", "Jo käütettü.");
        Vo("Invalid code.", "Väärä koodi.");
        Vo("+{0} FREE LOOT BOXES!", "+{0} ILMAIZTA ARKKUA!");
        Vo("+{0} COINS + SHIP!", "+{0} RAHAA + LAIVA!");
        Vo("+{0} COINS!", "+{0} RAHAA!");

        // Game HUD
        Vo("LEVEL: {0}", "TASÕ: {0}");
        Vo("LEVEL: 1", "TASÕ: 1");
        Vo("YOUR SHIP", "SIÄ LAIVA");
        Vo("ENEMY SHIP", "VAINLAIZÕ LAIVA");
        Vo("VICTORY!", "VÕITTO!");
        Vo("DEFEATED", "KAOTUZ");
        Vo("PLAY AGAIN", "MÄNGIÄ VIEL");
        Vo("MAIN MENU", "PÄÄMENJU");
        Vo("PILLARS LEFT: {0}", "SAMMASSÕ JÄLLÕ: {0}");
        Vo("PILLARS LEFT: 3", "SAMMASSÕ JÄLLÕ: 3");

        // Level 7
        Vo("KRAKEN", "KRAKEN");
        Vo("SUNFISH", "KUUKALA");
        Vo("KRAKEN KING", "KRAKENI KUNINGAS");
        Vo("PHASE 1 / 2", "FAASI 1 / 2");
        Vo("PHASE 2 / 2", "FAASI 2 / 2");
        Vo("KRAKEN'S THRONE", "KRAKENI TROONA");
        Vo("PHASE 1 · DODGE THE TENTACLES  {0}:{1:00}", "FAASI 1 · VÄLTÄ LONKEROID  {0}:{1:00}");
        Vo("THE TENTACLES SINK...", "LONKEROD VAJOVAD...");
        Vo("PHASE 1 · ESCAPE THE WHIRLPOOL!", "FAASI 1 · PÄÄSE PÖÖRTEESSÄ!");
        Vo("PHASE 1 · THE KRAKEN IS COMING - GET AWAY!", "FAASI 1 · KRAKEN TULOB - PAKKO!");
        Vo("PHASE 1 · ATTACK THE KRAKEN!", "FAASI 1 · LÕÕ KRAKENIA!");
        Vo("THE KRAKEN DIVES...", "KRAKEN SUKALTAB...");
        Vo("THE KRAKEN SINKS...", "KRAKEN VAJOB...");
        Vo("PHASE 2 · SINK THE SUNFISH", "FAASI 2 · UPOTA KUUKALA");
        Vo("PHASE 2 · THE KRAKEN KING RISES!", "FAASI 2 · KRAKENI KUNINGAS NOUZOB!");
        Vo("PHASE 2 · SINK THE JELLYFISH ({0} LEFT) - THE KING IS SHIELDED",
           "FAASI 2 · UPOTA MEDUZAD ({0} JÄLLÕ) - KUNINGAS ON KILVÕ TAKAN");
        Vo("PHASE 2 · SECOND WAVE! SINK THE JELLYFISH ({0} LEFT) - THE KING IS SHIELDED",
           "FAASI 2 · TOIN AALTO! UPOTA MEDUZAD ({0} JÄLLÕ) - KUNINGAS ON KILVÕ TAKAN");
        Vo("PHASE 2 · ATTACK THE KRAKEN KING!", "FAASI 2 · LÕÕ KRAKENI KUNINGASTA!");
        Vo("THE KRAKEN KING IS DEFEATED!", "KRAKENI KUNINGAS ON VÕITETTU!");
    }

    static void Add(string en, string ru, string pl)
    {
        table[en] = new[] { ru, pl, null };
        AddReverse(en, en);
        AddReverse(ru, en);
        AddReverse(pl, en);
    }

    static void Vo(string en, string vot)
    {
        if (!table.TryGetValue(en, out var tr))
        {
            Debug.LogWarning($"[Loc] Votic line for unknown key \"{en}\"");
            return;
        }
        tr[2] = vot;
        AddReverse(vot, en);
    }

    static void AddReverse(string text, string key)
    {
        if (key.Contains("{")) return; // format strings are never matched against scene texts
        if (reverse.TryGetValue(text, out var other) && other != key)
            Debug.LogWarning($"[Loc] \"{text}\" is the text of both \"{other}\" and \"{key}\"");
        else
            reverse[text] = key;
    }

    // ---- Lookup ----

    public static string T(string key)
    {
        if (string.IsNullOrEmpty(key) || Current == Lang.EN) return key;
        return table.TryGetValue(key, out var tr) ? tr[(int)Current - 1] ?? key : key;
    }

    public static string F(string formatKey, params object[] args) => string.Format(T(formatKey), args);

    public static string Code => Codes[(int)Current];

    // ---- Language selection ----

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        string saved = PlayerPrefs.GetString(PrefsKey, "");
        int idx = Array.IndexOf(Codes, saved);
        Current = idx >= 0 ? (Lang)idx : FromSystem();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static Lang FromSystem()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Russian:
            case SystemLanguage.Belarusian:
            case SystemLanguage.Ukrainian:
                return Lang.RU;
            case SystemLanguage.Polish:
                return Lang.PL;
            default:
                return Lang.EN;
        }
    }

    public static void Set(Lang lang)
    {
        PlayerPrefs.SetString(PrefsKey, Codes[(int)lang]);
        PlayerPrefs.Save();
        if (lang == Current) return;
        Current = lang;
        for (int i = 0; i < SceneManager.sceneCount; i++) TranslateScene(SceneManager.GetSceneAt(i));
        Changed?.Invoke();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TranslateScene(scene);

    // Rewrites every Text (including inactive ones) whose text is a known string in any language.
    static void TranslateScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Text>(true))
                TranslateText(t);
    }

    public static void TranslateText(Text t)
    {
        if (t == null || string.IsNullOrEmpty(t.text)) return;
        if (!reverse.TryGetValue(t.text, out var key)) return;
        t.text = T(key);
        Fit(t);
    }

    // Lets a label shrink (down to half its size) when a translation is longer than the English text.
    public static void Fit(Text t)
    {
        if (t == null || t.resizeTextForBestFit) return;
        t.resizeTextMaxSize     = t.fontSize;
        t.resizeTextMinSize     = Mathf.Max(8, t.fontSize / 2);
        t.resizeTextForBestFit  = true;
    }
}
