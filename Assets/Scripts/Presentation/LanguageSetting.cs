using UnityEngine;

namespace TableTennis
{
    static class LanguageSetting
    {
        const string PreferenceKey = "Language";
        static readonly string[] Names = { "中文", "Français" };

        public static void Load()
        {
            int fallback = Application.systemLanguage == SystemLanguage.French ? (int)Language.French : (int)Language.Chinese;
            L.Current = PlayerPrefs.GetInt(PreferenceKey, fallback) == (int)Language.French ? Language.French : Language.Chinese;
        }

        public static void DrawSelector()
        {
            int chosen = GUILayout.Toolbar((int)L.Current, Names);
            if (chosen == (int)L.Current) return;
            L.Current = (Language)chosen;
            PlayerPrefs.SetInt(PreferenceKey, chosen);
            PlayerPrefs.Save();
        }
    }
}
