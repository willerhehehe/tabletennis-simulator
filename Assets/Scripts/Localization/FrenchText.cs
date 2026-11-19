using System.Collections.Generic;

namespace TableTennis
{
    static partial class FrenchText
    {
        static Dictionary<string, string> entries;
        // Lazy: field initializers in other partial files are not guaranteed to run first.
        public static Dictionary<string, string> Entries => entries ??= Build();

        // Core messages assembled at runtime; each {n} is captured and translated on its own.
        public static readonly (string Zh, string Fr)[] Templates =
        {
            ("{0}·短球", "{0} · court"),
            ("{0}·长球", "{0} · long"),
            ("未知的反胶响应配置：{0}", "Configuration de réponse backside inconnue : {0}"),
            ("漏球：击拍前先碰到{0}", "Balle manquée : touche d'abord {0}"),
            ("发球 {0}", "Service {0}"),
            ("在无旋、无水平速度、竖直速度 ±{0} m/s 的抛球示例范围内，未找到满足当前胶皮参数的匹配动作；这不代表真实发球做不到。", "Aucun geste compatible avec ce revêtement pour un lancer sans effet ni vitesse horizontale, vitesse verticale ±{0} m/s ; ce service reste peut-être possible en réalité."),
            ("这组抛球假设需摩擦 μ ≥ {0}，当前 μ = {1}。请减弱旋转或提高线速度。", "Ce lancer nécessite μ ≥ {0}, actuel μ = {1}. Réduisez l'effet ou augmentez la vitesse."),
        };

        static Dictionary<string, string> Build()
        {
            var entries = new Dictionary<string, string>();
            foreach (var (zh, fr) in UiEntries) entries.Add(zh, fr);
            foreach (var (zh, fr) in CoreEntries) entries.Add(zh, fr);
            return entries;
        }
    }
}
