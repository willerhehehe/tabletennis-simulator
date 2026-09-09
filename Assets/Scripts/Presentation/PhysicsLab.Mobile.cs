using System;
using System.Collections.Generic;
using UnityEngine;

namespace TableTennis
{
    public sealed partial class PhysicsLab
    {
        int mobilePanel;
        Vector2 mobileScroll;
        bool mobilePan, mobileHeightDrag;
        float pinchDistance;
        readonly Dictionary<string, float> webNumbers = new Dictionary<string, float>();
        bool CompactLayout => UIWidth < 1000 || UIHeight < 600;
        bool MobileWide => UIWidth > UIHeight * 1.25f;
        Rect MobileViewArea => new Rect(0, 90, UIWidth - (MobileWide ? Mathf.Min(340, UIWidth * 0.42f) : 0),
            MobileWide ? Mathf.Max(100, UIHeight - 174) : Mathf.Clamp(UIHeight * 0.31f, 160, 300));
        Rect MobileSpinRect => new Rect(ViewArea.xMax - 78, ViewArea.y + 8, 70, 70);
        Rect MobilePanelRect => MobileWide
            ? new Rect(ViewArea.xMax + 8, 48, UIWidth - ViewArea.xMax - 16, UIHeight - 56)
            : new Rect(8, ViewArea.yMax + 80, UIWidth - 16, Mathf.Max(100, UIHeight - ViewArea.yMax - 88));

        [Serializable] sealed class WebNumber { public string label; public float value; }
        [UnityEngine.Scripting.Preserve]
        public void CloseWebNumber()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = true;
#endif
        }
        [UnityEngine.Scripting.Preserve]
        public void ApplyWebNumber(string json)
        {
            try
            {
                var n = JsonUtility.FromJson<WebNumber>(json);
                if (n != null && !string.IsNullOrEmpty(n.label) && n.label.Length < 100 && !float.IsNaN(n.value) && !float.IsInfinity(n.value))
                    webNumbers[n.label] = n.value;
            }
            catch (ArgumentException) { }
        }

        void UpdateTouchCamera()
        {
            if (!CompactLayout || Input.touchCount != 2) { pinchDistance = 0; return; }
            var a = Input.GetTouch(0); var b = Input.GetTouch(1);
            Vector2 Point(Vector2 p) => new Vector2(p.x / guiScale, (Screen.height - p.y) / guiScale);
            if (!ViewArea.Contains(Point(a.position)) || !ViewArea.Contains(Point(b.position))) { pinchDistance = 0; return; }
            float distance = Vector2.Distance(a.position, b.position);
            if (pinchDistance > 0 && distance > 0) ZoomCamera(Mathf.Log(pinchDistance / distance) / 0.08f);
            pinchDistance = distance;
            PanCamera(new Vector2((a.deltaPosition.x + b.deltaPosition.x) / 2, -(a.deltaPosition.y + b.deltaPosition.y) / 2) / guiScale);
        }

        void DrawMobileInterface()
        {
            parametersChanged = false; inputError = "";
            DrawRect(new Rect(0, 0, UIWidth, 90), new Color(.055f, .08f, .10f));
            DrawRect(new Rect(0, ViewArea.yMax, UIWidth, UIHeight - ViewArea.yMax), new Color(.055f, .08f, .10f));
            int oldMode = mode == 3 ? 0 : mode + 1;
            GUI.enabled = !IsStudying;
            int next = GUI.Toolbar(new Rect(8, 6, UIWidth - 16, 38), oldMode, new[] { "挥拍发球", "出球", "单板", "接发" });
            if (next != oldMode)
            {
                EndPointPicking(false); referenceFlight = null; reference.positionCount = 0;
                if (next == 0) OpenForwardServe(); else if (next == 2) ApplyForehand(); else if (next == 3) ApplyServeStudy(); else mode = 0;
                if (next != 0) SetCamera(2);
                mobilePanel = next == 3 ? 1 : 0; mobileScroll = Vector2.zero; Recalculate();
            }
            GUI.enabled = true;
            int camera = cameraMode == 1 ? 0 : cameraMode == 2 ? 1 : cameraMode == 0 ? 2 : 3;
            int selected = GUI.Toolbar(new Rect(8, 48, ViewArea.width - 16, 36), camera, new[] { "俯视", "侧视", "三维", "触球" });
            if (selected != camera)
            {
                if (selected == 3 && mode == 3) FocusStrokeContact();
                else if (selected == 3 && mode == 0) OpenServeContact();
                else { strokeCloseup = false; SetCamera(selected == 0 ? 1 : selected == 1 ? 2 : selected == 2 ? 0 : 3); }
            }
            if (spinPreview == null) spinPreview = new SpinPreview();
            spinPreview.SetViewRotation(view.transform.rotation);
            spinPreview.Draw(MobileSpinRect, PreviewBallState(), small, true, true);
            GUI.Label(new Rect(MobileSpinRect.x, MobileSpinRect.yMax, 76, 24), $"{spinPreview.LocalSpin.Length / BallPhysics.Rps:0.#} rps", small);
            string result = !string.IsNullOrEmpty(error) ? error : cameraMode == 4 ? (serveContact != null ? "发球触球示例" : "当前条件未匹配到触球动作") : mode == 3 ? (strokeResult?.TwoBounces == true ? "先落本方，再落对方" : "请调整发球与拍型") : flight?.Verdict ?? "";
            GUI.Label(new Rect(8, ViewArea.y + 5, ViewArea.width - 95, 50), pickingReceivePoint ? "点击高亮轨迹选择接球点" : result, small);
            if (mode == 3) DrawStrokeHandles();
            HandleCanvasPointClick(); HandleCameraInput();

            GUILayout.BeginArea(new Rect(8, ViewArea.yMax + 3, ViewArea.width - 16, 74));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((cameraMode == 4 ? servePlaying : playing) ? "暂停" : "播放", GUILayout.Width(58)))
            {
                if (cameraMode == 4) { if (serveTime >= .02f) serveTime = -.02f; servePlaying = !servePlaying; }
                else if (mode == 3) ToggleStrokePlayback(); else if (flight != null) playing = !playing;
            }
            if (GUILayout.Button("重播", GUILayout.Width(58)))
            { if (cameraMode == 4) { serveTime = -.02f; servePlaying = true; } else { time = mode == 3 ? 0 : StartTime; playing = true; } }
            mobilePan = GUILayout.Toggle(mobilePan, "平移", GUI.skin.button, GUILayout.Width(55));
            if (GUILayout.Button("−", GUILayout.Width(34))) ZoomCamera(2);
            if (GUILayout.Button("+", GUILayout.Width(34))) ZoomCamera(-2);
            if (GUILayout.Button("复位")) ResetCameraView();
            GUILayout.EndHorizontal();
            float end = cameraMode == 4 ? .02f : mode == 3 ? StrokeEnd : flight?.Samples.Count > 0 ? (float)flight.Samples[flight.Samples.Count - 1].Time : 1;
            float shownTime = cameraMode == 4 ? serveTime : time;
            float t = GUILayout.HorizontalSlider(shownTime, cameraMode == 4 ? -.02f : mode == 3 ? 0 : StartTime, Mathf.Max(0.001f, end));
            if (t != shownTime) { if (cameraMode == 4) { serveTime = t; servePlaying = false; } else { time = t; playing = false; } }
            GUILayout.EndArea();

            Rect panel = MobilePanelRect;
            DrawRect(panel, new Color(.075f, .105f, .13f));
            GUILayout.BeginArea(panel);
            int tab = GUILayout.Toolbar(mobilePanel, new[] { "动作", "接法", "器材" });
            if (tab != mobilePanel) { mobilePanel = tab; mobileScroll = Vector2.zero; editField = ""; GUI.FocusControl(null); }
            bool receiveActions = mobilePanel == 1 && mode == 2;
            float footer = receiveActions ? 96 : 0;
            mobileScroll = GUILayout.BeginScrollView(mobileScroll, GUILayout.Height(Mathf.Max(50, panel.height - 52 - footer)));
            GUI.enabled = !IsStudying;
            if (mobilePanel == 2)
            {
                DrawEquipmentSettings();
                GUILayout.Label("旋转示意", section); spinPreview.DrawControls(quietButton, small, true);
                GUILayout.Label("球台参数", section);
                SettingSlider("球台摩擦 μ", ref settings.TableFriction, 0, 1);
                SettingSlider("球台恢复 e", ref settings.TableRestitution, 0, 1);
            }
            else if (receiveActions)
            {
                int receivePage = GUILayout.Toolbar(receiveTab, new[] { "设置接法", "候选结果" });
                if (receivePage != receiveTab) { receiveTab = receivePage; mobileScroll = Vector2.zero; }
                if (receiveTab == 0) DrawReceiveSettings(); else DrawReceiveCandidates();
            }
            else if (mobilePanel == 1)
            {
                GUILayout.Label("先确定发球，再研究接法", section);
                GUI.enabled = mode == 3 && strokeResult?.TwoBounces == true && !strokeResult.MayTouchTable;
                if (GUILayout.Button("用当前发球研究接法", primaryButton)) { UseStrokeForReceive(); mobileScroll = Vector2.zero; }
                GUI.enabled = !IsStudying;
                if (GUILayout.Button("载入接发研究示例")) { ApplyServeStudy(); Recalculate(); SetCamera(2); }
            }
            else if (mode == 3) DrawMobileStrokeControls();
            else DrawMobileBallControls();
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(inputError)) GUILayout.Label(inputError, small);
            if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice, small);
            GUILayout.EndScrollView();
            if (receiveActions)
            {
                if (IsStudying)
                {
                    GUILayout.Label($"评估中 {receiveRunner.Done}/{receiveRunner.Total} · 可取消", small);
                    if (GUILayout.Button("取消评估")) receiveRunner.Cancel();
                }
                else
                {
                    GUI.enabled = CanEvaluateReceive;
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("搜索更稳接法", primaryButton)) { if (parametersChanged) { Recalculate(); parametersChanged = false; } StartReceiveSearch(); }
                    if (GUILayout.Button("仅测当前")) { if (parametersChanged) { Recalculate(); parametersChanged = false; } TestCurrentReceive(); }
                    GUILayout.EndHorizontal(); GUI.enabled = receiveReport != null;
                    if (GUILayout.Button("下载接发报告 ZIP")) ExportReceiveReport();
                    GUI.enabled = true;
                }
            }
            GUILayout.EndArea();
            var drag = Event.current;
            var scrollArea = new Rect(panel.x, panel.y + 42, panel.width, Mathf.Max(50, panel.height - 52 - footer));
            if (Input.touchCount == 1 && drag.type == EventType.MouseDrag && GUIUtility.hotControl == 0 && scrollArea.Contains(InterfaceMousePosition))
            { mobileScroll.y = Mathf.Max(0, mobileScroll.y - drag.delta.y); drag.Use(); }
            if (parametersChanged && !IsStudying) Recalculate();
        }

        void DrawMobileStrokeControls()
        {
            strokeSettingsTab = GUILayout.Toolbar(strokeSettingsTab, new[] { "发球预设", "手调动作" });
            if (strokeSettingsTab == 0)
            {
                var preset = presetPanel.Draw(section, small, quietButton, strokeResult);
                if (preset != null) try { LoadServePreset(preset); } catch (ArgumentException e) { notice = e.Message; }
            }
            else
            {
                double height = stroke.Apex.Y;
                SettingSlider("落球顶点高度", ref height, .85f, 2.5f, "m"); stroke.Apex = new V3(stroke.Apex.X, height, stroke.Apex.Z);
                SettingSlider("击球高度", ref strokeContactHeight, .8f, (float)Math.Max(.8, height - .03), "m");
                SettingSlider("拍面与台面夹角", ref stroke.TableAngle, 0, 180, "°");
                SettingSlider("拍面朝向 · 左右", ref stroke.Yaw, -90, 90, "°");
                SettingSlider("拍柄转角", ref stroke.Roll, -180, 180, "°");
                bool back = GUILayout.Toggle(stroke.BackFace, "以红面击球"); if (back != stroke.BackFace) { stroke.BackFace = back; parametersChanged = true; }
                SettingSlider("挥拍速度", ref stroke.Speed, 0, 25, "m/s");
                V3 d = stroke.Velocity.Length > 0 ? stroke.Velocity.Unit : (stroke.End - stroke.Start).Unit;
                double up = Math.Asin(d.Y) * 180 / Math.PI, side = Math.Atan2(d.X, d.Z) * 180 / Math.PI;
                SettingSlider("挥拍向上角", ref up, -90, 90, "°"); SettingSlider("挥拍左右角", ref side, -180, 180, "°");
                V3 next = Direction(up, side); if ((next - d).Length > 1e-6) stroke.End = stroke.Start + next * stroke.PathLength;
                mobileHeightDrag = GUILayout.Toggle(mobileHeightDrag, "拖动圆点时只改高度");
                GUILayout.Label("单指拖圆点；空白处旋转/平移；双指缩放。", small);
            }
            if (GUILayout.Button("下载动作与轨迹 ZIP")) ExportStroke();
        }

        void DrawMobileBallControls()
        {
            DrawSpinControls();
            Slider("速度", ref speed, 1, 25, "m/s");
            Slider(mode == 1 ? "向下入射角" : "出球仰角", ref elevation, -45, 40, "°");
            V3 spin = BallSpin.Components(WorldSpinRps, SpinTravel);
            if (Slider("水平偏角", ref direction, -40, 40, "°") && !serveSpinOpen) SetWorldSpin(BallSpin.Compose(spin, SpinTravel));
            Slider("出球左右", ref originX, -1.5f, 1.5f, "m"); Slider("球心高度", ref originY, .8f, 2.5f, "m"); Slider("出球前后", ref originZ, -3, 3, "m");
            if (mode == 1)
            {
                if (GUILayout.Button("反手拧拉下旋示例")) ApplyBackhandFlick();
                Slider("拍面开合", ref facePitch, -30, 40, "°"); Slider("拍面侧偏", ref faceYaw, -40, 40, "°");
                Slider("向前速度", ref racketForward, -20, 20, "m/s"); Slider("向上速度", ref racketUp, -20, 20, "m/s"); Slider("横向速度", ref racketSide, -20, 20, "m/s");
            }
            if (GUILayout.Button("下载轨迹 ZIP")) Export();
        }
    }
}
