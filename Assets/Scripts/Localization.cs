using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Game localization: English (source), Russian, Polish, Votic (draft — see AddVotic), Belarusian,
// Ukrainian, Spanish, Chinese (Simplified) and Arabic (shaped for the legacy Text renderer by ArabicText).
//
// Keys are the English strings themselves. Scene texts built by GameSetup keep their English text;
// on every scene load (and on every language switch) all Text components whose text matches a key
// in ANY language are rewritten in the current language — so no Build All is needed and no
// per-Text component. Texts that code sets at runtime go through Loc.T(key) / Loc.F(formatKey, args)
// and are refreshed by their owners on Loc.Changed.
//
// The selected language is stored in PlayerPrefs "Language" ("en" / "ru" / "pl" / "vot" / "be" / "uk" /
// "es" / "zh" / "ar"); on first launch it follows the device language (English when unsupported; Votic
// is never chosen automatically).
public static class Loc
{
    public enum Lang { EN = 0, RU = 1, PL = 2, VO = 3, BE = 4, UK = 5, ES = 6, ZH = 7, AR = 8 }

    public const string PrefsKey = "Language";
    static readonly string[] Codes = { "en", "ru", "pl", "vot", "be", "uk", "es", "zh", "ar" };
    public static int LanguageCount => Codes.Length;

    public static Lang Current { get; private set; } = Lang.EN;
    public static event Action Changed;

    // key (English) → one entry per non-English language, index = (int)Lang - 1 (a missing entry falls back to English).
    // Arabic entries are stored already shaped + in visual order (see ArabicText), exactly as Text displays them.
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
        Add("{0}!", "{0}!", "{0}!"); // reward reveal, e.g. "YELLOW SHIP!"

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
        AddMore();
    }

    // ---- Belarusian, Ukrainian, Spanish, Chinese (Simplified), Arabic ----
    // Columns: English key, BE, UK, ES, ZH, AR. Arabic is written in logical order; it is shaped on registration.
    static void AddMore()
    {
        // Main menu
        More("SEA BATTLE", "МАРСКІ БОЙ", "МОРСЬКИЙ БІЙ", "BATALLA NAVAL", "海战", "معركة بحرية");
        More("PLAY", "ГУЛЯЦЬ", "ГРАТИ", "JUGAR", "开始游戏", "العب");
        More("MAP", "МАПА", "МАПА", "MAPA", "地图", "الخريطة");
        More("SHOP", "КРАМА", "МАГАЗИН", "TIENDA", "商店", "المتجر");
        More("QUIT", "ВЫХАД", "ВИХІД", "SALIR", "退出", "خروج");

        // Shop
        More("TO BUY", "У ПРОДАЖЫ", "У ПРОДАЖУ", "EN VENTA", "可购买", "للشراء");
        More("BOUGHT", "КУПЛЕНА", "КУПЛЕНО", "COMPRADOS", "已购买", "تم شراؤها");
        More("STATS", "ХАРАКТАРЫСТЫКІ", "ХАРАКТЕРИСТИКИ", "ESTADÍSTICAS", "属性", "الإحصائيات");
        More("LOOT BOX", "СКРЫНЯ", "СКРИНЯ", "COFRE", "宝箱", "صندوق الغنائم");
        More("BUY", "КУПІЦЬ", "КУПИТИ", "COMPRAR", "购买", "شراء");
        More("SELECT", "ВЫБРАЦЬ", "ВИБРАТИ", "ELEGIR", "选择", "اختيار");
        More("SELL", "ПРАДАЦЬ", "ПРОДАТИ", "VENDER", "出售", "بيع");
        More("✓ SELECTED", "✓ ВЫБРАНЫ", "✓ ОБРАНО", "✓ ELEGIDO", "✓ 已选择", "✓ مختارة");
        More("BLUE SHIP", "СІНІ КАРАБЕЛЬ", "СИНІЙ КОРАБЕЛЬ", "BARCO AZUL", "蓝色战舰", "السفينة الزرقاء");
        More("YELLOW SHIP", "ЖОЎТЫ КАРАБЕЛЬ", "ЖОВТИЙ КОРАБЕЛЬ", "BARCO AMARILLO", "黄色战舰", "السفينة الصفراء");
        More("YELLOW-RED SHIP", "ЖОЎТА-ЧЫРВОНЫ КАРАБЕЛЬ", "ЖОВТО-ЧЕРВОНИЙ КОРАБЕЛЬ", "BARCO AMARILLO-ROJO", "黄红战舰", "السفينة الصفراء الحمراء");
        More("WHITE SHIP", "БЕЛЫ КАРАБЕЛЬ", "БІЛИЙ КОРАБЕЛЬ", "BARCO BLANCO", "白色战舰", "السفينة البيضاء");
        More("PIRAT SHIP", "ПІРАЦКІ КАРАБЕЛЬ", "ПІРАТСЬКИЙ КОРАБЕЛЬ", "BARCO PIRATA", "海盗船", "سفينة القراصنة");
        More("SHARK SHIP", "КАРАБЕЛЬ-АКУЛА", "КОРАБЕЛЬ-АКУЛА", "BARCO TIBURÓN", "鲨鱼战舰", "سفينة القرش");
        More("PROMO CODE", "ПРАМАКОД", "ПРОМОКОД", "CÓDIGO PROMOCIONAL", "兑换码", "رمز ترويجي");
        More("Enter code...", "Увядзіце код...", "Введіть код...", "Introduce el código...", "输入兑换码...", "أدخل الرمز...");
        More("REDEEM", "АКТЫВАВАЦЬ", "АКТИВУВАТИ", "CANJEAR", "兑换", "استبدال");

        // Stats table
        More("SHIP", "КАРАБЕЛЬ", "КОРАБЕЛЬ", "BARCO", "战舰", "السفينة");
        More("SIZE", "ПАМЕР", "РОЗМІР", "TAMAÑO", "尺寸", "الحجم");
        More("HP", "МОЦН.", "МІЦН.", "VIDA", "生命", "الصحة");
        More("SPD", "ХУТК.", "ШВИДК.", "VEL.", "速度", "السرعة");
        More("DMG", "ШКОДА", "ШКОДА", "DAÑO", "伤害", "الضرر");
        More("DELAY", "ПЕРАЗАР.", "ПЕРЕЗАР.", "RECARGA", "冷却", "التعبئة");
        More("BLUE", "СІНІ", "СИНІЙ", "AZUL", "蓝色", "الأزرق");
        More("YELLOW", "ЖОЎТЫ", "ЖОВТИЙ", "AMARILLO", "黄色", "الأصفر");
        More("YELLOW-RED", "ЖОЎТА-ЧЫРВОНЫ", "ЖОВТО-ЧЕРВОНИЙ", "AMARILLO-ROJO", "黄红", "الأصفر الأحمر");
        More("PIRATE", "ПІРАЦКІ", "ПІРАТСЬКИЙ", "PIRATA", "海盗", "القرصان");
        More("SHARK", "АКУЛА", "АКУЛА", "TIBURÓN", "鲨鱼", "القرش");
        More("BLACK (BOT)", "ЧОРНЫ (БОТ)", "ЧОРНИЙ (БОТ)", "NEGRO (BOT)", "黑色 (电脑)", "الأسود (آلي)");

        // Loot box
        More("SEA CHEST", "МАРСКАЯ СКРЫНЯ", "МОРСЬКА СКРИНЯ", "COFRE MARINO", "海洋宝箱", "صندوق البحر");
        More("OPEN", "АДКРЫЦЬ", "ВІДКРИТИ", "ABRIR", "打开", "افتح");
        More("OPEN (FREE)", "АДКРЫЦЬ (БЯСПЛАТНА)", "ВІДКРИТИ (БЕЗКОШТОВНО)", "ABRIR (GRATIS)", "打开 (免费)", "افتح (مجانا)");
        More("OPEN AGAIN (FREE)", "ЯШЧЭ РАЗ (БЯСПЛАТНА)", "ЩЕ РАЗ (БЕЗКОШТОВНО)", "OTRA VEZ (GRATIS)", "再开一次 (免费)", "مرة أخرى (مجانا)");
        More("OPEN AGAIN ({0})", "ЯШЧЭ РАЗ ({0})", "ЩЕ РАЗ ({0})", "OTRA VEZ ({0})", "再开一次 ({0})", "مرة أخرى ({0})");
        More("POSSIBLE DROPS", "МАГЧЫМЫЯ ЎЗНАГАРОДЫ", "МОЖЛИВІ НАГОРОДИ", "POSIBLES PREMIOS", "可能获得", "الجوائز الممكنة");
        More("BOXES: {0}", "СКРЫНІ: {0}", "СКРИНІ: {0}", "COFRES: {0}", "宝箱：{0}", "الصناديق: {0}");
        More("YOU GOT:  {0}", "ВЫПАЛА:  {0}", "ВИПАЛО:  {0}", "HAS OBTENIDO:  {0}", "你获得了：{0}", "حصلت على:  {0}");
        More("{0} (OWNED) -> +{1} COINS", "{0} (УЖО ЁСЦЬ) -> +{1} МАНЕТ", "{0} (ВЖЕ Є) -> +{1} МОНЕТ",
             "{0} (YA LO TIENES) -> +{1} MONEDAS", "{0} (已拥有) -> +{1} 金币", "{0} (مملوكة): +{1} عملة");
        More("OK", "ДОБРА", "ДОБРЕ", "VALE", "确定", "حسنا");
        More("{0}!", "{0}!", "{0}!", "¡{0}!", "{0}！", "{0}!");

        // Coins (every amount used is 0 or ends in 0/5 → genitive plural in BE/UK)
        More("COINS: {0}", "МАНЕТЫ: {0}", "МОНЕТИ: {0}", "MONEDAS: {0}", "金币：{0}", "العملات: {0}");
        More("{0} COINS", "{0} МАНЕТ", "{0} МОНЕТ", "{0} MONEDAS", "{0} 金币", "{0} عملة");
        More("+{0} COINS", "+{0} МАНЕТ", "+{0} МОНЕТ", "+{0} MONEDAS", "+{0} 金币", "+{0} عملة");
        foreach (int n in new[] { 10, 50, 65, 100, 150, 200, 250, 1000 })
            More(n + " COINS", n + " МАНЕТ", n + " МОНЕТ", n + " MONEDAS", n + " 金币", n + (n == 10 ? " عملات" : " عملة"));

        // Promo codes
        More("Reset {0} code(s).", "Скінута кодаў: {0}.", "Скинуто кодів: {0}.", "Códigos restablecidos: {0}.", "已重置兑换码：{0}。", "تمت إعادة تعيين الرموز: {0}.");
        More("Nothing to reset.", "Няма чаго скідаць.", "Нічого скидати.", "No hay nada que restablecer.", "没有可重置的内容。", "لا يوجد ما يعاد تعيينه.");
        More("Level reset to 1.", "Узровень скінуты да 1.", "Рівень скинуто до 1.", "Nivel restablecido a 1.", "关卡已重置为 1。", "تمت إعادة المستوى إلى 1.");
        More("Already used.", "Ужо выкарыстаны.", "Вже використано.", "Ya usado.", "已使用。", "مستخدم بالفعل.");
        More("Invalid code.", "Няправільны код.", "Невірний код.", "Código no válido.", "兑换码无效。", "رمز غير صالح.");
        More("+{0} FREE LOOT BOXES!", "+{0} БЯСПЛАТНЫХ СКРЫНЬ!", "+{0} БЕЗКОШТОВНИХ СКРИНЬ!", "¡+{0} COFRES GRATIS!", "+{0} 个免费宝箱！", "+{0} صناديق مجانية!");
        More("+{0} COINS + SHIP!", "+{0} МАНЕТ + КАРАБЕЛЬ!", "+{0} МОНЕТ + КОРАБЕЛЬ!", "¡+{0} MONEDAS + BARCO!", "+{0} 金币 + 战舰！", "+{0} عملة + سفينة!");
        More("+{0} COINS!", "+{0} МАНЕТ!", "+{0} МОНЕТ!", "¡+{0} MONEDAS!", "+{0} 金币！", "+{0} عملة!");

        // Game HUD
        More("LEVEL: {0}", "УЗРОВЕНЬ: {0}", "РІВЕНЬ: {0}", "NIVEL: {0}", "关卡：{0}", "المستوى: {0}");
        More("LEVEL: 1", "УЗРОВЕНЬ: 1", "РІВЕНЬ: 1", "NIVEL: 1", "关卡：1", "المستوى: 1");
        More("YOUR SHIP", "ВАШ КАРАБЕЛЬ", "ВАШ КОРАБЕЛЬ", "TU BARCO", "你的战舰", "سفينتك");
        More("ENEMY SHIP", "КАРАБЕЛЬ ВОРАГА", "КОРАБЕЛЬ ВОРОГА", "BARCO ENEMIGO", "敌方战舰", "سفينة العدو");
        More("VICTORY!", "ПЕРАМОГА!", "ПЕРЕМОГА!", "¡VICTORIA!", "胜利！", "النصر!");
        More("DEFEATED", "ПАРАЗА", "ПОРАЗКА", "DERROTA", "失败", "هزيمة");
        More("PLAY AGAIN", "ГУЛЯЦЬ ЗНОЎ", "ГРАТИ ЗНОВУ", "JUGAR DE NUEVO", "再玩一次", "العب مجددا");
        More("MAIN MENU", "ГАЛОЎНАЕ МЕНЮ", "ГОЛОВНЕ МЕНЮ", "MENÚ PRINCIPAL", "主菜单", "القائمة الرئيسية");
        More("PILLARS LEFT: {0}", "ЗАСТАЛОСЯ КАЛОН: {0}", "ЗАЛИШИЛОСЯ КОЛОН: {0}", "PILARES RESTANTES: {0}", "剩余石柱：{0}", "الأعمدة المتبقية: {0}");
        More("PILLARS LEFT: 3", "ЗАСТАЛОСЯ КАЛОН: 3", "ЗАЛИШИЛОСЯ КОЛОН: 3", "PILARES RESTANTES: 3", "剩余石柱：3", "الأعمدة المتبقية: 3");

        // Level 7
        More("KRAKEN", "КРАКЕН", "КРАКЕН", "KRAKEN", "克拉肯", "الكراكن");
        More("SUNFISH", "РЫБА-МЕСЯЦ", "РИБА-МІСЯЦЬ", "PEZ LUNA", "翻车鱼", "سمكة الشمس");
        More("KRAKEN KING", "КАРОЛЬ КРАКЕНАЎ", "КОРОЛЬ КРАКЕНІВ", "REY KRAKEN", "克拉肯之王", "ملك الكراكن");
        More("PHASE 1 / 2", "ФАЗА 1 / 2", "ФАЗА 1 / 2", "FASE 1 / 2", "阶段 1 / 2", "المرحلة 1 من 2");
        More("PHASE 2 / 2", "ФАЗА 2 / 2", "ФАЗА 2 / 2", "FASE 2 / 2", "阶段 2 / 2", "المرحلة 2 من 2");
        More("KRAKEN'S THRONE", "ТРОН КРАКЕНА", "ТРОН КРАКЕНА", "EL TRONO DEL KRAKEN", "克拉肯王座", "عرش الكراكن");
        More("PHASE 1 · DODGE THE TENTACLES  {0}:{1:00}", "ФАЗА 1 · УХІЛЯЙЦЕСЯ АД ШЧУПАЛЬЦАЎ  {0}:{1:00}", "ФАЗА 1 · УХИЛЯЙТЕСЯ ВІД ЩУПАЛЕЦЬ  {0}:{1:00}",
             "FASE 1 · ESQUIVA LOS TENTÁCULOS  {0}:{1:00}", "阶段 1 · 躲避触手  {0}:{1:00}", "المرحلة 1 · تفاد المجسات  {0}:{1:00}");
        More("THE TENTACLES SINK...", "ШЧУПАЛЬЦЫ ЙДУЦЬ ПАД ВАДУ...", "ЩУПАЛЬЦЯ ЙДУТЬ ПІД ВОДУ...", "LOS TENTÁCULOS SE HUNDEN...", "触手沉入水中……", "المجسات تغوص...");
        More("PHASE 1 · ESCAPE THE WHIRLPOOL!", "ФАЗА 1 · ВЫРВІЦЕСЯ З ВІРУ!", "ФАЗА 1 · ВИРВІТЬСЯ З ВИРУ!", "FASE 1 · ¡ESCAPA DEL REMOLINO!", "阶段 1 · 逃离漩涡！", "المرحلة 1 · اهرب من الدوامة!");
        More("PHASE 1 · THE KRAKEN IS COMING - GET AWAY!", "ФАЗА 1 · КРАКЕН ВЫПЛЫВАЕ - УЦЯКАЙЦЕ!", "ФАЗА 1 · КРАКЕН СПЛИВАЄ - ТІКАЙТЕ!",
             "FASE 1 · ¡EL KRAKEN SE ACERCA, HUYE!", "阶段 1 · 克拉肯来了，快躲开！", "المرحلة 1 · الكراكن قادم - ابتعد!");
        More("PHASE 1 · ATTACK THE KRAKEN!", "ФАЗА 1 · АТАКУЙЦЕ КРАКЕНА!", "ФАЗА 1 · АТАКУЙТЕ КРАКЕНА!", "FASE 1 · ¡ATACA AL KRAKEN!", "阶段 1 · 攻击克拉肯！", "المرحلة 1 · هاجم الكراكن!");
        More("THE KRAKEN DIVES...", "КРАКЕН НЫРАЕ...", "КРАКЕН ПІРНАЄ...", "EL KRAKEN SE SUMERGE...", "克拉肯潜入水中……", "الكراكن يغوص...");
        More("THE KRAKEN SINKS...", "КРАКЕН ТОНЕ...", "КРАКЕН ТОНЕ...", "EL KRAKEN SE HUNDE...", "克拉肯沉没了……", "الكراكن يغرق...");
        More("PHASE 2 · SINK THE SUNFISH", "ФАЗА 2 · ПЕРАМАЖЫЦЕ РЫБУ-МЕСЯЦ", "ФАЗА 2 · ПЕРЕМОЖІТЬ РИБУ-МІСЯЦЬ", "FASE 2 · HUNDE AL PEZ LUNA", "阶段 2 · 击沉翻车鱼", "المرحلة 2 · أغرق سمكة الشمس");
        More("PHASE 2 · THE KRAKEN KING RISES!", "ФАЗА 2 · КАРОЛЬ КРАКЕНАЎ ПАЎСТАЕ!", "ФАЗА 2 · КОРОЛЬ КРАКЕНІВ ПОВСТАЄ!",
             "FASE 2 · ¡EL REY KRAKEN SE ALZA!", "阶段 2 · 克拉肯之王现身！", "المرحلة 2 · ملك الكراكن ينهض!");
        More("PHASE 2 · SINK THE JELLYFISH ({0} LEFT) - THE KING IS SHIELDED",
             "ФАЗА 2 · ЗНІШЧЫЦЕ МЕДУЗ (ЗАСТАЛОСЯ: {0}) - КАРОЛЬ ПАД ШЧЫТОМ",
             "ФАЗА 2 · ЗНИЩТЕ МЕДУЗ (ЗАЛИШИЛОСЯ: {0}) - КОРОЛЬ ПІД ЩИТОМ",
             "FASE 2 · HUNDE LAS MEDUSAS (QUEDAN {0}) - EL REY TIENE ESCUDO",
             "阶段 2 · 消灭水母 (剩余 {0}) - 王有护盾",
             "المرحلة 2 · أغرق قناديل البحر (المتبقي: {0}) - الملك محمي بدرع");
        More("PHASE 2 · SECOND WAVE! SINK THE JELLYFISH ({0} LEFT) - THE KING IS SHIELDED",
             "ФАЗА 2 · ДРУГАЯ ХВАЛЯ! ЗНІШЧЫЦЕ МЕДУЗ (ЗАСТАЛОСЯ: {0}) - КАРОЛЬ ПАД ШЧЫТОМ",
             "ФАЗА 2 · ДРУГА ХВИЛЯ! ЗНИЩТЕ МЕДУЗ (ЗАЛИШИЛОСЯ: {0}) - КОРОЛЬ ПІД ЩИТОМ",
             "FASE 2 · ¡SEGUNDA OLEADA! HUNDE LAS MEDUSAS (QUEDAN {0}) - EL REY TIENE ESCUDO",
             "阶段 2 · 第二波！消灭水母 (剩余 {0}) - 王有护盾",
             "المرحلة 2 · الموجة الثانية! أغرق قناديل البحر (المتبقي: {0}) - الملك محمي بدرع");
        More("PHASE 2 · ATTACK THE KRAKEN KING!", "ФАЗА 2 · АТАКУЙЦЕ КАРАЛЯ КРАКЕНАЎ!", "ФАЗА 2 · АТАКУЙТЕ КОРОЛЯ КРАКЕНІВ!",
             "FASE 2 · ¡ATACA AL REY KRAKEN!", "阶段 2 · 攻击克拉肯之王！", "المرحلة 2 · هاجم ملك الكراكن!");
        More("THE KRAKEN KING IS DEFEATED!", "КАРОЛЬ КРАКЕНАЎ ПЕРАМОЖАНЫ!", "КОРОЛЬ КРАКЕНІВ ПЕРЕМОЖЕНИЙ!",
             "¡EL REY KRAKEN HA SIDO DERROTADO!", "克拉肯之王被击败了！", "هزم ملك الكراكن!");
    }

    static void More(string en, string be, string uk, string es, string zh, string ar)
    {
        Set(Lang.BE, en, be);
        Set(Lang.UK, en, uk);
        Set(Lang.ES, en, es);
        Set(Lang.ZH, en, zh);
        Set(Lang.AR, en, ArabicText.Shape(ar));
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
        Vo("{0}!", "{0}!");

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
        var tr = new string[Codes.Length - 1];
        tr[(int)Lang.RU - 1] = ru;
        tr[(int)Lang.PL - 1] = pl;
        table[en] = tr;
        AddReverse(en, en);
        AddReverse(ru, en);
        AddReverse(pl, en);
    }

    static void Vo(string en, string vot) => Set(Lang.VO, en, vot);

    // Adds one language's text for an existing key.
    static void Set(Lang lang, string en, string text)
    {
        if (!table.TryGetValue(en, out var tr))
        {
            Debug.LogWarning($"[Loc] {Codes[(int)lang]} line for unknown key \"{en}\"");
            return;
        }
        tr[(int)lang - 1] = text;
        AddReverse(text, en);
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
            case SystemLanguage.Russian:            return Lang.RU;
            case SystemLanguage.Belarusian:         return Lang.BE;
            case SystemLanguage.Ukrainian:          return Lang.UK;
            case SystemLanguage.Polish:             return Lang.PL;
            case SystemLanguage.Spanish:            return Lang.ES;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return Lang.ZH;
            case SystemLanguage.Arabic:             return Lang.AR;
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
