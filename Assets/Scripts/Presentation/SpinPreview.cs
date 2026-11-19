using System;
using System.IO;
using UnityEngine;

namespace TableTennis
{
    public enum SpinSkin { BlackDots, Band, Grid, Image }

    // A view-only sphere. Each visible normal is inverse-rotated into body texture coordinates.
    public sealed class SpinPreview : IDisposable
    {
        const int Size = 256;
        const float Radius = 101f, Half = Size * 0.5f;
        const string Preference = "TableTennis.SpinPreview.";
        static readonly string[] BasicSkins = { "黑点", "黑带", "网格" }, ImageSkins = { "黑点", "黑带", "网格", "标志" };
        // Icosahedron vertices give every dot the same spacing to its five neighbours.
        const float Phi = 1.618034f;
        static readonly Vector3[] Dots = {
            new Vector3(0, 1, Phi).normalized, new Vector3(0, -1, Phi).normalized,
            new Vector3(0, 1, -Phi).normalized, new Vector3(0, -1, -Phi).normalized,
            new Vector3(1, Phi, 0).normalized, new Vector3(-1, Phi, 0).normalized,
            new Vector3(1, -Phi, 0).normalized, new Vector3(-1, -Phi, 0).normalized,
            new Vector3(Phi, 0, 1).normalized, new Vector3(Phi, 0, -1).normalized,
            new Vector3(-Phi, 0, 1).normalized, new Vector3(-Phi, 0, -1).normalized
        };
        static readonly float DotThreshold = Mathf.Cos(9 * Mathf.Deg2Rad);
        static readonly Vector3 BandNormal = new Vector3(1, 0.3f, 0.2f).normalized;
        readonly Texture2D sphere;
        readonly Color32[] background, pixels;
        readonly Vector3[] normals;
        Quaternion mainView = Quaternion.identity;
        public bool FollowMainView { get; set; } = true;
        public Quaternion ViewRotation => FollowMainView ? mainView : Quaternion.identity;
        public void SetViewRotation(Quaternion rotation)
        {
            if (Quaternion.Angle(mainView, rotation) > 0.001f) dirty = true;
            mainView = rotation;
        }
        Quaternion orientation = Quaternion.identity;
        double lastDraw, lastImage;
        bool dirty = true, lastValid, lastArrows, displaySettings;
        SpinSkin lastSkin;
        Color32[] imported;
        int imageWidth, imageHeight;
        string imageStatus = "";
        public V3 LocalSpin { get; private set; }
        public SpinSkin Skin { get; set; } = SpinSkin.BlackDots;
        public bool ShowArrows { get; set; } = true;
        public bool Paused { get; set; }
        public bool Slower { get; set; }

        public SpinPreview()
        {
            sphere = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            background = new Color32[Size * Size]; pixels = new Color32[background.Length]; normals = new Vector3[background.Length];
            var light = new Vector3(-0.4f, 0.5f, 1).normalized;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                float px = (x + 0.5f - Half) / Radius, py = (y + 0.5f - Half) / Radius;
                float d = Mathf.Sqrt(px * px + py * py), z = Mathf.Sqrt(Mathf.Max(0, 1 - d * d));
                int i = y * Size + x; normals[i] = new Vector3(px, py, -z);
                float shade = 0.67f + 0.3f * Mathf.Max(0, Vector3.Dot(new Vector3(px, py, z), light));
                background[i] = new Color(shade, shade, shade * 0.94f, Mathf.Clamp01((1 - d) * Radius));
            }
            string path = PlayerPrefs.GetString(Preference + "Image", "");
            if (!string.IsNullOrEmpty(path))
            {
                try { LoadImage(path); }
                catch (Exception) { imageStatus = L.T("上次标志文件无法读取，可重新导入。"); }
            }
            int skin = PlayerPrefs.GetInt(Preference + "Skin", 0);
            Skin = skin >= 0 && skin <= (imported == null ? 2 : 3) ? (SpinSkin)skin : SpinSkin.BlackDots;
        }

        public void Reset() { orientation = Quaternion.identity; lastDraw = Time.realtimeSinceStartupAsDouble; dirty = true; }
        public void Step()
        {
            if (LocalSpin.Length < 0.001) return;
            Paused = true;
            orientation = Quaternion.AngleAxis(15, SpinAxis(LocalSpin)) * orientation;
            dirty = true;
        }
        static Vector3 SpinAxis(V3 spin) => new Vector3((float)spin.X, (float)spin.Y, (float)spin.Z).normalized;

        public void DrawControls(GUIStyle button, GUIStyle small, bool expanded = false)
        {
            if (!expanded && GUILayout.Button((displaySettings ? "− " : "+ ") + L.T("显示设置 · 视角 / 皮肤"), button)) displaySettings = !displaySettings;
            if (expanded || displaySettings)
            {
            FollowMainView = GUILayout.Toolbar(FollowMainView ? 0 : 1, new[] { L.T("跟随主视角"), L.T("固定发球者") }) == 0;
            int chosen = GUILayout.Toolbar((int)Skin, L.All(imported == null ? BasicSkins : ImageSkins));
            if (chosen != (int)Skin) { Skin = (SpinSkin)chosen; Reset(); SaveSkin(); }
            GUILayout.BeginHorizontal();
#if UNITY_EDITOR
            if (GUILayout.Button(L.T("导入标志"), button, GUILayout.Width(102)))
            {
                string path = UnityEditor.EditorUtility.OpenFilePanelWithFilters(L.T("选择球面标志"), "", new[] { L.T("图片"), "png,jpg,jpeg" });
                if (!string.IsNullOrEmpty(path))
                {
                    try { LoadImage(path); PlayerPrefs.SetString(Preference + "Image", path); SaveSkin(); }
                    catch (Exception e) { imageStatus = L.T("导入失败：") + L.T(e.Message); }
                }
            }
#endif
            bool arrows = GUILayout.Toggle(ShowArrows, L.T("旋转轴与转向"));
            if (arrows != ShowArrows) { ShowArrows = arrows; dirty = true; }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(imageStatus)) GUILayout.Label(imageStatus, small);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Paused ? L.T("播放示意") : L.T("暂停示意"), button)) Paused = !Paused;
            bool enabled = GUI.enabled; GUI.enabled = enabled && LocalSpin.Length > 0.001;
            if (GUILayout.Button(L.T("单步 15°"), button)) Step();
            GUI.enabled = enabled;
            if (GUILayout.Button(Slower ? L.T("极慢") : L.T("慢放"), button)) Slower = !Slower;
            GUILayout.EndHorizontal();

        }
        void SaveSkin() { PlayerPrefs.SetInt(Preference + "Skin", (int)Skin); PlayerPrefs.Save(); }

        public void LoadImage(string path)
        {
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new ArgumentException(L.T("图片请小于 16 MB。"));
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)) || texture.width > 4096 || texture.height > 4096)
                    throw new ArgumentException(L.T("请使用不超过 4096×4096 的 PNG/JPG。"));
                float scale = Mathf.Min(1, 512f / Mathf.Max(texture.width, texture.height));
                int width = Mathf.Max(1, Mathf.RoundToInt(texture.width * scale)), height = Mathf.Max(1, Mathf.RoundToInt(texture.height * scale));
                var next = new Color32[width * height];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) next[y * width + x] = texture.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height);
                imported = next; imageWidth = width; imageHeight = height; Skin = SpinSkin.Image; imageStatus = ""; Reset();
            }
            finally { DestroyTexture(texture); }
        }

        public void Draw(Rect area, BallState? state, GUIStyle text, bool compact = false, bool iconOnly = false)
        {
            LocalSpin = state.HasValue ? state.Value.Spin : new V3();
            if (Event.current.type != EventType.Repaint) return;
            double now = Time.realtimeSinceStartupAsDouble;
            float dt = (float)Math.Min(0.1, Math.Max(0, now - lastDraw)); lastDraw = now;
            float rps = (float)(LocalSpin.Length / BallPhysics.Rps);
            if (!Paused) orientation = Quaternion.AngleAxis(Mathf.Min(Slower ? 0.10f : 0.25f, rps * 0.025f) * dt * 360, SpinAxis(LocalSpin)) * orientation;
            if (dirty || now - lastImage >= 1.0 / 30 || Skin != lastSkin || ShowArrows != lastArrows || state.HasValue != lastValid)
            { RenderFrame(orientation, LocalSpin, state.HasValue); lastImage = now; dirty = false; lastSkin = Skin; lastArrows = ShowArrows; lastValid = state.HasValue; }
            Color previous = GUI.color; GUI.color = state.HasValue ? Color.white : new Color(0.4f, 0.45f, 0.48f);
            GUI.DrawTexture(iconOnly ? area : new Rect(area.x + 6, area.y + 7, compact ? 110 : 126, compact ? 110 : 126), sphere); GUI.color = previous;
            if (iconOnly) return;
            float labelX = compact ? 130 : 145;
            if (!state.HasValue) GUI.Label(new Rect(area.x + labelX, area.y + 23, area.width - labelX, 85), L.T("暂无有效\n球状态"), text);
            else
            {
                GUI.Label(new Rect(area.x + labelX, area.y + 18, area.width - labelX, 30), rps < 0.001f ? L.T("无旋转") : $"{rps:0.0} rps", text);
                string hint = Skin == SpinSkin.BlackDots ? L.T("红线：旋转轴\n绿箭头：球面转向") : Skin == SpinSkin.Image ? L.T("标志贴在球面\n随球转动") : L.T("条纹标记\n随球面转动");
                GUI.Label(new Rect(area.x + labelX, area.y + 52, area.width - labelX, 72), hint, text);
            }
            GUI.Label(new Rect(area.x, area.y + (compact ? 126 : 140), area.width, compact ? 30 : 45), compact ? (FollowMainView ? L.T("跟随主视角 · 慢放示意") : L.T("发球者 → 对方 · 慢放示意")) : (FollowMainView ? L.T("跟随主画面视角") : L.T("固定视角：发球者 → 对方")) + L.T("\n实际 rps · 图中为慢放示意"), text);
        }

        public Texture2D RenderFrame(Quaternion rotation, V3 spin, bool valid = true)
        {
            LocalSpin = spin; Array.Copy(background, pixels, pixels.Length);
            if (valid)
            {
                Quaternion inverse = Quaternion.Inverse(rotation) * ViewRotation;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a == 0) continue;
                    Vector3 p = inverse * normals[i];
                    float ink = 0;
                    if (Skin == SpinSkin.BlackDots || (Skin == SpinSkin.Image && imported == null))
                        for (int j = 0; j < Dots.Length; j++) ink = Mathf.Max(ink, Mathf.Clamp01((Vector3.Dot(p, Dots[j]) - DotThreshold) / 0.003f + 0.5f));
                    else if (Skin == SpinSkin.Band) ink = Mathf.Clamp01((0.075f - Mathf.Abs(Vector3.Dot(p, BandNormal))) / 0.008f + 0.5f);
                    else if (Skin == SpinSkin.Grid) ink = Mathf.Clamp01((0.025f - Mathf.Min(Mathf.Abs(p.x), Mathf.Min(Mathf.Abs(p.y), Mathf.Abs(p.z)))) / 0.008f + 0.5f);
                    Color original = pixels[i], color = Color.Lerp(original, new Color(0.025f, 0.03f, 0.035f, original.a), ink);
                    if (Skin == SpinSkin.Image && imported != null)
                    {
                        // A front-face decal follows curvature and naturally disappears behind the ball.
                        if (p.z < -0.1f)
                        {
                            float halfWidth = 0.85f * imageWidth / Mathf.Max(imageWidth, imageHeight), halfHeight = 0.85f * imageHeight / Mathf.Max(imageWidth, imageHeight);
                            float u = 0.5f + p.x / (-p.z * halfWidth) * 0.5f, v = 0.5f + p.y / (-p.z * halfHeight) * 0.5f;
                            if (u >= 0 && u <= 1 && v >= 0 && v <= 1)
                            {
                                Color image = SampleImage(u, v);
                                color = Color.Lerp(color, new Color(image.r * original.r, image.g * original.g, image.b * original.b, color.a), image.a);
                            }
                        }
                        // A back mark remains useful when the logo faces away.
                        float back = Mathf.Clamp01((Vector3.Dot(p, Vector3.forward) - DotThreshold) / 0.003f + 0.5f);
                        color = Color.Lerp(color, new Color(0.025f, 0.03f, 0.035f, color.a), back);
                    }
                    pixels[i] = color;
                }
                if (ShowArrows && spin.Length > 0.001) DirectionArrows(SpinAxis(spin));
            }
            sphere.SetPixels32(pixels); sphere.Apply(false); return sphere;
        }

        Color SampleImage(float u, float v)
        {
            float x = u * (imageWidth - 1), y = v * (imageHeight - 1); int x0 = (int)x, y0 = (int)y;
            int x1 = Mathf.Min(imageWidth - 1, x0 + 1), y1 = Mathf.Min(imageHeight - 1, y0 + 1);
            return Color.Lerp(Color.Lerp(imported[y0 * imageWidth + x0], imported[y0 * imageWidth + x1], x - x0),
                Color.Lerp(imported[y1 * imageWidth + x0], imported[y1 * imageWidth + x1], x - x0), y - y0);
        }

        void DirectionArrows(Vector3 axis)
        {
            Vector3 right = ViewRotation * Vector3.right, up = ViewRotation * Vector3.up, forward = ViewRotation * Vector3.forward;
            Vector2 Project(Vector3 p) => new Vector2(Half + Vector3.Dot(p, right) * Radius, Half + Vector3.Dot(p, up) * Radius);
            var red = new Color(0.95f, 0.12f, 0.08f);
            // Draw only the parts outside the sphere in 3D. The near end is in front
            // of the visible surface; only the far end is masked by the silhouette.
            Vector3 near = Vector3.Dot(axis, forward) > 0 ? -axis : axis;
            float projectedLength = new Vector2(Vector3.Dot(axis, right), Vector3.Dot(axis, up)).magnitude;
            float extension = Mathf.Min(1.8f, (Half - 5) / (Radius * Mathf.Max(0.001f, projectedLength)));
            Line(Project(-near), Project(-near * extension), red, 6, true);
            Line(Project(near), Project(near * extension), red, 6);
            // One local surface-velocity arrow: omega cross radius, projected into the current view.
            Vector2 center = Vector2.zero, tangent = Vector2.zero;
            foreach (var anchor in new[] { new Vector3(0, 0.6f, -0.8f), new Vector3(0.6f, 0, -0.8f), Vector3.back })
            {
                Vector3 point = ViewRotation * anchor, velocity = Vector3.Cross(axis, point);
                Vector2 projected = new Vector2(Vector3.Dot(velocity, right), Vector3.Dot(velocity, up));
                if (projected.sqrMagnitude > tangent.sqrMagnitude) { center = Project(point); tangent = projected; }
            }
            Vector2 dir = tangent.normalized, side = new Vector2(-dir.y, dir.x), tip = center + dir * 20;
            var green = new Color(0.02f, 0.65f, 0.39f);
            Line(center - dir * 20, tip, green, 4);
            Line(tip, tip - dir * 13 + side * 6, green, 3.5f);
            Line(tip, tip - dir * 13 - side * 6, green, 3.5f);
        }

        void Line(Vector2 a, Vector2 b, Color color, float width, bool outsideBall = false)
        {
            Vector2 delta = b - a; float radius = width * 0.5f + 0.65f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius)), maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius)), maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius));
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
            {
                int i = y * Size + x;
                if (outsideBall && background[i].a > 0) continue;
                var p = new Vector2(x + 0.5f, y + 0.5f); float t = delta.sqrMagnitude > 1e-9f ? Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude) : 0;
                float alpha = Mathf.Clamp01(radius - Vector2.Distance(p, a + delta * t)) * color.a;
                Color old = pixels[i]; float opacity = alpha + old.a * (1 - alpha);
                Color next = opacity > 0 ? (color * alpha + old * (old.a * (1 - alpha))) / opacity : Color.clear;
                next.a = opacity; pixels[i] = next;
            }
        }
        static void DestroyTexture(Texture2D texture)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
        public void Dispose() { DestroyTexture(sphere); }
    }
}
