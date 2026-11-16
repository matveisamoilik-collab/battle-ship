using System.Collections.Generic;
using System.Text;

// Makes Arabic readable in Unity's legacy UI Text, which draws every string left-to-right and never
// joins Arabic letters. Shape() does two things to a logical-order Arabic string:
//   1. Joining: each letter becomes its isolated / final / initial / medial presentation form
//      (U+FE80…U+FEFC), lam + alef become the lam-alef ligature.
//   2. Ordering: the result is put in visual order for a right-to-left line: right-to-left runs are
//      reversed (brackets mirrored), left-to-right runs (Latin, digits, "{0}" placeholders, and
//      separators between them such as "0:20") keep their order, and the runs themselves are reversed.
// Placeholders survive untouched, so string.Format can fill them after shaping.
// Limits: no diacritics (harakat), single-line text only (Text wraps the visual string left-to-right,
// so a wrapped line order would be wrong — keep Arabic strings short).
public static class ArabicText
{
    // Arabic letter → (first presentation form, number of forms: 1 = none, 2 = joins only to the
    // previous letter, 4 = joins on both sides). Forms are consecutive: isolated, final, initial, medial.
    static readonly Dictionary<char, (char form, int count)> Letters = BuildLetters();

    static Dictionary<char, (char, int)> BuildLetters()
    {
        var d = new Dictionary<char, (char, int)>();
        int form = 0xFE80;
        // U+0621 … U+063A
        int[] countsA = { 1, 2, 2, 2, 2, 4, 2, 4, 2, 4, 4, 4, 4, 4, 2, 2, 2, 2, 4, 4, 4, 4, 4, 4, 4, 4 };
        for (int i = 0; i < countsA.Length; i++) { d[(char)(0x0621 + i)] = ((char)form, countsA[i]); form += countsA[i]; }
        // U+0641 … U+064A
        int[] countsB = { 4, 4, 4, 4, 4, 4, 4, 2, 2, 4 };
        for (int i = 0; i < countsB.Length; i++) { d[(char)(0x0641 + i)] = ((char)form, countsB[i]); form += countsB[i]; }
        return d;
    }

    const char Lam = 'ل';

    // lam-alef ligatures (isolated form; final = +1)
    static char LamAlef(char alef)
    {
        switch (alef)
        {
            case 'آ': return 'ﻵ'; // آ
            case 'أ': return 'ﻷ'; // أ
            case 'إ': return 'ﻹ'; // إ
            case 'ا': return 'ﻻ'; // ا
            default:       return '\0';
        }
    }

    static bool IsLetter(char c) => Letters.ContainsKey(c);
    static bool JoinsNext(char c) => Letters.TryGetValue(c, out var l) && l.count == 4;

    public static string Shape(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return Reorder(Join(s));
    }

    // ---- 1. Contextual forms ----

    static string Join(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (!Letters.TryGetValue(c, out var l)) { sb.Append(c); continue; }

            bool prevJoins = i > 0 && JoinsNext(s[i - 1]);

            if (c == Lam && i + 1 < s.Length)
            {
                char lig = LamAlef(s[i + 1]);
                if (lig != '\0')
                {
                    sb.Append(prevJoins ? (char)(lig + 1) : lig);
                    i++;
                    continue;
                }
            }

            bool nextJoins = l.count == 4 && i + 1 < s.Length && IsLetter(s[i + 1]);
            int offset;
            if (l.count == 1)                  offset = 0;
            else if (prevJoins && nextJoins)   offset = 3; // medial
            else if (nextJoins)                offset = 2; // initial
            else if (prevJoins)                offset = 1; // final
            else                               offset = 0; // isolated
            sb.Append((char)(l.form + offset));
        }
        return sb.ToString();
    }

    // ---- 2. Visual order for a right-to-left line ----

    enum Dir { L, R, N }

    static Dir Classify(char c)
    {
        if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return Dir.L;
        if ((c >= '؀' && c <= 'ۿ') || (c >= 'ﭐ' && c <= '﷿') || (c >= 'ﹰ' && c <= '﻿')) return Dir.R;
        return Dir.N;
    }

    static string Reorder(string s)
    {
        // Units: "{…}" placeholders are one left-to-right unit, everything else is one character.
        var units = new List<string>();
        var dirs  = new List<Dir>();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '{')
            {
                int end = s.IndexOf('}', i);
                if (end > i) { units.Add(s.Substring(i, end - i + 1)); dirs.Add(Dir.L); i = end; continue; }
            }
            units.Add(s[i].ToString());
            dirs.Add(Classify(s[i]));
        }

        // Neutrals: left-to-right only when sandwiched between left-to-right units (e.g. "0:20", "1 / 2"),
        // or a sign directly in front of one ("+5"); otherwise they follow the right-to-left base direction.
        int n = units.Count;
        var res = new Dir[n];
        for (int i = 0; i < n; i++)
        {
            if (dirs[i] != Dir.N) { res[i] = dirs[i]; continue; }
            int a = i - 1; while (a >= 0 && dirs[a] == Dir.N) a--;
            int b = i + 1; while (b < n && dirs[b] == Dir.N) b++;
            bool leftL  = a >= 0 && dirs[a] == Dir.L;
            bool rightL = b < n && dirs[b] == Dir.L;
            bool sign   = (units[i] == "+" || units[i] == "-") && i + 1 < n && dirs[i + 1] == Dir.L;
            res[i] = (leftL && rightL) || sign ? Dir.L : Dir.R;
        }

        // Runs in logical order, emitted last-to-first; right-to-left runs reversed and mirrored.
        var sb = new StringBuilder(s.Length);
        int runEnd = n;
        while (runEnd > 0)
        {
            int runStart = runEnd - 1;
            while (runStart > 0 && res[runStart - 1] == res[runEnd - 1]) runStart--;
            if (res[runEnd - 1] == Dir.L)
                for (int k = runStart; k < runEnd; k++) sb.Append(units[k]);
            else
                for (int k = runEnd - 1; k >= runStart; k--) sb.Append(Mirror(units[k]));
            runEnd = runStart;
        }
        return sb.ToString();
    }

    static string Mirror(string u)
    {
        switch (u)
        {
            case "(": return ")";
            case ")": return "(";
            case "[": return "]";
            case "]": return "[";
            case "<": return ">";
            case ">": return "<";
            default:  return u;
        }
    }
}
