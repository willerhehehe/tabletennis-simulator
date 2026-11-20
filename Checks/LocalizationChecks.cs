using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TableTennis;

static class LocalizationChecks
{
    static readonly Regex Literal = new(@"(\$?)""((?:[^""\\\n]|\\.)*)""");
    static readonly Regex Chinese = new(@"[一-鿿]");
    static readonly Regex Placeholder = new(@"\{\d+(?::[^}]*)?\}");
    // Chinese literals in Presentation that are object names, Core identifiers, language names or arrays wrapped with L.All.
    static readonly Regex UntranslatedContext = new(
        @"(?:Sphere|Line|Cube|Primitive)\(""|new GameObject\(""|[!=]= ""|static readonly string\[\]|Names = \{");

    public static void Run()
    {
        void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        string scripts = FindScripts();
        var missing = new List<string>();

        foreach (var file in Directory.GetFiles(Path.Combine(scripts, "Presentation"), "*.cs"))
        {
            if (Path.GetFileName(file) == "LabScene.cs") continue;
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
                foreach (Match m in Literal.Matches(lines[i]))
                {
                    if (!Chinese.IsMatch(m.Value)) continue;
                    string where = $"{Path.GetFileName(file)}:{i + 1}";
                    string prefix = lines[i].Substring(0, m.Index);
                    bool wrapped = prefix.EndsWith("L.T(") || prefix.EndsWith("L.F(");
                    if (m.Groups[1].Value == "$") missing.Add($"{where} interpolated Chinese text must use L.F");
                    else if (!wrapped && !UntranslatedContext.IsMatch(lines[i])) missing.Add($"{where} Chinese text not wrapped in L.T/L.F");
                    else if (!lines[i].Contains("Names = {") && !lines[i].Contains("== \"") && !lines[i].Contains("!= \"") && !IsObjectName(prefix))
                        RequireEntry(Regex.Unescape(m.Groups[2].Value), where, missing);
                }
        }

        var fragments = FrenchText.Templates.Select(t => t.Zh).ToArray();
        foreach (var dir in new[] { "Core", "Application" })
            foreach (var file in Directory.GetFiles(Path.Combine(scripts, dir), "*.cs"))
                foreach (Match m in Literal.Matches(File.ReadAllText(file)))
                {
                    string text = Regex.Unescape(m.Groups[2].Value);
                    if (!Chinese.IsMatch(text) || m.Groups[1].Value == "$" || fragments.Any(t => t.Contains(text))) continue;
                    RequireEntry(text, Path.GetFileName(file), missing);
                }
        Require(missing.Count == 0, "Missing French text:\n" + string.Join("\n", missing));

        foreach (var (zh, fr) in FrenchText.Entries.Select(e => (e.Key, e.Value)).Concat(FrenchText.Templates))
            Require(Placeholders(zh) == Placeholders(fr) && !Chinese.IsMatch(fr), $"French text mismatch for \"{zh}\": \"{fr}\"");

        var previous = L.Current;
        try
        {
            L.Current = Language.Chinese;
            Require(L.T("球台") == "球台" && L.F("出球 {0:0.00} m/s", 1.5) == string.Format("出球 {0:0.00} m/s", 1.5), "Chinese mode must return source text");
            var names = new[] { "黑点", "黑带" };
            Require(ReferenceEquals(L.All(names), names), "Chinese mode must not copy arrays");

            L.Current = Language.French;
            Require(L.T("球台") == "Table", "exact French lookup failed");
            Require(L.F("出球 {0:0.00} m/s", 1.5) == string.Format("Sortie {0:0.00} m/s", 1.5), "French format failed");
            Require(L.T("顺旋·侧下·短球") == "Pendule · latéral-coupé · court", "serve preset name template failed: " + L.T("顺旋·侧下·短球"));
            Require(L.T("漏球：击拍前先碰到球台") == "Balle manquée : touche d'abord Table", "nested template failed");
            Require(L.T("这组抛球假设需摩擦 μ ≥ 0.50，当前 μ = 0.30。请减弱旋转或提高线速度。") ==
                "Ce lancer nécessite μ ≥ 0.50, actuel μ = 0.30. Réduisez l'effet ou augmentez la vitesse.", "numeric template failed");
            Require(L.T("Already French") == "Already French" && L.T("未收录") == "未收录" && L.T(null) == null, "fallback must return input");
            var translated = L.All(names);
            Require(translated.SequenceEqual(new[] { "Points", "Bande" }) && ReferenceEquals(translated, L.All(names)), "array translation must be cached");
            L.Current = Language.Chinese;
            Require(ReferenceEquals(L.All(names), names), "switching back must restore Chinese arrays");
        }
        finally { L.Current = previous; }
        Console.WriteLine($"Localization checks passed: {FrenchText.Entries.Count} entries, {FrenchText.Templates.Length} templates");
    }

    static bool IsObjectName(string prefix) => Regex.IsMatch(prefix, @"(?:Sphere|Line|Cube|Primitive|new GameObject)\($");

    static void RequireEntry(string zh, string where, List<string> missing)
    {
        if (!FrenchText.Entries.ContainsKey(zh)) missing.Add($"{where} \"{zh}\"");
    }

    static string Placeholders(string text) =>
        string.Join(",", Placeholder.Matches(text).Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal));

    static string FindScripts()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "Assets", "Scripts");
            if (Directory.Exists(path)) return path;
        }
        throw new DirectoryNotFoundException("Assets/Scripts not found above " + AppContext.BaseDirectory);
    }
}
