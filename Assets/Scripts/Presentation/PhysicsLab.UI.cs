using System;
using UnityEngine;

namespace TableTennis
{
    public sealed partial class PhysicsLab
    {
        float guiWidth, guiHeight, guiScale = 1;
        bool servePositionOpen, serveSpinOpen, serveRouteOpen, showTelemetry, showCloud = true;
        float UIWidth => guiWidth > 0 ? guiWidth : Screen.width / guiScale;
        float UIHeight => guiHeight > 0 ? guiHeight : Screen.height / guiScale;
        // Unity already exposes these in the current GUI coordinate space, including GUI.matrix scaling.
        Vector2 InterfaceMousePosition => Event.current.mousePosition;
        Vector2 InterfaceMouseDelta => Event.current.delta;
        Rect ViewArea => new Rect(panelWidth, ViewTop, CanvasRight - panelWidth, Mathf.Max(80, UIHeight - ViewTop - FooterHeight));

#if UNITY_EDITOR
        UnityEditor.EditorWindow editorGameView;
        static readonly Type GameViewType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
        const System.Reflection.BindingFlags ViewFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        static readonly System.Reflection.PropertyInfo ViewDensity = GameViewType.GetProperty("backingScale", ViewFlags);
        static readonly System.Reflection.PropertyInfo ViewLowResolution = GameViewType.GetProperty("lowResolutionForAspectRatios", ViewFlags);
        static readonly System.Reflection.PropertyInfo ViewSize = GameViewType.GetProperty("currentGameViewSize", ViewFlags);
#endif
        float InterfaceScale()
        {
#if UNITY_EDITOR
            // The window's backing density is stable even while runtime IMGUI renders with a different GUI context.
            if (editorGameView == null)
            {
                var views = Resources.FindObjectsOfTypeAll(GameViewType);
                if (views.Length == 0) return 1;
                editorGameView = (UnityEditor.EditorWindow)views[0];
            }
            var size = ViewSize.GetValue(editorGameView);
            bool aspectRatio = size.GetType().GetProperty("sizeType", ViewFlags).GetValue(size).ToString() == "AspectRatio";
            if (aspectRatio && (bool)ViewLowResolution.GetValue(editorGameView)) return 1;
            return Mathf.Max(1, (float)ViewDensity.GetValue(editorGameView));
#else
            // ponytail: retain the existing player layout; configure standalone display scaling when a player build is added.
            return 1;
#endif
        }

        void LayoutInterface()
        {
            guiWidth = Screen.width / guiScale; guiHeight = Screen.height / guiScale;
            panelWidth = mode == 3 ? 332 : Mathf.Clamp(guiWidth * (mode == 2 ? 0.21f : 0.25f), 284, 340);
            Rect area = ViewArea;
            view.rect = new Rect(area.x / guiWidth, FooterHeight / guiHeight, area.width / guiWidth, area.height / guiHeight);
            if (cameraMode != 0 && Mathf.Abs(view.aspect - fittedAspect) > 0.01f) FitCamera();
        }

        bool Fold(string label, ref bool expanded)
        {
            GUILayout.Space(6);
            if (GUILayout.Button((expanded ? "−  " : "+  ") + label, quietButton)) expanded = !expanded;
            return expanded;
        }

        void DrawInterface()
        {
            if (mode == 3) { DrawStrokeInterface(); return; }
            DrawRect(new Rect(0, 0, panelWidth, UIHeight), new Color(0.075f, 0.105f, 0.13f));
            DrawRect(new Rect(panelWidth - 1, 0, 1, UIHeight), new Color(0.19f, 0.25f, 0.28f));
            parametersChanged = false; inputError = "";
            GUILayout.BeginArea(new Rect(18, 16, panelWidth - 36, UIHeight - 32));
            GUILayout.Label(mode == 2 ? "01  发球设置" : "单球实验", heading);
            GUILayout.Label(mode == 2 ? "改变来球，再比较接法" : "独立观察击球与飞行", small);
            GUILayout.Space(12);
            GUI.enabled = !IsStudying;
            GUILayout.Label("实验模式", small);
            int tab = GUILayout.SelectionGrid(mode + 1, new[] { "挥拍发球", "出球模拟", "单板击球", "接发研究" }, 2);
            int selectedMode = tab == 0 ? 3 : tab - 1;
            if (selectedMode != mode)
            {
                if (cameraMode == 4) SetCamera(2);
                EndPointPicking(false);
                if (selectedMode == 3) OpenForwardServe();
                else if (selectedMode == 2) ApplyServeStudy(); else { if (mode == 2) ApplyForehand(); mode = selectedMode; }
                referenceFlight = null; reference.positionCount = 0; referenceLabel = "";
                parametersChanged = true; editField = "";
            }
            GUILayout.Space(10);
            scroll = GUILayout.BeginScrollView(scroll);
            if (mode == 2 && GUILayout.Button("载入下旋发球示例", quietButton)) { ApplyServeStudy(); parametersChanged = true; }
            if (mode == 1)
            {
                if (GUILayout.Button("正手拉下旋示例", quietButton)) { ApplyForehand(); parametersChanged = true; }
                if (GUILayout.Button("反手拧拉下旋示例", quietButton)) ApplyBackhandFlick();
                if (GUILayout.Button("对照：拧拉只横擦", quietButton)) { ApplyBackhandFlick(); racketUp = 0; }
                if (!string.IsNullOrEmpty(shotExample)) GUILayout.Label("参考：" + shotExample + "\n改变参数可继续试验；未模拟完整手腕动作。", small);
                if (GUILayout.Button("对照：上提不足", quietButton)) { ApplyForehand(); racketUp = ForehandBackspin.WeakUp; parametersChanged = true; }
            }
            DrawSpinControls();
            if (mode == 2 && Fold("发球路线 · 两次落台", ref serveRouteOpen))
            {
                if (serveFlight != null)
                    for (int i = 0; i < Math.Min(2, serveFlight.Events.Count); i++)
                    {
                        var hit = serveFlight.Events[i];
                        GUILayout.Label(hit.Kind == "球台" ? $"第 {i + 1} 跳  X {hit.Before.Position.X:+0.00;-0.00;0.00} / Z {hit.Before.Position.Z:+0.00;-0.00;0.00} m" : $"第 {i + 1} 次接触：{hit.Kind}", small);
                    }
                GUILayout.Label("先落本方半台，再落对方半台，之后选接球点。", small);
            }
            GUILayout.Label(mode == 1 ? "来球" : "速度与方向", section);
            Slider("速度", ref speed, 1, 25, "m/s");
            Slider(mode == 1 ? "向下入射角" : "出球仰角", ref elevation, mode == 2 ? -45 : -20, 40, "°");
            V3 relativeSpin = BallSpin.Components(WorldSpinRps, SpinTravel);
            if (Slider("水平偏角", ref direction, -40, 40, "°") && !serveSpinOpen)
                SetWorldSpin(BallSpin.Compose(relativeSpin, SpinTravel));
            if (mode == 1)
            {
                GUILayout.Label("拍型与挥拍", section);
                Slider("拍面开合", ref facePitch, -30, 40, "°");
                GUILayout.Label("0° 竖直；正值后仰，负值前压。", small);
                Slider("拍面侧偏", ref faceYaw, -40, 40, "°");
                Slider("拍柄转角", ref racketRoll, -180, 180, "°");
                Slider("向前速度 · Z", ref racketForward, -RacketMotionLimits.Component, RacketMotionLimits.Component, "m/s");
                Slider("向上速度 · Y", ref racketUp, -RacketMotionLimits.Component, RacketMotionLimits.Component, "m/s");
                Slider("横向速度 · X", ref racketSide, -RacketMotionLimits.Component, RacketMotionLimits.Component, "m/s");
            }
            if (Fold(mode == 1 ? "击球位置" : "离拍位置", ref servePositionOpen))
            {
                Slider("横向 X", ref originX, -1, 1, "m");
                Slider("球心高度 Y", ref originY, 0.80f, 1.8f, "m");
                Slider("纵向 Z", ref originZ, -2.4f, -0.35f, "m");
            }
            if (Fold("物理参数", ref advanced))
            {
                GUILayout.Label("经验参数，仍需实测标定。", small);
                SettingSlider("空气阻力 Cd", ref settings.Drag, 0, 1);
                SettingSlider("升力斜率", ref settings.LiftSlope, 0, 1.5f);
                SettingSlider("升力上限", ref settings.LiftCap, 0, 1);
                SettingSlider("旋转衰减", ref settings.SpinDecay, 0, 0.5f, "1/s");
                SettingSlider("球台恢复 e", ref settings.TableRestitution, 0, 1);
                SettingSlider("球台摩擦 μ", ref settings.TableFriction, 0, 1);
                if (mode != 2)
                {
                    bool enabled = GUILayout.Toggle(settings.NetEnabled, "检测球网");
                    if (enabled != settings.NetEnabled) { settings.NetEnabled = enabled; parametersChanged = true; }
                }
            }
            GUILayout.EndScrollView();
            GUI.enabled = true;
            GUILayout.Space(10);
            GUILayout.Label(IsStudying ? "正在评估，参数暂时锁定" : "拖动滑块观察变化 · 点击数值精确输入", small);
            GUILayout.EndArea();
            DrawReceivePanel();
            if (parametersChanged) Recalculate();
            DrawResults();
            DrawCameraControls();
            HandleCanvasPointClick();
        }

        void DrawResults()
        {
            float left = panelWidth + 22, width = CanvasRight - left - 22;
            DrawRect(new Rect(panelWidth, 0, CanvasRight - panelWidth, ViewTop), new Color(0.055f, 0.08f, 0.10f));
            DrawRect(new Rect(panelWidth, UIHeight - FooterHeight, CanvasRight - panelWidth, FooterHeight), new Color(0.055f, 0.08f, 0.10f));
            GUI.Label(new Rect(left, 16, width - 190, 28), cameraMode == 4 ? "发球挥拍与摩擦位置" : mode == 2 ? "接发稳定性实验" : "乒乓球物理实验", heading);
            GUI.Label(new Rect(left, 46, width, 22), cameraMode == 4 ? "目标出球 → 一组可复现的触球动作" : mode == 2 ? "设发球  →  选接球点  →  评估与比较" : "观察轨迹、旋转与碰撞", small);
            if (flight != null && cameraMode != 4)
            {
                string verdict = mode != 0 && RacketGeometry.MayTouchTable(incoming.Position, RacketNormal, RacketVelocity, racketRoll)
                    ? "球拍可能碰台" : flight.Verdict;
                statusStyle.fontSize = 14;
                statusStyle.normal.textColor = verdict == "落入对方半台" ? mint : new Color(1, 0.73f, 0.44f);
                GUI.Label(new Rect(CanvasRight - 215, 18, 193, 27), new GUIContent("单次：" + verdict, "这是无误差的一条回球轨迹；右侧百分比来自多次扰动复验。"), statusStyle);
            }
            GUILayout.BeginArea(new Rect(left, 76, width, 38)); GUILayout.BeginHorizontal();
            int selectedView = cameraMode == 4 ? -1 : cameraMode == 1 ? 0 : cameraMode == 2 ? 1 : cameraMode == 3 ? 3 : 2;
            int selected = GUILayout.Toolbar(selectedView, mode != 0 ? new[] { "俯视", "侧视", "三维", "触球" } : new[] { "俯视", "侧视", "三维" }, GUILayout.Width(mode != 0 ? 252 : 198));
            if (selected != selectedView)
            {
                if (cameraMode == 4 && selected < 3) { serveRouteView = selected; FitServeTrajectory(); }
                else
                {
                    SetCamera(selected == 0 ? 1 : selected == 1 ? 2 : selected == 3 ? 3 : 0);
                    if (selected == 3) { time = 0; playing = false; }
                }
            }
            if (mode == 2)
            {
                GUI.enabled = !IsStudying && CanPickReceivePoint;
                bool pick = GUILayout.Toggle(pickingReceivePoint, new GUIContent("选接球点", "在可接球轨迹上点击设置接球点；Esc 退出。"), GUI.skin.button, GUILayout.Width(100));
                if (pick != pickingReceivePoint) { if (pick) BeginPointPicking(); else EndPointPicking(true); }
                GUI.enabled = true;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(referenceFlight == null ? "固定轨迹" : "清除参照", "固定当前无误差轨迹，便于修改参数后比较。"), GUILayout.Width(92)))
            {
                if (referenceFlight != null) { reference.positionCount = 0; referenceFlight = null; referenceLabel = ""; FitCamera(); }
                else if (flight != null)
                {
                    var points = new Vector3[path.positionCount]; path.GetPositions(points); reference.positionCount = points.Length; reference.SetPositions(points);
                    referenceFlight = flight; referenceLaunch = launch; referenceLabel = "紫线为已固定的参照";
                }
            }
            showTelemetry = GUILayout.Toggle(showTelemetry, "数据", GUI.skin.button, GUILayout.Width(54));
            GUILayout.EndHorizontal(); GUILayout.EndArea();

            if (cameraMode == 4) { DrawServeContactCanvas(left, width); return; }
            if (cameraMode == 3)
                DrawRect(new Rect(panelWidth, ViewTop, CanvasRight - panelWidth, 94), new Color(0.055f, 0.08f, 0.10f));
            if (pickingReceivePoint)
            {
                DrawRect(new Rect(left, ViewTop + 10, Mathf.Min(width, 440), 35), new Color(0.13f, 0.24f, 0.24f));
                GUI.Label(new Rect(left + 10, ViewTop + 15, width - 20, 26), "点击右侧高亮曲线选择接球点 · Esc 退出", small);
            }
            else
            {
                string legend = IsStudying && receiveReport != null ? "上一轮结果 · 新试验完成后更新" :
                    selectedReceive != null ? "绿点：复验落点    橙线：发球    青线：当前接法" : mode == 0 ? "青线：出球轨迹" : "橙线：来球    青线：回球";
                GUI.Label(new Rect(left, ViewTop + 10, width, 22), legend + (referenceFlight != null ? "    紫线：参照" : ""), small);
            }
            if (cameraMode == 1 && mode != 1)
            {
                int spinKind = BallSpin.Kind(BallSpin.Components(WorldSpinRps, SpinTravel));
                string label = spinKind < 6 ? SpinNames[spinKind] : "自定义旋转";
                GUI.Label(new Rect(left, ViewTop + 34, width, 38), "出球：" + label + " · 上下旋请切侧视；左右按发球者朝向对方定义。", small);
                WorldLabel(new Vector3(-0.82f, 0.78f, -1.5f), "发球者左", 8);
                WorldLabel(new Vector3(0.82f, 0.78f, -1.5f), "发球者右", 8);
            }
            if (landingCloud != null) landingCloud.gameObject.SetActive(showCloud && !pickingReceivePoint);
            if (cameraMode != 3 && mode == 2 && serveFlight != null)
            {
                for (int i = 0; i < Math.Min(2, serveFlight.Events.Count); i++)
                    if (serveFlight.Events[i].Kind == "球台") WorldLabel(U(serveFlight.Events[i].Before.Position), $"{i + 1} · 发球落台");
                if (reception != null) WorldLabel(U(incoming.Position), "3 · 接球点", -40);
            }
            if (cameraMode == 3 && mode != 0 && flight != null)
            {
                GUI.Label(new Rect(left, ViewTop + 38, width, 25), "黑面触球 · 蓝箭头：拍面朝向 · 黄箭头：挥拍速度" + (showSpinAxis ? " · 紫箭头：角速度轴" : ""), small);
                double normalSpeed = V3.Dot(RacketVelocity, RacketNormal);
                double brushSpeed = (RacketVelocity - RacketNormal * normalSpeed).Length;
                GUI.Label(new Rect(left, ViewTop + 64, width, 25), $"垂直拍面 {normalSpeed:+0.00;-0.00;0.00} m/s  ·  沿拍面 {brushSpeed:0.00} m/s  ·  开合 {facePitch:+0.0;-0.0;0.0}°", small);
                DrawRect(new Rect(left - 4, ViewArea.yMax - 82, Mathf.Min(width + 8, 480), 45), new Color(0.055f, 0.08f, 0.10f, 0.94f));
                GUI.Label(new Rect(left, ViewArea.yMax - 82, width, 40), "仅显示触球前后 10 ms 的局部动作，拍柄转角可调。\n完整引拍、收拍及人体可达性尚未建模。", small);
            }
            if (mode != 0 && flight != null && RacketGeometry.MayTouchTable(incoming.Position, RacketNormal, RacketVelocity, racketRoll))
                GUI.Label(new Rect(left, ViewTop + 94, width, 35), "当前动作可能碰台，评估会计作失败；可提高接球点或调整拍型。", small);
            if (!string.IsNullOrEmpty(error))
            {
                var rect = new Rect(left, ViewTop + 55, width, 150);
                DrawRect(rect, new Color(0.23f, 0.16f, 0.12f));
                bool validArrival = mode == 2 && reception != null;
                GUI.Label(new Rect(rect.x + 12, rect.y + 10, rect.width - 24, 90),
                    (validArrival ? "手动接法未形成有效击球\n" : "发球或接球点需要调整\n") + error +
                    (validArrival ? "\n来球和接球点有效，仍可搜索其他接法。" : ""), small);
                GUI.enabled = !IsStudying;
                if (validArrival)
                {
                    if (GUI.Button(new Rect(rect.x + 12, rect.y + 107, 148, 34), "搜索可用接法")) StartReceiveSearch();
                }
                else if (mode == 2 && CanPickReceivePoint)
                {
                    if (GUI.Button(new Rect(rect.x + 12, rect.y + 107, 148, 34), "重新选择接球点")) BeginPointPicking();
                }
                else if (mode == 2 && GUI.Button(new Rect(rect.x + 12, rect.y + 107, 148, 34), "恢复下旋示例"))
                { ApplyServeStudy(); Recalculate(); }
                GUI.enabled = true;
            }
            if (!string.IsNullOrEmpty(inputError))
            {
                DrawRect(new Rect(left, ViewTop + 42, width, 42), new Color(0.23f, 0.16f, 0.12f));
                GUI.Label(new Rect(left + 10, ViewTop + 49, width - 20, 32), inputError, small);
            }
            if (flight == null) return;
            if (showTelemetry) DrawTelemetry(left, width);
            DrawPlayback(left, width);
        }

        void DrawPlayback(float left, float width)
        {
            var landing = flight.Events.Find(e => e.Kind == "球台");
            GUILayout.BeginArea(new Rect(left, UIHeight - FooterHeight + 10, width, FooterHeight - 12));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(playing ? "暂停" : "播放", primaryButton, GUILayout.Width(78), GUILayout.Height(34))) TogglePlayback();
            if (GUILayout.Button(new GUIContent("重播", "从发球起点重新播放；快捷键 R"), GUILayout.Width(58))) { time = StartTime; playing = true; }
            time = GUILayout.HorizontalSlider(time, StartTime, (float)flight.Samples[flight.Samples.Count - 1].Time);
            GUILayout.Label($"{time:0.000} s", value, GUILayout.Width(80));
            if (GUILayout.Button($"{playbackSpeed:0.##}×", GUILayout.Width(64))) playbackSpeed = playbackSpeed == 0.25f ? 1 : playbackSpeed == 1 ? 0.1f : 0.25f;
            GUILayout.EndHorizontal(); GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("到触球", GUILayout.Width(74))) { time = 0; playing = false; }
            GUI.enabled = landing != null;
            if (GUILayout.Button("到落台", GUILayout.Width(74))) { time = (float)landing.Before.Time + 0.000001f; playing = false; }
            GUI.enabled = true;
            string metrics = $"出球 {launch.Velocity.Length:0.00} m/s";
            if (flight.NetClearance.HasValue) metrics += $"  ·  过网 {flight.NetClearance * 100:0.0} cm";
            if (mode == 2)
            {
                var action = ManualReceiveAction;
                metrics = $"开合 {action.Pitch:+0.0;-0.0;0.0}°  ·  迎前 {action.Forward:+0.00;-0.00;0.00} / 上提 {action.Up:+0.00;-0.00;0.00} / 横向 {action.Side:+0.00;-0.00;0.00} m/s";
            }
            GUILayout.Label(new GUIContent(metrics, "前后速度：正值朝左回球，负值向右缓冲。完整球速、旋转与落点可在顶部“数据”中查看。"), small, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(new GUIContent("轨迹 CSV", "导出完整轨迹、碰撞事件和本次参数。"), GUILayout.Width(92))) Export();
            GUILayout.EndHorizontal();
            string line = "空格：播放 / 暂停  ·  物理参数待实测标定";
            if (!string.IsNullOrEmpty(notice)) line = notice.Contains("导出") && !notice.Contains("失败") ? "已导出到项目 Artifacts 文件夹" : notice;
            if (referenceFlight != null && string.IsNullOrEmpty(notice))
            {
                line = $"与参照相比  ·  速度 Δ{launch.Velocity.Length - referenceLaunch.Velocity.Length:+0.00;-0.00;0.00} m/s";
                var prior = referenceFlight.Events.Find(e => e.Kind == "球台");
                if (prior != null && landing != null) line += $"  ·  落点 ΔX {(landing.Before.Position.X - prior.Before.Position.X) * 100:+0.0;-0.0;0.0} / ΔZ {(landing.Before.Position.Z - prior.Before.Position.Z) * 100:+0.0;-0.0;0.0} cm";
            }
            GUILayout.Label(new GUIContent(line, notice), small);
            GUILayout.EndArea();
        }

        void DrawTelemetry(float left, float width)
        {
            var rect = TelemetryRect(width);
            DrawRect(rect, new Color(0.075f, 0.12f, 0.15f, 0.97f));
            GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 8, rect.width - 24, rect.height - 16));
            BallState s = StateAt(time);
            GUILayout.Label(time < 0 ? "发球 / 来球中" : "接球之后", section);
            GUILayout.Label($"速度 {s.Velocity.Length:0.00} m/s  ·  转速 {s.Spin.Length / BallPhysics.Rps:0.0} rps", small);
            GUILayout.Label($"ωX {s.Spin.X / BallPhysics.Rps:0.0} / Y {s.Spin.Y / BallPhysics.Rps:0.0} / Z {s.Spin.Z / BallPhysics.Rps:0.0}", small);
            var landing = flight.Events.Find(e => e.Kind == "球台");
            if (landing != null)
            {
                GUILayout.Label($"落点 X {landing.Before.Position.X:+0.000;-0.000;0.000} / Z {landing.Before.Position.Z:+0.000;-0.000;0.000} m", small);
                GUILayout.Label($"反弹 {landing.After.Velocity.Length:0.00} m/s · {landing.After.Spin.Length / BallPhysics.Rps:0.0} rps", small);
            }
            else if (flight.FirstTableHeightCrossing.HasValue)
            {
                V3 p = flight.FirstTableHeightCrossing.Value.Position;
                GUILayout.Label($"台高交点 X {p.X:0.000} / Z {p.Z:0.000} m", small);
            }
            showGuides = GUILayout.Toggle(showGuides, "显示拍面与挥拍方向");
            showSpinAxis = GUILayout.Toggle(showSpinAxis, "角速度轴箭头（高级，不是转动方向）");
            if (selectedReceive != null) showCloud = GUILayout.Toggle(showCloud, "显示复验落点云");
            GUILayout.EndArea();
        }

        void WorldLabel(Vector3 point, string label, float offset = 10)
        {
            Vector3 p = view.WorldToViewportPoint(point);
            if (p.z <= 0 || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) return;
            float x = Mathf.Clamp(ViewArea.x + p.x * ViewArea.width - 46, ViewArea.x + 4, ViewArea.xMax - 100);
            float y = Mathf.Clamp(ViewArea.y + (1 - p.y) * ViewArea.height + offset, ViewArea.y + 38, ViewArea.yMax - 25);
            DrawRect(new Rect(x - 3, y, 102, 23), new Color(0.05f, 0.08f, 0.10f, 0.88f));
            GUI.Label(new Rect(x, y, 100, 23), label, small);
        }

        void TogglePlayback()
        {
            if (flight == null) return;
            if (time >= flight.Samples[flight.Samples.Count - 1].Time) time = StartTime;
            playing = !playing;
        }

        void HandleInterfaceKeys()
        {
            if (Event.current.type != EventType.KeyDown) return;
            if (pickingReceivePoint && Event.current.keyCode == KeyCode.Escape)
            { EndPointPicking(true); Event.current.Use(); return; }
            if (GUI.GetNameOfFocusedControl().StartsWith("parameter:", StringComparison.Ordinal))
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter || Event.current.keyCode == KeyCode.Escape)
                { GUI.FocusControl(null); editField = ""; Event.current.Use(); }
                return;
            }
            if (mode == 3)
            {
                if (Event.current.keyCode == KeyCode.Space) { ToggleStrokePlayback(); Event.current.Use(); }
                if (Event.current.keyCode == KeyCode.R) { time = 0; playing = strokeTimeline.Count > 0; Event.current.Use(); }
                return;
            }
            if (cameraMode == 4)
            {
                if (Event.current.keyCode == KeyCode.Space && serveContact != null)
                { if (!servePlaying && serveTime >= 0.02f) serveTime = -0.02f; servePlaying = !servePlaying; Event.current.Use(); }
                if (Event.current.keyCode == KeyCode.R && serveContact != null)
                { serveTime = -0.02f; servePlaying = true; Event.current.Use(); }
                return;
            }
            if (Event.current.keyCode == KeyCode.Space) { TogglePlayback(); Event.current.Use(); }
            if (Event.current.keyCode == KeyCode.R && flight != null) { time = StartTime; playing = true; Event.current.Use(); }
        }

        void DrawHelpTooltip()
        {
            if (string.IsNullOrEmpty(GUI.tooltip) || Event.current.type != EventType.Repaint) return;
            float width = Mathf.Min(330, UIWidth - 24), height = tooltipStyle.CalcHeight(new GUIContent(GUI.tooltip), width);
            Vector2 mouse = InterfaceMousePosition;
            GUI.Label(new Rect(Mathf.Clamp(mouse.x + 14, 8, UIWidth - width - 8), Mathf.Clamp(mouse.y + 22, 8, UIHeight - height - 8), width, height), GUI.tooltip, tooltipStyle);
        }
    }
}
