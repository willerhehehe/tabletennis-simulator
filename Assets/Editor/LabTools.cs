using System.IO;
using TableTennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LabTools
{
    public static void CheckResponsiveLayout()
    {
        var obj = new GameObject("布局检查（临时）"); var lab = obj.AddComponent<PhysicsLab>(); lab.enabled = false;
        var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            foreach (var size in new[] { new Vector2(390, 844), new Vector2(375, 667), new Vector2(844, 390), new Vector2(932, 430) })
            {
                typeof(PhysicsLab).GetField("guiWidth", f).SetValue(lab, size.x); typeof(PhysicsLab).GetField("guiHeight", f).SetValue(lab, size.y);
                var area = (Rect)typeof(PhysicsLab).GetProperty("ViewArea", f).GetValue(lab);
                var panel = (Rect)typeof(PhysicsLab).GetProperty("MobilePanelRect", f).GetValue(lab);
                var spin = (Rect)typeof(PhysicsLab).GetProperty("MobileSpinRect", f).GetValue(lab);
                if (area.width < 250 || area.height < 160 || panel.width < 280 || panel.height < 250 || area.Overlaps(panel) ||
                    panel.x < 0 || panel.y < 0 || panel.xMax > size.x || panel.yMax > size.y || !area.Contains(spin.center))
                    throw new System.Exception("手机布局越界或球台与参数重叠：" + size);
            }
            File.WriteAllText("Artifacts/responsive-layout-checks.txt", "PASS: 390x844, 375x667, 844x390 and 932x430 keep viewport, rotation illustration and scrollable controls within bounds.\n");
        }
        finally { Object.DestroyImmediate(obj); }
    }

    [MenuItem("乒乓球/验证统一球拍设置")]
    public static void CheckEquipmentSettings()
    {
        if (!EditorApplication.isPlaying) throw new System.Exception("请先运行实验场景。");
        var obj = new GameObject("器材设置验证（临时）"); var lab = obj.AddComponent<PhysicsLab>(); lab.enabled = false;
        var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        object Get(string n) => typeof(PhysicsLab).GetField(n, f).GetValue(lab);
        void Set(string n, object v) => typeof(PhysicsLab).GetField(n, f).SetValue(lab, v);
        object Call(string n, params object[] a) => typeof(PhysicsLab).GetMethod(n, f).Invoke(lab, a);
        T Property<T>(string n) => (T)typeof(PhysicsLab).GetProperty(n, f).GetValue(lab);
        void Require(bool ok, string message) { if (!ok) throw new System.Exception(message); }
        var profile = RubberCalibrations.All[1];
        void CheckChoice(bool serving, int responseId)
        {
            var c = (RubberCoefficients)Call("EquipmentCoefficients", serving);
            Require(RubberPresetPanel.Current(c, responseId)?.Id == 2 && profile.Matches(c.Restitution, c.Friction, c.Tangent), "R02 标签与实际系数不一致。");
            Require((int)Call("EquipmentResponse", serving) == responseId, "拟合方式未生效。");
        }
        void CheckStrike()
        {
            var input = (BallState)Get("incoming"); var actual = (BallState)Get("launch");
            var c = (RubberCoefficients)Call("EquipmentCoefficients", false); int id = (int)Call("EquipmentResponse", false);
            var expected = BallPhysics.Strike(input, Property<V3>("RacketNormal"), Property<V3>("RacketVelocity"), c.Restitution, c.Friction, c.Tangent, id);
            Require((actual.Velocity - expected.Velocity).Length < 1e-7 && (actual.Spin - expected.Spin).Length < 1e-7, "单板或接发未使用当前器材。");
        }
        try
        {
            Call("Start"); ((Transform)Get("world")).gameObject.SetActive(false);
            foreach (bool response in new[] { false, true })
            {
                int id = response ? 2 : 0;
                Call("ApplyEquipmentChoice", true, new RubberChoice(profile, response)); CheckChoice(true, id);
                lab.LoadServePreset(ServePresetLibrary.Get(0, 0)); CheckChoice(true, id);
                var contact = (ServeContact)Get("serveContact"); Require(contact != null, "发球触球缺失。");
                var expected = BallPhysics.Strike(contact.Before, contact.Normal, contact.RacketVelocity, profile.Restitution, profile.Friction, profile.Tangent, id);
                Require((expected.Velocity-contact.After.Velocity).Length < 1e-7 && (expected.Spin-contact.After.Spin).Length < 1e-7, "正向发球未使用所选器材。");
                Call("ApplyEquipmentChoice", false, new RubberChoice(profile, response));
                foreach (string action in new[] { "ApplyForehand", "ApplyBackhandFlick", "ApplyServeStudy" })
                {
                    Call(action); lab.Recalculate(); CheckChoice(false, id); CheckStrike();
                }
                Require(((ReceiveStudySettings)Call("StudySettings")).RubberResponseId == id, "扰动评估使用了旧器材响应。");
                Set("mode", 0); Set("speed", 5f); Set("elevation", 8f); Set("spinX", -20f); Set("spinY", 0f); Set("spinZ", 0f); lab.Recalculate();
                contact = (ServeContact)Get("serveContact"); Require(contact != null, "反算示例触球缺失。");
                expected = BallPhysics.Strike(contact.Before, contact.Normal, contact.RacketVelocity, profile.Restitution, profile.Friction, profile.Tangent, id);
                Require((expected.Velocity-contact.After.Velocity).Length < 1e-7 && (expected.Spin-contact.After.Spin).Length < 1e-7, "反算示例没有共用发球拍。");
            }
            Call("SetEquipment", false, new RubberCoefficients(.7, .4, .1), 0);
            Require(RubberPresetPanel.Current((RubberCoefficients)Call("EquipmentCoefficients", false), 0) == null, "手调系数仍冒充标定器材。");
            CheckChoice(true, 2);
            File.WriteAllText("Artifacts/equipment-settings-checks.txt", "PASS: R02 constant and adaptive selections match actual coefficients and response IDs.\nPASS: forward serve, inverse contact, single shot and receiving use the selected racket.\nPASS: action presets preserve explicit equipment choices; receiver changes leave sender unchanged.\nPASS: receiving perturbations retain the response ID; custom coefficients have no preset label.\nEditor integration checks through the same selection handler; pointer input is verified separately.\n");
        }
        finally { Object.Destroy(obj); }
    }

    [MenuItem("乒乓球/验证旋转皮肤")]
    public static void CheckSpinSkins()
    {
        int Dark(Color32[] image) { int n = 0; foreach (var p in image) if (p.a > 128 && p.r < 80 && p.g < 80 && p.b < 80) n++; return n; }
        int Different(Color32[] a, Color32[] b) { int n = 0; for (int i = 0; i < a.Length; i++) if (System.Math.Abs(a[i].r-b[i].r) > 20 || System.Math.Abs(a[i].g-b[i].g) > 20) n++; return n; }
        var report = new System.Text.StringBuilder();
        using (var preview = new SpinPreview())
        {
            Color32[] last = null;
            foreach (SpinSkin skin in new[] { SpinSkin.BlackDots, SpinSkin.Band, SpinSkin.Grid })
            {
                preview.Skin = skin; preview.ShowArrows = false;
                var first = preview.RenderFrame(Quaternion.identity, new V3(0, 40, 0)).GetPixels32();
                var second = preview.RenderFrame(Quaternion.AngleAxis(35, Vector3.up), new V3(0, 40, 0)).GetPixels32();
                if (Dark(first) < 100 || Different(first, second) < 100 || (last != null && Different(first, last) < 100))
                    throw new System.Exception("皮肤无法区分或没有随球面转动：" + skin);
                report.AppendLine("PASS: " + skin + " has distinct markings that move with the sphere."); last = first;
            }
            preview.Skin = SpinSkin.BlackDots; preview.Reset(); preview.Paused = true;
            var spin = new V3(0, 40, 0); preview.RenderFrame(Quaternion.identity, spin); preview.Step();
            var orientation = (Quaternion)typeof(SpinPreview).GetField("orientation", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(preview);
            if (!preview.Paused || Mathf.Abs(Quaternion.Angle(Quaternion.identity, orientation)-15) > 0.001f)
                throw new System.Exception("单步没有暂停并转动 15 度。");
            report.AppendLine("PASS: single step pauses the illustration and advances 15 degrees.");
            var image = new Texture2D(16, 8);
            try
            {
                for (int y = 0; y < 8; y++) for (int x = 0; x < 16; x++) image.SetPixel(x,y,y >= 5 || x == 7 || x == 8 ? Color.black : Color.clear);
                image.Apply(); File.WriteAllBytes("Artifacts/spin-skin-test.png", image.EncodeToPNG());
                preview.LoadImage("Artifacts/spin-skin-test.png");
                var first = preview.RenderFrame(Quaternion.identity, spin).GetPixels32();
                var second = preview.RenderFrame(Quaternion.AngleAxis(45, Vector3.up), spin).GetPixels32();
                if (preview.Skin != SpinSkin.Image || Dark(first) < 100 || Different(first, second) < 100)
                    throw new System.Exception("导入图片没有成为随球面转动的标志。");
                report.AppendLine("PASS: imported PNG is mapped onto the curved rotating surface.");
            }
            finally { if (EditorApplication.isPlaying) Object.Destroy(image); else Object.DestroyImmediate(image); }
        }
        File.WriteAllText("Artifacts/spin-skin-checks.txt", report.ToString());
    }

    [MenuItem("乒乓球/验证常见发球预设")]
    public static void CheckServePresets()
    {
        if (!EditorApplication.isPlaying) throw new System.Exception("请先运行试验场。");
        var obj = new GameObject("预设验证（临时）"); var lab = obj.AddComponent<PhysicsLab>(); lab.enabled = false;
        var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Get(string name) => lab.GetType().GetField(name, f).GetValue(lab);
        void Call(string name) => lab.GetType().GetMethod(name, f).Invoke(lab, null);
        try
        {
            Call("Start"); ((Transform)Get("world")).gameObject.SetActive(false);
            foreach (var preset in ServePresetLibrary.All)
            {
                lab.LoadServePreset(preset); Call("UpdateStrokeVisuals");
                var result = (ServeStrokeResult)Get("strokeResult");
                if (!preset.Matches(result)) throw new System.Exception("预设未按标注生效：" + preset.Name);
                if (((LineRenderer)Get("path")).positionCount < 10 || !((Transform)Get("serveRacket")).gameObject.activeSelf)
                    throw new System.Exception("预设缺少轨迹或可见球拍：" + preset.Name);
                var action = (ServeStroke)Get("stroke"); var c = action.CenterAt(result.Contact.Before.Time);
                if (Vector3.Distance(((Transform)Get("serveRacket")).position, new Vector3((float)c.X, (float)c.Y, (float)c.Z)) > 0.001f)
                    throw new System.Exception("预设未停在触球位置：" + preset.Name);
            }
            int combinations = 0;
            foreach (var rubber in RubberCalibrations.All)
            {
                if (InvertedRubberResponses.Find(rubber.Id) == null) continue;
                foreach (bool adaptive in new[] { false, true })
                    foreach (var preset in ServePresetLibrary.All)
                    {
                        var material = (ServeStroke)Get("stroke");
                        material.Restitution = rubber.Restitution; material.Friction = rubber.Friction; material.Tangent = rubber.Tangent;
                        material.ResponseId = adaptive ? rubber.Id : 0;
                        lab.LoadServePreset(preset); Call("UpdateStrokeVisuals");
                        var result = (ServeStrokeResult)Get("strokeResult"); var action = (ServeStroke)Get("stroke");
                        if (!preset.Matches(result) || result.Flight.NetClearance < 0.015 || action.ResponseId != material.ResponseId ||
                            !rubber.Matches(action.Restitution, action.Friction, action.Tangent))
                            throw new System.Exception("按器材匹配的预设失效：R" + rubber.Id + " " + preset.Name);
                        combinations++;
                    }
            }
            var saved = (ServeStroke)Get("stroke"); saved.Friction = 0; saved.ResponseId = 0;
            var physics = (PhysicsSettings)Get("settings"); physics.TableFriction = 0.15;
            bool rejected = false;
            try { lab.LoadServePreset(ServePresetLibrary.Get(6, 0)); } catch (System.ArgumentException) { rejected = true; }
            if (!rejected || !ReferenceEquals(saved, Get("stroke")) || saved.Friction != 0 || physics.TableFriction != 0.15)
                throw new System.Exception("无法匹配时没有保留原动作与材料。");
            if (combinations != 160) throw new System.Exception("未覆盖全部反胶与拟合模式。");
            File.WriteAllText("Artifacts/serve-preset-validation.csv", ServePresetLibrary.Report(new PhysicsSettings()));
            File.WriteAllText("Artifacts/serve-preset-ui-checks.txt", "PASS: default presets and 160 inverted-rubber/mode combinations load through the public experiment API with labelled spin/length and net clearance.\nPASS: each preset renders a trajectory and a racket at contact.\nPASS: chosen rubber/response and table parameters are preserved; impossible matching leaves the original action intact.\nThese are Editor state/render checks, not native mouse clicks.\n");
        }
        finally { Object.Destroy(obj); }
    }

    [MenuItem("乒乓球/验证旋转示意图")]
    public static void CheckSpinView()
    {
        using (var preview = new SpinPreview { Paused = true, Skin = SpinSkin.BlackDots, ShowArrows = true })
        {
            var spin = new V3(12, 35, -8);
            var baseline = preview.RenderFrame(Quaternion.identity, spin).GetPixels32();
            int RedAxis(Color32[] image) { int n = 0; foreach (var p in image) if (p.a > 128 && p.r > p.g * 2 && p.r > p.b * 2) n++; return n; }
            if (RedAxis(baseline) < 20 || RedAxis(preview.RenderFrame(Quaternion.identity, new V3()).GetPixels32()) != 0)
                throw new System.Exception("旋转轴未显示，或不转球仍显示轴。");

            // The sphere's centre must stay clear; the near external end may overlap its silhouette.
            int centre = 128 * 256 + 128;
            var centrePixel = baseline[centre];
            if (centrePixel.r > centrePixel.g * 2 && centrePixel.r > centrePixel.b * 2)
                throw new System.Exception("旋转轴绘出了球体内部段。");
            foreach (var camera in new[] { Quaternion.Euler(90, 0, 0), Quaternion.Euler(0, 90, 0), Quaternion.Euler(30, 140, 0) })
            {
                preview.SetViewRotation(camera);
                var actual = preview.RenderFrame(Quaternion.identity, spin).GetPixels32();
                // Equivalent projection: keep camera fixed and rotate both body and spin into camera coordinates.
                preview.FollowMainView = false;
                Vector3 local = Quaternion.Inverse(camera) * new Vector3(12, 35, -8);
                var expected = preview.RenderFrame(Quaternion.Inverse(camera), new V3(local.x, local.y, local.z)).GetPixels32();
                int changed = 0, mismatch = 0, arrows = 0;
                for (int i = 0; i < actual.Length; i++)
                {
                    if (!actual[i].Equals(baseline[i])) changed++;
                    if (Mathf.Abs(actual[i].r - expected[i].r) > 3 || Mathf.Abs(actual[i].g - expected[i].g) > 3) mismatch++;
                    if (actual[i].g > actual[i].r * 1.8f && actual[i].g > actual[i].b * 1.15f) arrows++;
                }
                if (changed < 100 || mismatch > 20 || arrows < 20) throw new System.Exception("视角投影或箭头不一致。");
                var fixedImage = preview.RenderFrame(Quaternion.identity, spin).GetPixels32();
                for (int i = 0; i < baseline.Length; i++) if (!fixedImage[i].Equals(baseline[i])) throw new System.Exception("固定视角受主相机影响。");
                preview.FollowMainView = true;
                preview.RenderFrame(Quaternion.identity, spin);
                if ((preview.LocalSpin - spin).Length > 1e-9) throw new System.Exception("视角改变了物理旋转。");
            }
        }
        File.WriteAllText("Artifacts/spin-view-checks.txt", "PASS: paused top, side and orbit views match camera-space surface and arrow projection.\nPASS: fixed sender view ignores camera changes.\nPASS: world angular velocity is unchanged.\n");
    }

    [MenuItem("乒乓球/验证全模式旋转常驻")]
    public static void CheckPersistentSpinPreview()
    {
        var lab = ActiveLab();
        if (lab == null || !EditorApplication.isPlaying) throw new System.Exception("请先运行试验场。");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        lab.StartCoroutine(PersistentSpinCheck(lab));
    }

    static System.Collections.IEnumerator PersistentSpinCheck(PhysicsLab lab)
    {
        var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Get(string n) => lab.GetType().GetField(n, f).GetValue(lab);
        void Set(string n, object v) => lab.GetType().GetField(n, f).SetValue(lab, v);
        object Call(string n, params object[] a) => lab.GetType().GetMethod(n, f).Invoke(lab, a);
        var saved = new System.Collections.Generic.Dictionary<System.Reflection.FieldInfo, object>();
        foreach (var field in lab.GetType().GetFields(f))
            if (!field.IsInitOnly && (field.FieldType.IsValueType || field.FieldType == typeof(string) ||
                field.FieldType == typeof(ServeStroke) || field.FieldType == typeof(PhysicsSettings) ||
                field.FieldType == typeof(ReceiveReport) || field.FieldType == typeof(ReceiveScore))) saved[field] = field.GetValue(lab);
        var camera = (Camera)Get("view"); var pos = camera.transform.position; var rot = camera.transform.rotation;
        bool ortho = camera.orthographic; float size = camera.orthographicSize;
        var preview = (SpinPreview)Get("spinPreview"); bool paused = preview?.Paused ?? false, follow = preview?.FollowMainView ?? true;
        System.Collections.IEnumerator Check(BallState? expected)
        {
            for (int i = 0; i < 12; i++) yield return null;
            var p = (SpinPreview)Get("spinPreview");
            if (p == null || (p.LocalSpin - (expected?.Spin ?? new V3())).Length > 1e-7)
                throw new System.Exception("常驻旋转示意的数据来源错误。");
            var area = (Rect)lab.GetType().GetProperty("SpinPanelRect", f).GetValue(lab);
            var canvas = (Rect)lab.GetType().GetProperty("ViewArea", f).GetValue(lab);
            if (area.x < canvas.xMax - 1 || area.width < 250 || area.y != 0 || area.height < 150)
                throw new System.Exception("旋转面板遮挡了球台或位置不固定。");
            if (Quaternion.Angle(p.ViewRotation, camera.transform.rotation) > 0.001f)
                throw new System.Exception("常驻旋转示意没有跟随主视角。");
        }
        try
        {
            Set("spinSettingsOpen", false); Set("spinPreviewSource", 1); Set("playing", false); Set("servePlaying", false);
            if (preview != null) { preview.Paused = true; preview.FollowMainView = true; }
            Call("ApplyForehand"); lab.Recalculate(); Set("time", -0.06f);
            yield return Check((BallState)Call("StateAt", -0.06f));
            Set("time", 0f); yield return Check((BallState)Get("launch"));
            var bounce = ((FlightResult)Get("flight")).Events.Find(x => x.Kind == "球台");
            float t = (float)bounce.After.Time + 0.00001f; Set("time", t);
            yield return Check((BallState)Call("StateAt", t));
            Set("mode", 0); lab.Recalculate(); Set("time", 0.1f);
            yield return Check((BallState)Call("StateAt", 0.1f));
            Call("ApplyServeStudy"); lab.Recalculate(); Set("time", -0.05f);
            yield return Check((BallState)Call("StateAt", -0.05f));
            Set("time", 0f); yield return Check((BallState)Get("launch"));
            Set("stroke", ((ServeStroke)Get("stroke")).Copy()); ((ServeStroke)Get("stroke")).Friction = 1.5; ((ServeStroke)Get("stroke")).ResponseId = 0; Call("RecalculateServeContact"); Call("OpenServeContact");
            var contact = (ServeContact)Get("serveContact"); if (contact == null) throw new System.Exception("触球示例未匹配。");
            Set("serveTime", -0.01f); yield return Check(BallPhysics.Advance(contact.Before, -0.01, (PhysicsSettings)Get("settings")));
            Set("serveTime", 0.01f); yield return Check(BallPhysics.Advance(contact.After, (float)0.01f, (PhysicsSettings)Get("settings")));
            Set("stroke", new ServeStroke()); Set("strokeAutoContact", true); Call("OpenForwardServe"); lab.Recalculate();
            yield return Check((BallState)Call("CurrentStrokeState"));
            Set("spinPreviewSource", 0); yield return Check(((ServeContact)Get("serveContact")).After);
            Set("strokeAutoContact", false); var miss = new ServeStroke(); miss.Start = miss.End = new V3(4, 1, -2); Set("stroke", miss); lab.Recalculate();
            yield return Check(null);
            File.WriteAllText("Artifacts/persistent-spin-checks.txt", "PASS: direct launch, single shot, receiving and forward serve keep the widget visible.\nPASS: pre-contact, post-contact, table bounce and inverse-contact replay use the displayed ball state.\nPASS: outgoing-only view and missing-contact state do not leak old spin.\nPASS: viewport separation and camera orientation are preserved.\n");
        }
        finally
        {
            foreach (var item in saved) item.Key.SetValue(lab, item.Value);
            Set("keepStudyResults", true); lab.Recalculate();
            foreach (string n in new[] { "time", "playing", "serveTime", "servePlaying" }) Set(n, saved[lab.GetType().GetField(n, f)]);
            if (Get("selectedReceive") is ReceiveScore score) Call("DrawLandingCloud", score);
            camera.orthographic = ortho; camera.orthographicSize = size; camera.transform.SetPositionAndRotation(pos, rot);
            if (preview != null) { preview.Paused = paused; preview.FollowMainView = follow; }
        }
    }

    public static void CheckSpinPreview()
    {
        var lab = ActiveLab();
        if (lab == null || !EditorApplication.isPlaying) throw new System.Exception("请先运行挥拍实验。");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        lab.StartCoroutine(SpinPreviewCheck(lab));
    }

    static System.Collections.IEnumerator SpinPreviewCheck(PhysicsLab lab)
    {
        var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Get(string n) => lab.GetType().GetField(n, f).GetValue(lab);
        void Set(string n, object v) => lab.GetType().GetField(n, f).SetValue(lab, v);
        var contact = (ServeContact)Get("serveContact");
        if (contact == null) throw new System.Exception("需先生成一次有效触球。");
        int source = (int)Get("spinPreviewSource"); float time = (float)Get("time"); bool playing = (bool)Get("playing");
        var result = Get("strokeResult");
        SpinPreview preview = null; SpinSkin savedSkin = SpinSkin.BlackDots; bool savedArrows = false, savedFollow = true;
        try
        {
            Set("playing", false); Set("spinPreviewSource", 0);
            for (int i = 0; i < 12; i++) yield return null;
            preview = (SpinPreview)Get("spinPreview");
            if (preview == null) throw new System.Exception("旋转示意图未创建。");
            savedFollow = preview.FollowMainView; preview.FollowMainView = false;
            savedSkin = preview.Skin; savedArrows = preview.ShowArrows; preview.Skin = SpinSkin.BlackDots; preview.ShowArrows = true;
            for (int i = 0; i < 12; i++) yield return null;
            V3 expected = contact.After.Spin;
            if (preview == null || (preview.LocalSpin - expected).Length > 1e-7)
                throw new System.Exception("出球示意与实际触球后旋转不一致。");
            var forward = preview.ViewRotation * Vector3.forward;
            if (Vector3.Distance(forward, Vector3.forward) > 1e-7f)
                throw new System.Exception("示意图没有固定沿球台长边看向对方。");
            var image = (Texture2D)typeof(SpinPreview).GetField("sphere", f).GetValue(preview);
            int markings = 0, arrows = 0;
            foreach (var pixel in image.GetPixels32())
            {
                if (pixel.a < 128) continue;
                if (pixel.r < 100 && pixel.g < 150 && pixel.b < 150) markings++;
                if (pixel.g > pixel.r * 1.8f && pixel.g > pixel.b * 1.15f) arrows++;
            }
            if (markings < 100 || (expected.Length > 0.01 && arrows < 20))
                throw new System.Exception("球面转动标记或方向箭头没有真正绘入示意球。");
            Set("spinPreviewSource", 1);
            var flight = (FlightResult)Get("flight"); var bounce = flight.Events.Find(e => e.Kind == "球台");
            Set("time", bounce != null ? (float)bounce.After.Time + 0.00001f : 0f);
            for (int i = 0; i < 12; i++) yield return null;
            var state = (BallState)lab.GetType().GetMethod("CurrentStrokeState", f).Invoke(lab, null);
            expected = state.Spin;
            if ((preview.LocalSpin - expected).Length > 1e-7)
                throw new System.Exception("随回放示意没有使用当前时刻的旋转。");
            if (Get("strokeResult") != result) throw new System.Exception("旋转示意修改了物理结果。");
            File.WriteAllText("Artifacts/spin-preview-checks.txt", "PASS: outgoing diagram uses computed post-contact angular velocity in the fixed table frame.\nPASS: viewing direction is fixed along the table from sender to receiver.\nPASS: surface markings and direction arrows are present in the rendered sphere.\nPASS: playback diagram uses current state including table-bounce spin changes.\nPASS: preview does not recalculate or change physics.\n");
        }
        finally { if (preview != null) { preview.Skin = savedSkin; preview.ShowArrows = savedArrows; preview.FollowMainView = savedFollow; } Set("spinPreviewSource", source); Set("time", time); Set("playing", playing); }
    }

    static PhysicsLab ActiveLab()
    {
        foreach (var lab in Object.FindObjectsByType<PhysicsLab>(FindObjectsSortMode.None))
            if (lab.isActiveAndEnabled) return lab;
        return null;
    }

    [MenuItem("乒乓球/验证控制点完整输入事件")]
    public static void CheckStrokePointerEvents()
    {
        var lab = ActiveLab();
        if (lab == null || !EditorApplication.isPlaying) throw new System.Exception("先运行挥拍实验。");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        lab.StartCoroutine(StrokePointerEvents(lab));
    }

    static System.Collections.IEnumerator StrokePointerEvents(PhysicsLab lab)
    {
        var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Get(string n) => lab.GetType().GetField(n, f).GetValue(lab);
        void Set(string n, object v) => lab.GetType().GetField(n, f).SetValue(lab, v);
        object Call(string n, params object[] a) => lab.GetType().GetMethod(n, f).Invoke(lab, a);
        void Queue(Event e) => EditorApplication.delayCall += () => EditorGUIUtility.QueueGameViewInputEvent(e);
        var saved = ((ServeStroke)Get("stroke")).Copy(); bool auto = (bool)Get("strokeAutoContact");
        int mode = (int)Get("mode");
        var camera = (Camera)Get("view");
        var point = Vector2.zero;
        try
        {
            Set("mode", 3); Set("strokeAutoContact", true); Set("strokeCloseup", false); Set("stroke", new ServeStroke());
            Call("SetCamera", 0); lab.Recalculate(); yield return null;
            var before = ((ServeStroke)Get("stroke")).Copy();
            var cameraPosition = camera.transform.position;
            float scale = (float)Get("guiScale");
            point = (Vector2)Call("StrokePointOnScreen", 2) * scale;
            Queue(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
            double until = EditorApplication.timeSinceStartup + 3;
            while ((int)Get("strokeDragging") != 2 && EditorApplication.timeSinceStartup < until) yield return null;
            if ((int)Get("strokeDragging") != 2) throw new System.Exception("圆点未捕获按下事件：" + Get("strokeInputTrace"));
            point += new Vector2(14, -6) * scale;
            Queue(new Event { type = EventType.MouseDrag, button = 0, mousePosition = point, delta = new Vector2(14, -6) * scale });
            until = EditorApplication.timeSinceStartup + 3;
            while ((((ServeStroke)Get("stroke")).End - before.End).Length < 0.01 && EditorApplication.timeSinceStartup < until) yield return null;
            var after = (ServeStroke)Get("stroke");
            if (System.Math.Abs(after.Speed - before.Speed) > 1e-10 || System.Math.Abs(after.Velocity.Length - before.Speed) > 1e-8)
                throw new System.Exception("拖动路径改变了独立设置的挥拍速度。");
            if ((after.End - before.End).Length < 0.01 || (after.Start - before.Start).Length < 0.01 || Get("serveContact") == null)
                throw new System.Exception("完整拖动没有移动两端并保持触球。");
            var hit = (ServeContact)Get("serveContact");
            if (System.Math.Abs((float)Get("time") - hit.Before.Time) > 0.001)
                throw new System.Exception("拖动后没有停在触球时刻。");
            Call("UpdateStrokeVisuals");
            var pose = ((Transform)Get("serveRacket")).position;
            V3 center = after.CenterAt(hit.Before.Time);
            if (Vector3.Distance(pose, new Vector3((float)center.X, (float)center.Y, (float)center.Z)) > 0.001)
                throw new System.Exception("拖动时球拍没有显示在触球位置。");
            if (camera.transform.position != cameraPosition) throw new System.Exception("拖动圆点同时移动了相机。");
            Queue(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
            until = EditorApplication.timeSinceStartup + 3;
            while ((int)Get("strokeDragging") != -1 && EditorApplication.timeSinceStartup < until) yield return null;
            if ((int)Get("strokeDragging") != -1) throw new System.Exception("松开后仍占用拖动。");
            File.WriteAllText("Artifacts/stroke-pointer-events.txt", "PASS: game input queue MouseDown captures the visible endpoint at current DPI.\nPASS: MouseDrag moves the endpoint and linked start, recalculates contact, and leaves the camera fixed.\nPASS: MouseUp releases capture.\n" + Get("strokeInputTrace") + "\nThis uses Unity input events, not OS-level mouse automation.\n");
        }
        finally
        {
            EditorGUIUtility.QueueGameViewInputEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
            Set("stroke", saved); Set("strokeAutoContact", auto); Set("mode", mode); lab.Recalculate(); Call("SetCamera", 0);
        }
    }

    [MenuItem("乒乓球/验证发球控制点拖动")]
    public static void CheckStrokeDragging()
    {
        var lab = ActiveLab();
        if (lab == null || !EditorApplication.isPlaying) throw new System.Exception("先运行挥拍实验。");
        var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Get(string n) => lab.GetType().GetField(n, f).GetValue(lab);
        void Set(string n, object v) => lab.GetType().GetField(n, f).SetValue(lab, v);
        object Call(string n, params object[] a)
        {
            var method = lab.GetType().GetMethod(n, f);
            if (method == null) throw new System.Exception("FAIL: dragging method missing: " + n);
            return method.Invoke(lab, a);
        }
        void Require(bool ok, string message) { if (!ok) throw new System.Exception(message); }
        var saved = ((ServeStroke)Get("stroke")).Copy();
        int mode = (int)Get("mode"), cameraMode = (int)Get("cameraMode");
        bool closeup = (bool)Get("strokeCloseup");
        var camera = (Camera)Get("view");
        var position = camera.transform.position; var rotation = camera.transform.rotation;
        float size = camera.orthographicSize; bool adjusted = (bool)Get("cameraAdjusted");
        try
        {
            Set("mode", 3); Set("strokeCloseup", false);
            foreach (int point in new[] { 0, 1, 2 })
            {
                Set("stroke", saved.Copy()); Call("SetCamera", 1); lab.Recalculate();
                V3 before = (V3)Call("StrokePoint", point);
                Vector2 mouse = (Vector2)Call("StrokePointOnScreen", point);
                Require((bool)Call("BeginStrokePointDrag", point, mouse, false), "无法从俯视图开始拖动控制点。");
                Call("MoveStrokePoint", mouse + new Vector2(22, 16));
                V3 after = (V3)Call("StrokePoint", point);
                Require((after - before).Length > 0.02 && System.Math.Abs(after.Y - before.Y) < 1e-6,
                    "俯视拖动应改变水平位置并保持高度。");
            }
            Set("stroke", saved.Copy()); Call("SetCamera", 2); lab.Recalculate();
            V3 sideBefore = (V3)Call("StrokePoint", 1); Vector2 sideMouse = (Vector2)Call("StrokePointOnScreen", 1);
            Require((bool)Call("BeginStrokePointDrag", 1, sideMouse, false), "无法从侧视开始拖动。");
            Call("MoveStrokePoint", sideMouse + new Vector2(20, -20));
            V3 sideAfter = (V3)Call("StrokePoint", 1);
            Require(sideAfter.Y > sideBefore.Y && System.Math.Abs(sideAfter.X - sideBefore.X) < 1e-6, "侧视拖动高度或固定左右坐标错误。");
            Set("stroke", saved.Copy()); Call("SetCamera", 0); lab.Recalculate();
            Vector2 m = (Vector2)Call("StrokePointOnScreen", 2); V3 b = (V3)Call("StrokePoint", 2);
            Require((bool)Call("BeginStrokePointDrag", 2, m, true), "三维高度拖动不可用。");
            Call("MoveStrokePoint", m + new Vector2(0, -22)); V3 a2 = (V3)Call("StrokePoint", 2);
            Require(a2.Y > b.Y && System.Math.Abs(a2.X - b.X) < 1e-6 && System.Math.Abs(a2.Z - b.Z) < 1e-6,
                "Shift 应只修改高度。");
            Require(!(bool)Call("BeginStrokePointDrag", 0, Vector2.zero, false), "侧栏不能启动控制点拖动。");
            Require(Get("strokeResult") != null && (bool)Get("cameraAdjusted"), "拖动后未重算或镜头未锁定。");
            File.WriteAllText("Artifacts/stroke-drag-checks.txt", "PASS: all three top-view handles move horizontally without changing height.\nPASS: side view changes height and depth without lateral drift.\nPASS: Shift in 3D changes height only.\nPASS: sidebar excluded; dragging recalculates physics and fixes the camera.\nThese are coordinate/state checks, not native mouse automation.\n");
        }
        finally
        {
            if (lab.GetType().GetField("strokeDragging", f) != null) Set("strokeDragging", -1);
            Set("stroke", saved); Set("mode", mode); Set("strokeCloseup", closeup); Set("cameraMode", cameraMode);
            lab.Recalculate(); camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size; Set("cameraAdjusted", adjusted);
        }
    }
    [MenuItem("乒乓球/打开物理试验场")]
    public static void OpenLab()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists("Assets/Scenes/PhysicsLab.unity")) CreateScene();
        else EditorSceneManager.OpenScene("Assets/Scenes/PhysicsLab.unity");
    }

    public static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Physics Lab").AddComponent<PhysicsLab>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/PhysicsLab.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/PhysicsLab.unity", true) };
        PlayerSettings.companyName = "Willer"; PlayerSettings.productName = "乒乓球物理试验场";
        PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        AssetDatabase.SaveAssets();
    }

    [MenuItem("乒乓球/运行物理检查")]
    public static void Check()
    {
        string report = PhysicsChecks.Run();
        Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/physics-checks.txt", report);
        Debug.Log(report);
    }

    // unity open <path> --args '-batchmode -executeMethod LabTools.SetupAndCheck -quit'
    public static void SetupAndCheck() { CreateScene(); Check(); }

    public static void PlayForReview()
    {
        OpenLab(); Check();
        EditorApplication.delayCall += () =>
        {
            var game = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            ConfigureClearPreview();
            game.maximized = true;
            game.Focus();
            EditorApplication.isPlaying = true;
        };
    }

    [MenuItem("乒乓球/原生清晰度预览")]
    public static void ConfigureClearPreview()
    {
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var game = EditorWindow.GetWindow(type);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        type.GetProperty("lowResolutionForAspectRatios", flags).SetValue(game, false);
        game.Repaint();
    }

    [MenuItem("乒乓球/导出当前轨迹")]
    public static void Export()
    {
        var lab = ActiveLab();
        if (EditorApplication.isPlaying && lab != null) lab.Export();
        else Debug.LogWarning("请先进入 Play 模式。");
    }

    [MenuItem("乒乓球/验证界面交互")]
    public static void CheckInterface()
    {
        var lab = ActiveLab();
        if (!EditorApplication.isPlaying || lab == null) throw new System.InvalidOperationException("请先运行接发研究场景。");
        var type = lab.GetType();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Get(string name) => type.GetField(name, flags).GetValue(lab);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(lab, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(lab, args);
        if ((int)Get("mode") != 2 || Get("reception") == null || ((ReceiveStudyRunner)Get("receiveRunner")).IsRunning)
            throw new System.InvalidOperationException("需要有效且未在搜索中的接发研究场景。");
        float delay = (float)Get("receiveDelay"), time = (float)Get("time");
        bool playing = (bool)Get("playing"), dirty = (bool)Get("studyDirty");
        int cameraMode = (int)Get("cameraMode"), tab = (int)Get("receiveTab");
        object report = Get("receiveReport"), selected = Get("selectedReceive"), notice = Get("notice");
        try
        {
            Call("BeginPointPicking");
            var points = (System.Collections.Generic.List<BallState>)Get("pickStates");
            if (points.Count < 2) throw new System.Exception("可选接球轨迹为空。");
            BallState target = points[points.Count / 2];
            var camera = (Camera)Get("view");
            var area = (Rect)type.GetProperty("ViewArea", flags).GetValue(lab);
            Vector3 screen = camera.WorldToViewportPoint(new Vector3((float)target.Position.X, (float)target.Position.Y, (float)target.Position.Z));
            Vector2 point = new Vector2(area.x + screen.x * area.width, area.y + (1 - screen.y) * area.height);
            if (!lab.TrySelectReceivePoint(point)) throw new System.Exception("未能从可见轨迹选中接球点。");
            var serve = (FlightResult)Get("serveFlight");
            double expectedDelay = target.Time - serve.Events[1].After.Time;
            if (System.Math.Abs((float)Get("receiveDelay") - expectedDelay) > 0.003 || (float)Get("time") != 0)
                throw new System.Exception("选点未正确更新时刻或暂停在触球位置。");
            if (Get("receiveReport") != null) throw new System.Exception("选点后旧统计没有失效。");
            float selectedDelay = (float)Get("receiveDelay");
            Call("BeginPointPicking");
            if (lab.TrySelectReceivePoint(new Vector2(area.x - 10, area.y - 10)) || (float)Get("receiveDelay") != selectedDelay)
                throw new System.Exception("画布外点击改变了接球点。");
            Call("EndPointPicking", false);
            Call("TogglePlayback"); if (!(bool)Get("playing")) throw new System.Exception("播放没有启动。");
            Call("TogglePlayback"); if ((bool)Get("playing")) throw new System.Exception("暂停没有生效。");
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/ui-interaction-checks.txt", "PASS: visible trajectory point updates contact time and pauses at contact.\nPASS: changed point invalidates old statistics.\nPASS: outside-canvas clicks preserve input.\nPASS: playback toggle starts and pauses.\n");
            Debug.Log("界面交互检查通过：轨迹选点、统计失效、画布边界、播放暂停。");
        }
        finally
        {
            Call("EndPointPicking", false); Set("receiveDelay", delay); lab.Recalculate(); Set("receiveReport", report);
            if (selected != null) Call("ApplyReceiveScore", selected);
            Set("receiveTab", tab); Set("studyDirty", dirty); Call("SetCamera", cameraMode);
            Set("time", time); Set("playing", playing); Set("notice", notice);
        }
    }

    [MenuItem("乒乓球/验证拍型与回放")]
    public static void CheckPresentation()
    {
        var lab = ActiveLab();
        if (!EditorApplication.isPlaying || lab == null) throw new System.InvalidOperationException("请先运行实验场景。");
        lab.StartCoroutine(CheckPresentationRoutine(lab));
    }

    static System.Collections.IEnumerator CheckPresentationRoutine(PhysicsLab lab)
    {
        var type = lab.GetType();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Get(string name) => type.GetField(name, flags).GetValue(lab);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(lab, value);
        float time = (float)Get("time"); bool playing = (bool)Get("playing");
        if (Get("flight") == null || ((ReceiveStudyRunner)Get("receiveRunner")).IsRunning) throw new System.InvalidOperationException("需要有效且未在评估的场景。");
        try
        {
            Set("playing", false); Set("time", 0.003f);
            yield return new WaitForSecondsRealtime(0.3f);
            if (Mathf.Abs((float)Get("time") - 0.003f) > 0.000001f)
                throw new System.Exception("没有输入时回放位置被重置。");
            var racket = (Transform)Get("racket");
            V3 normal = (V3)type.GetProperty("RacketNormal", flags).GetValue(lab);
            V3 velocity = (V3)type.GetProperty("RacketVelocity", flags).GetValue(lab);
            Vector3 n = new Vector3((float)normal.X, (float)normal.Y, (float)normal.Z);
            Vector3 v = new Vector3((float)velocity.X, (float)velocity.Y, (float)velocity.Z);
            if (Vector3.Distance(racket.Find("胶皮").up, n) > 0.00001f)
                throw new System.Exception("可见胶皮法向与碰撞法向不一致。");
            RacketGeometry.Axes(normal, (float)Get("racketRoll"), out _, out V3 up);
            if (Vector3.Distance(racket.up, new Vector3((float)up.X, (float)up.Y, (float)up.Z)) > 0.00001f)
                throw new System.Exception("可见拍柄姿态与碰台筛选几何不一致。");
            Vector3 center = (Vector3)Get("contactRacket");
            if (Vector3.Distance(racket.position, center + v * 0.003f) > 0.00001f)
                throw new System.Exception("局部挥拍位移与触球速度不一致。");
            var arrow = (LineRenderer)Get("racketArrow");
            if ((bool)Get("showGuides") && v.sqrMagnitude > 0 &&
                (arrow.positionCount < 2 || Vector3.Dot((arrow.GetPosition(1) - arrow.GetPosition(0)).normalized, v.normalized) < 0.9999f))
                throw new System.Exception("挥拍箭头与实际接触点速度不一致。");
            Set("playing", true);
            yield return new WaitForSecondsRealtime(0.3f);
            if ((float)Get("time") <= 0.003f) throw new System.Exception("播放未前进。");
            File.WriteAllText("Artifacts/racket-playback-checks.txt", "PASS: idle GUI preserves paused time across frames.\nPASS: rendered face and handle match collision geometry.\nPASS: local racket displacement and arrow match contact velocity.\nPASS: playback advances across frames.\n");
            Debug.Log("拍型与回放检查通过。");
        }
        finally { Set("time", time); Set("playing", playing); }
    }

    [MenuItem("乒乓球/验证接法研究流程")]
    public static void CheckReceiveWorkflow()
    {
        var active = ActiveLab();
        if (!EditorApplication.isPlaying || active == null) throw new System.InvalidOperationException("请先运行实验场景。");
        active.StartCoroutine(CheckReceiveWorkflowRoutine());
    }

    [MenuItem("乒乓球/验证视角控制")]
    public static void CheckCameraControls()
    {
        var lab = ActiveLab();
        if (!EditorApplication.isPlaying || lab == null) throw new System.InvalidOperationException("请先运行实验场景。");
        var type = lab.GetType();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Get(string name) => type.GetField(name, flags).GetValue(lab);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(lab, value);
        object Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(lab, args);
        void Require(bool condition, string message) { if (!condition) throw new System.Exception(message); }
        var camera = (Camera)Get("view");
        var position = camera.transform.position; var rotation = camera.transform.rotation;
        bool orthographic = camera.orthographic; float size = camera.orthographicSize, fov = camera.fieldOfView;
        string[] fields = { "mode", "cameraMode", "cameraPivot", "cameraAdjusted", "cameraDragButton", "cameraDragControl", "fittedAspect", "pickingReceivePoint", "showTelemetry" };
        object[] saved = System.Array.ConvertAll(fields, Get);
        object flight = Get("flight"), report = Get("receiveReport"), time = Get("time");
        try
        {
            Set("mode", 0); Set("pickingReceivePoint", false); Call("SetCamera", 0);
            Vector3 initial = camera.transform.position, pivot = (Vector3)Get("cameraPivot");
            float distance = Vector3.Distance(initial, pivot);
            Call("OrbitCamera", new Vector2(80, 30));
            Require(Vector3.Distance(initial, camera.transform.position) > 0.1f &&
                Mathf.Abs(Vector3.Distance(camera.transform.position, pivot) - distance) < 0.0001f, "旋转没有围绕观察目标保持距离。");
            Vector3 beforePan = camera.transform.position; Call("PanCamera", new Vector2(30, -20));
            Require(Vector3.Distance(beforePan, camera.transform.position) > 0 &&
                Vector3.Distance(camera.transform.position - beforePan, (Vector3)Get("cameraPivot") - pivot) < 0.0001f, "平移没有同步移动镜头和观察目标。");
            Call("ZoomCamera", -2f);
            Require(Vector3.Distance(camera.transform.position, (Vector3)Get("cameraPivot")) < distance, "透视缩放未拉近镜头。");
            Vector3 adjusted = camera.transform.position; Call("FitCamera");
            Require(camera.transform.position == adjusted, "自动适配覆盖了手动视角。");
            Call("ResetCameraView"); Require(Vector3.Distance(camera.transform.position, initial) < 0.0001f, "复位未恢复预设视角。");
            Call("SetCamera", 1); float oldSize = camera.orthographicSize;
            Call("ZoomCamera", -2f); Require(camera.orthographicSize < oldSize, "正交缩放无效。");
            for (int i = 0; i < 40; i++) Call("ZoomCamera", -10f);
            Require(camera.orthographicSize >= 0.025f && camera.orthographicSize < 0.026f, "缩放下限失效。");
            for (int i = 0; i < 40; i++) Call("ZoomCamera", 10f);
            Require(camera.orthographicSize <= 8, "缩放上限失效。");
            var area = (Rect)type.GetProperty("ViewArea", flags).GetValue(lab);
            var middle = new Vector2(area.center.x, area.center.y);
            Set("showTelemetry", false);
            Require(!(bool)Call("CanStartCameraDrag", new Vector2(area.x - 10, area.center.y), 0), "侧栏输入被视角控制捕获。");
            Set("pickingReceivePoint", true);
            Require(!(bool)Call("CanStartCameraDrag", middle, 0), "左键视角拖动抢占了接球选点。");
            Require(Get("flight") == flight && Get("receiveReport") == report && Get("time").Equals(time), "视角控制修改了实验或回放状态。");
            File.WriteAllText("Artifacts/camera-control-checks.txt", "PASS: orbit preserves pivot distance.\nPASS: pan translates camera and pivot together.\nPASS: perspective and orthographic zoom work within limits.\nPASS: manual view survives automatic fitting and resets correctly.\nPASS: sidebar and left-button receiving selection are not captured.\nPASS: physics, report and playback state remain unchanged.\n");
            Debug.Log("视角控制检查通过。");
        }
        finally
        {
            for (int i = 0; i < fields.Length; i++) Set(fields[i], saved[i]);
            camera.orthographic = orthographic; camera.orthographicSize = size; camera.fieldOfView = fov;
            camera.transform.SetPositionAndRotation(position, rotation);
        }
    }

    [MenuItem("乒乓球/验证触球与轨迹同屏")]
    public static void CheckCombinedServeView()
    {
        var lab = ActiveLab();
        if (!EditorApplication.isPlaying || lab == null) throw new System.InvalidOperationException("请先运行出球或接发研究场景。");
        var type = lab.GetType(); var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Get(string name) => type.GetField(name, flags).GetValue(lab);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(lab, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(lab, args);
        void Require(bool condition, string message) { if (!condition) throw new System.Exception(message); }
        if ((int)Get("mode") == 3) { CheckForwardStroke(); return; }
        Require((int)Get("mode") != 1 && !((ReceiveStudyRunner)Get("receiveRunner")).IsRunning, "需要未在评估中的出球或接发研究场景。");
        var camera = (Camera)Get("view"); Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        bool orthographic = camera.orthographic; float size = camera.orthographicSize, fov = camera.fieldOfView;
        string[] fields = { "cameraMode", "cameraPivot", "cameraAdjusted", "cameraDragButton", "cameraDragControl", "fittedAspect", "servePlaying", "serveTime", "serveRouteView", "showServeTrajectory" };
        object[] saved = System.Array.ConvertAll(fields, Get);
        object flight = Get("flight"), report = Get("receiveReport"), time = Get("time");
        try
        {
            Set("servePlaying", false); Call("SetCamera", 4); Set("showServeTrajectory", true); Call("UpdateServeVisuals");
            var lines = new[] { (LineRenderer)Get("path"), (LineRenderer)Get("incomingPath"), (LineRenderer)Get("reference") };
            Require(lines[0].enabled && lines[1].enabled && lines[2].enabled, "触球视图仍隐藏了轨迹。");
            Require(lines[0].positionCount + lines[1].positionCount > 1, "没有可验证的轨迹。");
            Require(((Transform)Get("serveRacket")).gameObject.activeSelf, "显示轨迹时球拍或未匹配位置参照被隐藏。");
            if (Get("serveContact") == null) Require(!((LineRenderer)Get("serveSwing")).enabled && !string.IsNullOrEmpty((string)Get("serveContactError")), "无解状态显示了未经验证的挥拍。");
            foreach (var marker in (System.Collections.Generic.List<GameObject>)Get("markers")) Require(marker.activeSelf, "碰撞标记被隐藏。");
            for (int orientation = 0; orientation < 3; orientation++)
            {
                Set("serveRouteView", orientation); Call("FitServeTrajectory");
                Require((int)Get("cameraMode") == 4, "适配球路退出了触球视图。");
                foreach (var line in lines)
                    for (int i = 0; i < line.positionCount; i++)
                    {
                        Vector3 point = camera.WorldToViewportPoint(line.GetPosition(i));
                        Require(point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1, "完整球路未被包含在视口内。");
                    }
            }
            Set("showServeTrajectory", false); Call("UpdateServeVisuals");
            Require(!lines[0].enabled && !lines[1].enabled && !lines[2].enabled, "隐藏轨迹开关无效。");
            Require(((Transform)Get("serveRacket")).gameObject.activeSelf, "隐藏轨迹错误地隐藏了球拍或位置参照。");
            Require(Get("flight") == flight && Get("receiveReport") == report && Get("time").Equals(time), "显示切换改变了实验数据。");
            File.WriteAllText("Artifacts/combined-serve-view-checks.txt", "PASS: racket, paths and collision markers are visible together.\nPASS: all path points fit in top, side and 3D overviews without leaving serve view.\nPASS: trajectory toggle preserves the racket.\nPASS: physics, report and playback state are unchanged.\n");
            Debug.Log("触球与轨迹同屏检查通过。");
        }
        finally
        {
            for (int i = 0; i < fields.Length; i++) Set(fields[i], saved[i]);
            camera.orthographic = orthographic; camera.orthographicSize = size; camera.fieldOfView = fov;
            camera.transform.SetPositionAndRotation(position, rotation); Call("UpdateServeVisuals");
        }
    }

    static System.Collections.IEnumerator CheckReceiveWorkflowRoutine()
    {
        var obj = new GameObject("接法流程检查（临时）");
        var lab = obj.AddComponent<PhysicsLab>(); lab.enabled = false;
        var type = lab.GetType();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Owner(string name) => name == "receiveTask" || name == "receiveCancellation" ? type.GetField("receiveRunner", flags).GetValue(lab) : (object)lab;
        object Get(string name) => Owner(name).GetType().GetField(name, flags).GetValue(Owner(name));
        void Set(string name, object value) => Owner(name).GetType().GetField(name, flags).SetValue(Owner(name), value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(lab, args);
        void Require(bool condition, string error) { if (!condition) throw new System.Exception(error); }
        System.Collections.IEnumerator FinishTask()
        {
            var task = (System.Threading.Tasks.Task<ReceiveReport>)Get("receiveTask");
            double until = EditorApplication.timeSinceStartup + 30;
            while (!task.IsCompleted && EditorApplication.timeSinceStartup < until) yield return null;
            Require(task.IsCompleted, "评估未在 30 秒内完成。"); Call("UpdateReceiveTask");
        }
        try
        {
            Call("Start"); ((Transform)Get("world")).gameObject.SetActive(false); Call("ApplyServeStudy");
            Set("facePitch", 0f); Set("racketForward", -3f); Set("racketUp", 0f); lab.Recalculate();
            Require(Get("reception") != null && Get("flight") == null && (bool)type.GetProperty("CanEvaluateReceive", flags).GetValue(lab),
                "无效手动拍型阻止了有效来球的评估。");
            lab.TestCurrentReceive(); yield return FinishTask();
            var failed = (ReceiveReport)Get("receiveReport");
            Require(failed != null && failed.Baseline.Successes == 0 && Get("selectedReceive") == failed.Baseline, "失败接法没有显示自己的测试结果。");
            lab.ExportReceiveReport();
            Require(!((string)Get("notice")).Contains("失败"), "失败接法的报告不能导出。");
            Set("facePitch", 10f); Set("racketForward", 0.5f); Set("racketUp", 4f); lab.Recalculate();
            lab.TestCurrentReceive(); yield return FinishTask();
            var previous = (ReceiveReport)Get("receiveReport"); object selected = Get("selectedReceive");
            Require(previous != null && previous.Baseline.Successes > 0, "当前接法测试没有产生有效结果。");
            lab.StartReceiveSearch();
            Require(Get("receiveReport") == previous && Get("selectedReceive") == selected, "开始重评丢掉了上一轮结果。");
            ((System.Threading.CancellationTokenSource)Get("receiveCancellation")).Cancel(); yield return FinishTask();
            Require(Get("receiveReport") == previous && Get("selectedReceive") == selected, "取消重评未保留上一轮结果。");
            var cancellation = new System.Threading.CancellationTokenSource(); cancellation.Cancel();
            Set("receiveCancellation", cancellation); Set("receiveTask", System.Threading.Tasks.Task.FromResult(failed)); Call("UpdateReceiveTask");
            Require(Get("receiveReport") == previous, "完成与取消同时发生时覆盖了保留结果。");
            Set("receiveCancellation", new System.Threading.CancellationTokenSource());
            Set("receiveTask", System.Threading.Tasks.Task.FromException<ReceiveReport>(new System.Exception("测试故障"))); Call("UpdateReceiveTask");
            Require(Get("receiveReport") == previous, "失败重评丢掉了上一轮结果。");
            lab.StartReceiveSearch(); yield return FinishTask();
            var searched = (ReceiveReport)Get("receiveReport");
            Require(searched != null && searched.Ranked.Count > 1 && searched.Baseline.Action.Equals(previous.Baseline.Action), "搜索未保留手调基线。");
            Call("ApplyReceiveScore", searched.Baseline);
            Require(Get("receiveReport") == searched && Get("selectedReceive") == searched.Baseline, "恢复手调基线破坏了候选列表。");
            Set("receiveCancellation", new System.Threading.CancellationTokenSource());
            Set("receiveTask", System.Threading.Tasks.Task.FromResult(failed)); Call("UpdateReceiveTask");
            Require(Get("receiveReport") == failed && Get("selectedReceive") == failed.Baseline, "新零分结果仍显示旧候选。");
            File.WriteAllText("Artifacts/receive-workflow-checks.txt", "PASS: invalid manual contact remains assessable and exportable.\nPASS: current-only test evaluates one action.\nPASS: cancel, completion race and fault preserve prior results.\nPASS: search and baseline comparison retain the original baseline.\nPASS: zero-score completion selects its own result.\n");
            Debug.Log("接法研究流程检查通过。");
        }
        finally { Object.Destroy(obj); }
    }

    [MenuItem("乒乓球/验证球拍缺失回归")]
    public static void CheckMissingRacketRegression()
    {
        if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("请先运行实验场景。");
        var obj = new GameObject("球拍缺失回归（临时）");
        var lab = obj.AddComponent<PhysicsLab>(); lab.enabled = false;
        var type = lab.GetType(); var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Get(string name) => type.GetField(name, flags).GetValue(lab);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(lab, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(lab, args);
        void Require(bool condition, string message) { if (!condition) throw new System.Exception(message); }
        try
        {
            Call("Start"); Set("mode", 0); Set("speed", 5f); Set("elevation", 8f); Set("direction", 0f);
            Set("spinX", -40f); Set("spinY", 0f); Set("spinZ", 0f); ((ServeStroke)Get("stroke")).Friction = 0.8;
            lab.Recalculate(); Call("OpenServeContact"); Call("UpdateServeVisuals");
            var solved = (ServeContact)Get("serveContact");
            Require(solved != null, "截图中的 5 m/s、8°、40 rps、μ=0.8 仍然无解。");
            var racket = (Transform)Get("serveRacket");
            Require(racket.gameObject.activeSelf && ((LineRenderer)Get("serveSwing")).enabled, "有解时球拍或挥拍箭头不可见。");
            var target = (BallState)type.GetProperty("ServeTarget", flags).GetValue(lab);
            Require((solved.After.Velocity - target.Velocity).Length < 1e-7 && (solved.After.Spin - target.Spin).Length < 1e-6,
                "重算没有复现截图目标。");
            Require((float)Get("speed") == 5 && (float)Get("spinX") == -40 && ((ServeStroke)Get("stroke")).Friction == 0.8, "修复偷偷修改了目标或摩擦参数。");
            Call("FitServeTrajectory"); Call("UpdateServeVisuals");
            var camera = (Camera)Get("view");
            Require(racket.Find("胶皮").GetComponent<Renderer>().sharedMaterial.color.a >= 0.8f, "全景球拍仍过于透明。");
            Vector3 normal = new Vector3((float)solved.Normal.X, (float)solved.Normal.Y, (float)solved.Normal.Z);
            Require(Mathf.Abs(Vector3.Dot(normal, -camera.transform.forward)) >= 0.35f, "全景球拍恰好侧面对镜头，无法辨认。");
            var firstFlight = (FlightResult)Get("flight"); Vector3 finalPoint = new Vector3((float)firstFlight.Samples[firstFlight.Samples.Count - 1].Position.X,
                (float)firstFlight.Samples[firstFlight.Samples.Count - 1].Position.Y, (float)firstFlight.Samples[firstFlight.Samples.Count - 1].Position.Z);
            ((ServeStroke)Get("stroke")).Friction = 0; lab.Recalculate(); Call("UpdateServeVisuals");
            Require(Get("serveContact") == null && racket.gameObject.activeSelf, "无解时没有保留位置参照。");
            Require(!((LineRenderer)Get("serveSwing")).enabled && !((LineRenderer)Get("serveBrush")).enabled &&
                !((LineRenderer)Get("serveNormal")).enabled && !((Transform)Get("serveTouch")).gameObject.activeSelf,
                "无解时显示了未经验证的挥拍、摩擦或触点。");
            Require(!string.IsNullOrEmpty((string)Get("serveContactError")) && racket.Find("胶皮").GetComponent<Renderer>().sharedMaterial.color.g > 0.5f,
                "位置参照未明确显示为失败状态。");
            var path = (LineRenderer)Get("path");
            Require(path.enabled && Vector3.Distance(path.GetPosition(path.positionCount - 1), finalPoint) < 1e-5 && (float)Get("spinX") == -40,
                "失败状态改变了目标旋转或球路。");
            ((ServeStroke)Get("stroke")).Friction = 0.8; lab.Recalculate(); Call("UpdateServeVisuals");
            Require(Get("serveContact") != null && ((LineRenderer)Get("serveSwing")).enabled, "从失败恢复后动作仍不可见。");
            File.WriteAllText("Artifacts/missing-racket-regression-checks.txt", "PASS: exact screenshot parameters solve and render without changing speed, spin or friction.\nPASS: wide view keeps the racket opaque and avoids an edge-on view.\nPASS: failed solve retains a labelled grey position reference with no invented motion/contact arrows.\nPASS: failure preserves the target trajectory and recovers when conditions become feasible.\n");
            Debug.Log("球拍缺失回归检查通过。");
        }
        finally { Object.Destroy(obj); }
    }

    [MenuItem("乒乓球/验证正向挥拍发球")]
    public static void CheckForwardStroke()
    {
        if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("请先运行实验场景。");
        var obj = new GameObject("正向挥拍检查（临时）");
        var lab = obj.AddComponent<PhysicsLab>(); lab.enabled = false;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = lab.GetType();
        object Get(string name) => type.GetField(name, flags).GetValue(lab);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(lab, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(lab, args);
        void Require(bool ok, string message) { if (!ok) throw new System.Exception(message); }
        try
        {
            Call("Start"); Call("UpdateServeVisuals");
            var stroke = (ServeStroke)Get("stroke"); var original = stroke.Copy();
            var racket = (Transform)Get("serveRacket");
            foreach (double angle in new[] { 0.0, 40.0, 90.0 })
            {
                stroke.TableAngle = angle; lab.Recalculate(); Call("UpdateServeVisuals");
                Vector3 n = new Vector3((float)stroke.Normal.X, (float)stroke.Normal.Y, (float)stroke.Normal.Z);
                Require(racket.gameObject.activeSelf && Vector3.Distance(racket.forward, n) < 1e-5f,
                    $"{angle}° 没有同步到可见球拍法向。");
                Require(Vector3.Distance(racket.Find("胶皮").up, n) < 1e-5f, "黑色胶皮与碰撞面法向不一致。");
            }
            Set("stroke", original); lab.Recalculate(); Call("UpdateServeVisuals");
            var result = (ServeStrokeResult)Get("strokeResult");
            Require(result.TwoBounces && !result.MayTouchTable, "默认动作不能用于接发。");
            var black = racket.Find("胶皮").GetComponent<Renderer>().sharedMaterial.color;
            var red = racket.Find("背面").GetComponent<Renderer>().sharedMaterial.color;
            Require(black.r < 0.1f && black.g < 0.1f && red.r > 0.6f && red.g < 0.15f && black.a >= 0.9f,
                "黑正面/红反面材质或不透明度不正确。");
            Require(((LineRenderer)Get("path")).positionCount > 10 && ((LineRenderer)Get("incomingPath")).positionCount > 10 &&
                ((LineRenderer)Get("strokePath")).positionCount == 2, "落球、挥拍、出球轨迹未同时生成。");
            Set("time", (float)(original.StartTime + original.Duration * 0.4)); Call("UpdateServeVisuals");
            V3 center = original.CenterAt((float)Get("time"));
            Require(Vector3.Distance(racket.position, new Vector3((float)center.X, (float)center.Y, (float)center.Z)) < 1e-5f,
                "显示球拍未沿用户指定直线移动。");
            Call("FocusStrokeContact"); Call("UpdateServeVisuals");
            Require(((Transform)Get("serveTouch")).gameObject.activeSelf && ((LineRenderer)Get("serveBrush")).enabled,
                "停在触球未显示真实触点和摩擦方向。");
            var camera = (Camera)Get("view"); Vector3 projected = camera.WorldToViewportPoint(racket.position);
            Require(projected.z > 0 && projected.x > 0 && projected.x < 1 && projected.y > 0 && projected.y < 1,
                "放大触球时球拍不在画面内。");
            var poseBefore = camera.transform.position; float sizeBefore = camera.orthographicSize;
            Call("OrbitCamera", new Vector2(40, 20)); Call("ZoomCamera", -2f);
            Require(Vector3.Distance(poseBefore, camera.transform.position) > 0.01f && camera.orthographicSize < sizeBefore,
                "发球特写的旋转或缩放无效。");
            Require(Get("strokeResult") == result, "视角操作改变了正向物理结果。");
            original.Start += new V3(0.4, 0, 0); original.End += new V3(0.4, 0, 0); lab.Recalculate();
            Require(Get("serveContact") != null, "自动对准没有修正挥拍偏移。");
            Set("strokeAutoContact", false);
            original.Start += new V3(0.4, 0, 0); original.End += new V3(0.4, 0, 0); lab.Recalculate(); Call("UpdateServeVisuals");
            Require(Get("serveContact") == null && Get("flight") == null && racket.gameObject.activeSelf &&
                !((Transform)Get("serveTouch")).gameObject.activeSelf && !((LineRenderer)Get("serveBrush")).enabled,
                "漏球时伪造了出球或接触信息。");
            Set("strokeAutoContact", true); Set("stroke", new ServeStroke()); lab.Recalculate(); result = (ServeStrokeResult)Get("strokeResult");
            var source = result.Contact.After;
            Call("UseStrokeForReceive");
            var arrival = (BallState)Get("serveInitial");
            Require((int)Get("mode") == 2 && Get("reception") != null && (arrival.Position - source.Position).Length < 1e-5 &&
                (arrival.Velocity - source.Velocity).Length < 1e-5 && (arrival.Spin - source.Spin).Length < 1e-4,
                "接发研究没有使用实际生成的发球。");
            File.WriteAllText("Artifacts/forward-stroke-ui-checks.txt",
                "PASS: 0/40/90-degree visible black face matches collision normal.\nPASS: opaque black front and red back.\nPASS: falling, stroke and outgoing paths coexist.\nPASS: displayed racket follows the specified line.\nPASS: contact zoom frames actual contact and friction.\nPASS: miss retains racket without fabricating a hit.\nPASS: generated shot enters fixed-point receive study unchanged within float precision.\nThese are Editor state/geometry checks; native mouse interactions require separate observation.\n");
        }
        finally { Object.Destroy(obj); }
    }
}
