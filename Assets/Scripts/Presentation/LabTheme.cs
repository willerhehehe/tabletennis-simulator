using System;
using System.Collections.Generic;
using UnityEngine;

namespace TableTennis
{
    public sealed class LabTheme : IDisposable
    {
        public GUISkin Skin { get; }
        public readonly GUIStyle heading, small, value, section, statusStyle, primaryButton, quietButton, cardStyle, metricStyle, tooltipStyle;
        readonly List<Texture2D> skinTextures = new List<Texture2D>();
        readonly Color mint = new Color(0.45f, 0.96f, 0.77f);
        public LabTheme(GUISkin source, Font font)
        {
            Skin = UnityEngine.Object.Instantiate(source);
            Skin.font = font;
            Skin.label.fontSize = 17; Skin.label.normal.textColor = new Color(0.88f, 0.92f, 0.94f);
            Skin.button.fontSize = 16; Skin.button.fixedHeight = 36;
            Skin.toggle.fontSize = 16; Skin.horizontalSlider.margin = new RectOffset(0, 0, 8, 13);
            Skin.textField.fontSize = 17; Skin.textField.fixedHeight = 30;
            Skin.textField.alignment = TextAnchor.MiddleRight;
            Skin.textField.padding = new RectOffset(8, 8, 4, 4);
            Skin.textField.border = new RectOffset(1, 1, 1, 1);
            Skin.textField.normal.background = BorderedTexture(new Color(0.09f, 0.13f, 0.16f), new Color(0.25f, 0.31f, 0.34f));
            Skin.textField.focused.background = BorderedTexture(new Color(0.11f, 0.19f, 0.20f), mint);
            Skin.textField.normal.textColor = Skin.textField.focused.textColor = new Color(0.94f, 0.97f, 0.98f);
            Texture2D normal = SkinTexture(new Color(0.12f, 0.17f, 0.20f)), hover = SkinTexture(new Color(0.19f, 0.29f, 0.31f));
            Skin.button.normal.background = normal; Skin.button.hover.background = hover;
            Skin.button.active.background = Skin.button.onNormal.background = hover;
            Skin.button.onHover.background = Skin.button.onActive.background = hover;
            Skin.button.focused.background = BorderedTexture(new Color(0.16f, 0.25f, 0.27f), mint);
            Skin.button.border = new RectOffset(1, 1, 1, 1);
            heading = new GUIStyle(Skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            heading.normal.textColor = new Color(0.88f, 0.94f, 0.96f);
            small = new GUIStyle(Skin.label) { fontSize = 16, wordWrap = true };
            small.normal.textColor = new Color(0.70f, 0.77f, 0.80f);
            value = new GUIStyle(Skin.label) { alignment = TextAnchor.MiddleRight };
            value.normal.textColor = mint;
            section = new GUIStyle(Skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 14, 8) };
            section.normal.textColor = new Color(0.89f, 0.94f, 0.95f);
            statusStyle = new GUIStyle(Skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            primaryButton = new GUIStyle(Skin.button) { fontStyle = FontStyle.Bold, fixedHeight = 42 };
            primaryButton.normal.background = SkinTexture(new Color(0.45f, 0.86f, 0.70f));
            primaryButton.hover.background = SkinTexture(new Color(0.61f, 0.94f, 0.81f));
            primaryButton.active.background = SkinTexture(new Color(0.30f, 0.69f, 0.55f));
            primaryButton.focused.background = BorderedTexture(new Color(0.45f, 0.86f, 0.70f), Color.white);
            primaryButton.normal.textColor = primaryButton.hover.textColor = primaryButton.active.textColor = primaryButton.focused.textColor = new Color(0.04f, 0.13f, 0.12f);
            quietButton = new GUIStyle(Skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(9, 9, 3, 3) };
            quietButton.normal.background = SkinTexture(new Color(0.085f, 0.12f, 0.15f));
            cardStyle = new GUIStyle(Skin.box) { padding = new RectOffset(12, 12, 10, 10), margin = new RectOffset(0, 0, 0, 8) };
            cardStyle.normal.background = SkinTexture(new Color(0.105f, 0.155f, 0.18f));
            metricStyle = new GUIStyle(Skin.label) { fontSize = 34, fontStyle = FontStyle.Bold };
            metricStyle.normal.textColor = mint;
            tooltipStyle = new GUIStyle(small) { padding = new RectOffset(10, 10, 7, 7), border = new RectOffset(1, 1, 1, 1) };
            tooltipStyle.normal.background = BorderedTexture(new Color(0.11f, 0.17f, 0.20f), new Color(0.32f, 0.42f, 0.46f));
        }

        Texture2D SkinTexture(Color color)
        {
            var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply();
            skinTextures.Add(texture); return texture;
        }

        Texture2D BorderedTexture(Color fill, Color border)
        {
            var texture = new Texture2D(3, 3);
            texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) texture.SetPixel(x, y, x == 1 && y == 1 ? fill : border);
            texture.Apply(); skinTextures.Add(texture); return texture;
        }

        public void Dispose()
        {
            if (Skin != null) UnityEngine.Object.Destroy(Skin);
            foreach (var texture in skinTextures) UnityEngine.Object.Destroy(texture);
        }
    }
}
