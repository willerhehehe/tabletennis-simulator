using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TableTennis
{
    public enum Language { Chinese, French }

    // Chinese source text is the lookup key; Core keeps Chinese because it compares these strings as identifiers.
    public static class L
    {
        static Language current = Language.Chinese;
        static readonly Dictionary<string[], string[]> translatedArrays = new();
        static List<(Regex Pattern, string Target)> templates;

        public static Language Current
        {
            get => current;
            set { current = value; translatedArrays.Clear(); }
        }

        public static string T(string zh)
        {
            if (current == Language.Chinese || !HasChinese(zh)) return zh;
            if (FrenchText.Entries.TryGetValue(zh, out var fr)) return fr;
            return TranslateTemplate(zh) ?? zh;
        }

        public static string F(string zhFormat, params object[] args) => string.Format(T(zhFormat), args);

        public static string[] All(string[] zh)
        {
            if (current == Language.Chinese) return zh;
            if (!translatedArrays.TryGetValue(zh, out var result))
            {
                result = Array.ConvertAll(zh, T);
                translatedArrays[zh] = result;
            }
            return result;
        }

        static string TranslateTemplate(string zh)
        {
            templates ??= BuildTemplates();
            foreach (var (pattern, target) in templates)
            {
                var match = pattern.Match(zh);
                if (!match.Success) continue;
                return Regex.Replace(target, @"\{(\d+)\}", m => T(match.Groups[int.Parse(m.Groups[1].Value) + 1].Value));
            }
            return null;
        }

        static List<(Regex, string)> BuildTemplates()
        {
            var list = new List<(Regex, string)>();
            foreach (var (zh, fr) in FrenchText.Templates)
            {
                string pattern = "^" + Regex.Replace(Regex.Escape(zh), @"\\\{\d+}", "(.+?)") + "$";
                list.Add((new Regex(pattern, RegexOptions.Singleline), fr));
            }
            return list;
        }

        static bool HasChinese(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text) if (c >= '一' && c <= '鿿') return true;
            return false;
        }
    }
}
