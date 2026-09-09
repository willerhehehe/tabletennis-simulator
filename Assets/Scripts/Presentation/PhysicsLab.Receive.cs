using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TableTennis
{
    public sealed partial class PhysicsLab
    {
        float receiveDelay = 0.1f, receiveBallError = 0.3f, receiveSpinError = 8, receiveFaceError = 1, receiveRacketError = 0.25f, receiveSpeedLimit = 8, receiveNetLimitCm = 15;
        Vector2 receiveScroll;
        bool receiveManual, receiveAdvanced, receiveHelp, receiveFailures, keepStudyResults, studyDirty;
        int receiveTab, receiveDirection, receiveSpinDirection;
        bool receiveDirectionOpen;
        bool pickingReceivePoint;
        int cameraBeforePicking;
        LineRenderer pickGuide;
        readonly List<BallState> pickStates = new List<BallState>();
        Reception reception;
        FlightResult serveFlight;
        BallState serveInitial;
        ReceiveReport receiveReport;
        ReceiveScore selectedReceive;
        readonly ReceiveStudyRunner receiveRunner = new ReceiveStudyRunner(Application.platform == RuntimePlatform.WebGLPlayer);
        MeshFilter landingCloud;
        const float ReceiveFooterHeight = 160;
        static readonly string[] ReceiveSpinDirections = { "不限制", "顺旋转接", "逆旋转接" };
        static readonly string[] ReceiveDirections = { "全部方向", "迎前上提", "后撤缓冲", "向下切削" };
        float ReceivePanelWidth => mode == 2 || cameraMode == 4 ? Mathf.Clamp(UIWidth * 0.225f, 320, 360) : 300;
        float CanvasRight => UIWidth - ReceivePanelWidth;
        bool IsStudying => receiveRunner.IsRunning;
        bool CanEvaluateReceive => mode == 2 && reception != null && !IsStudying && string.IsNullOrEmpty(inputError);
        V3 RacketVelocity => mode == 2 ? ManualReceiveAction.Velocity : new V3(racketSide, racketUp, racketForward);
        V3 RacketNormal => mode == 2 ? ManualReceiveAction.Normal : Direction(facePitch, faceYaw);
        ReceiveAction ManualReceiveAction => new ReceiveAction(facePitch, faceYaw, racketForward, racketUp, racketSide);

        ReceiveStudySettings StudySettings() => new ReceiveStudySettings
        {
            BallVelocityError = receiveBallError, SpinErrorRps = receiveSpinError,
            FaceErrorDegrees = receiveFaceError, RacketVelocityError = receiveRacketError, MaxRacketSpeed = receiveSpeedLimit, RubberResponseId = rubberResponseId,
            RacketRollDegrees = racketRoll, DirectionConstraint = receiveDirection, SpinDirection = (SpinReceiveDirection)receiveSpinDirection, MaxNetClearance = receiveNetLimitCm / 100.0
        };

        void ApplyServeStudy()
        {
            EndPointPicking(false); studyDirty = false; receiveTab = receiveDirection = receiveSpinDirection = 0; receiveScroll = Vector2.zero;
            mode = 2; settings = new PhysicsSettings(); editField = "";
            speed = 6; elevation = -15; direction = spinY = spinZ = 0; spinX = -40;
            originX = 0; originY = 1; originZ = -1.65f;
            facePitch = 10; faceYaw = racketSide = 0; racketForward = racketUp = 1; racketRoll = -70;
            if (!receiveEquipmentChosen) { rubberResponseId = 0; rubberBounce = rubberFriction = 0.8f; rubberTangent = 0.15f; }
            receiveDelay = 0.1f;
            receiveReport = null; selectedReceive = null;
        }

        void UpdateReceiveTask()
        {
            receiveRunner.Pump();
            if (!receiveRunner.TryComplete(out var report, out var failure, out bool cancelled)) return;
            if (cancelled)
            { receiveTab = receiveReport == null ? 0 : 1; notice = receiveReport == null ? "已取消，可继续调整接法。" : "已取消，上一轮结果已保留。"; return; }
            if (failure != null)
            { receiveTab = receiveReport == null ? 0 : 1; notice = "评估失败：" + failure.Message + (receiveReport != null ? "；上一轮结果已保留。" : ""); return; }
            receiveReport = report;
            receiveManual = false; receiveTab = 1; receiveScroll = Vector2.zero; studyDirty = false; showCloud = true;
            ApplyReceiveScore(receiveReport.Ranked[0]);
            SetCamera(1);
            if (receiveRunner.TestingCurrent) notice = $"当前接法已测试 {receiveReport.Settings.Trials} 次，可微调后再测或搜索其他接法。";
            else notice = receiveReport.Ranked[0].QualifiedSuccesses == 0 ? "未找到上台且高度达标的接法，请调整接球点、动作范围或高度上限。" :
                $"已评估 {receiveReport.EvaluatedCandidates} 种接法，并以独立的 {receiveReport.Settings.Trials} 次扰动复验候选。";
        }

        public void StartReceiveSearch() => StartReceiveTask(false);
        public void TestCurrentReceive() => StartReceiveTask(true);

        void StartReceiveTask(bool currentOnly)
        {
            if (IsStudying) return;
            if (mode != 2 || reception == null)
            { notice = "请先设置有效发球和接球时刻。"; return; }
            if (!string.IsNullOrEmpty(inputError)) { notice = "请先修正未应用的无效输入。"; return; }
            EndPointPicking(false); receiveTab = 1; receiveScroll = Vector2.zero; studyDirty = false;
            BallState input = incoming; PhysicsSettings p = settings.Copy(); ReceiveStudySettings study = StudySettings();
            ReceiveAction baseline = currentOnly ? ManualReceiveAction : receiveReport?.Baseline.Action ?? ManualReceiveAction;
            double bounce = rubberBounce, friction = rubberFriction, tangent = rubberTangent;
            receiveRunner.Start(input, p, study, baseline, bounce, friction, tangent, currentOnly);
            notice = currentOnly ? "正在测试当前拍型和挥拍，不搜索其他动作…" : "正在搜索指定范围内更稳的接法…";
        }

        void ApplyReceiveScore(ReceiveScore score)
        {
            selectedReceive = score; keepStudyResults = true;
            receiveFailures = score.Successes == 0;
            facePitch = (float)score.Action.Pitch; faceYaw = (float)score.Action.Yaw;
            racketForward = (float)score.Action.Forward; racketUp = (float)score.Action.Up; racketSide = (float)score.Action.Side;
            editField = ""; Recalculate(); DrawLandingCloud(score); FitCamera();
            time = 0; playing = false;
        }

        void ClearLandingCloud()
        {
            if (landingCloud != null && landingCloud.sharedMesh != null)
            { Destroy(landingCloud.sharedMesh); landingCloud.sharedMesh = null; }
        }

        void DrawLandingCloud(ReceiveScore score)
        {
            ClearLandingCloud();
            if (landingCloud == null)
            {
                var obj = new GameObject("扰动落点云"); obj.transform.SetParent(world);
                landingCloud = obj.AddComponent<MeshFilter>(); obj.AddComponent<MeshRenderer>().sharedMaterial = lineMaterial;
            }
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            foreach (var trial in score.Trials)
            {
                if (!trial.Landing.HasValue) continue;
                V3 p = trial.Landing.Value;
                if (Math.Abs(p.X) > 3 || Math.Abs(p.Z) > 5) continue;
                int i = vertices.Count; float x = (float)p.X, z = (float)p.Z, r = 0.008f;
                vertices.Add(new Vector3(x-r, 0.766f, z-r)); vertices.Add(new Vector3(x+r, 0.766f, z-r));
                vertices.Add(new Vector3(x+r, 0.766f, z+r)); vertices.Add(new Vector3(x-r, 0.766f, z+r));
                Color c = trial.Success ? new Color(0.4f, 1, 0.72f, 0.7f) : new Color(1, 0.4f, 0.2f, 0.8f);
                for (int j = 0; j < 4; j++) colors.Add(c);
                triangles.AddRange(new[] { i, i+2, i+1, i, i+3, i+2 });
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            landingCloud.sharedMesh = mesh;
        }

        void StudyBounds(ref Bounds bounds)
        {
            if (pickingReceivePoint) foreach (var sample in pickStates) bounds.Encapsulate(U(sample.Position));
            if (selectedReceive == null) return;
            foreach (var trial in selectedReceive.Trials)
                if (trial.Landing.HasValue && Math.Abs(trial.Landing.Value.X) < 3 && Math.Abs(trial.Landing.Value.Z) < 5)
                    bounds.Encapsulate(U(trial.Landing.Value));
        }

        bool CanPickReceivePoint => mode == 2 && !IsStudying && serveFlight != null && serveFlight.Events.Count >= 2 &&
            serveFlight.Events[0].Kind == "球台" && serveFlight.Events[1].Kind == "球台" &&
            serveFlight.Events[0].Before.Position.Z < 0 && serveFlight.Events[1].Before.Position.Z > 0;

        void BeginPointPicking()
        {
            if (!CanPickReceivePoint) return;
            cameraBeforePicking = cameraMode; pickingReceivePoint = true; receiveTab = 0;
            playing = false; time = 0; BuildPickGuide(); SetCamera(2);
        }

        void EndPointPicking(bool restoreCamera)
        {
            bool wasPicking = pickingReceivePoint; pickingReceivePoint = false;
            if (pickGuide != null) pickGuide.positionCount = 0;
            pickStates.Clear();
            if (restoreCamera && wasPicking && view != null) SetCamera(cameraBeforePicking);
        }

        void BuildPickGuide()
        {
            pickStates.Clear();
            if (!CanPickReceivePoint) { EndPointPicking(false); return; }
            double bounce = serveFlight.Events[1].After.Time;
            double stop = Math.Min(bounce + 0.4, serveFlight.Events.Count > 2 ? serveFlight.Events[2].Before.Time - 0.000001 : double.MaxValue);
            foreach (var sample in serveFlight.Samples)
                if (sample.Time >= bounce + 0.01 && sample.Time <= stop && sample.Position.Z > 0) pickStates.Add(sample);
            if (pickGuide == null) pickGuide = Line("可选择的接球轨迹", Array.Empty<Vector3>(), new Color(0.8f, 0.98f, 0.94f), 0.012f);
            pickGuide.positionCount = pickStates.Count;
            for (int i = 0; i < pickStates.Count; i++) pickGuide.SetPosition(i, U(pickStates[i].Position));
        }

        public bool TrySelectReceivePoint(Vector2 guiPosition)
        {
            if (!pickingReceivePoint || !CanPickReceivePoint || !CameraPointerAllowed(guiPosition)) return false;
            float nearest = 24 * 24; BallState? chosen = null;
            foreach (var sample in pickStates)
            {
                Vector3 p = view.WorldToViewportPoint(U(sample.Position));
                if (p.z <= 0 || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) continue;
                Vector2 screen = new Vector2(ViewArea.x + p.x * ViewArea.width, ViewArea.y + (1 - p.y) * ViewArea.height);
                float distance = (screen - guiPosition).sqrMagnitude;
                if (distance < nearest) { nearest = distance; chosen = sample; }
            }
            if (!chosen.HasValue) { notice = "请点击高亮的可接球轨迹。"; return false; }
            receiveDelay = (float)(chosen.Value.Time - serveFlight.Events[1].After.Time);
            EndPointPicking(false); Recalculate(); time = 0; playing = false;
            notice = $"接球点已更新：弹起后 {receiveDelay * 1000:0} ms";
            return true;
        }

        void HandleCanvasPointClick()
        {
            if (pickingReceivePoint && Event.current.type == EventType.MouseDown && Event.current.button == 0 && CameraPointerAllowed(InterfaceMousePosition))
            { TrySelectReceivePoint(InterfaceMousePosition); Event.current.Use(); }
        }

        void DrawReceivePanel()
        {
            if (DrawSpinSettingsPanel()) return;
            if (cameraMode == 4) { DrawServeContactPanel(); return; }
            if (mode != 2) return;
            float x = CanvasRight + 18, width = ReceivePanelWidth - 36;
            DrawRect(new Rect(CanvasRight, 0, ReceivePanelWidth, UIHeight), new Color(0.075f, 0.105f, 0.13f));
            DrawRect(new Rect(CanvasRight, 0, 1, UIHeight), new Color(0.19f, 0.25f, 0.28f));
            GUILayout.BeginArea(new Rect(x, SpinPanelHeight + 12, width, 86));
            GUILayout.Label("02  接球实验", heading); GUILayout.Space(10);
            int tab = GUILayout.Toolbar(receiveTab, new[] { "设置接法", receiveReport == null ? "候选方案" : $"候选方案 · {receiveReport.Ranked.Count}" });
            if (tab != receiveTab) { receiveTab = tab; receiveScroll = Vector2.zero; }
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(x, SpinPanelHeight + 102, width, Mathf.Max(24, UIHeight - SpinPanelHeight - 102 - ReceiveFooterHeight - 12)));
            receiveScroll = GUILayout.BeginScrollView(receiveScroll);
            if (receiveTab == 0) DrawReceiveSettings(); else DrawReceiveCandidates();
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUI.enabled = true;
            DrawRect(new Rect(CanvasRight + 12, UIHeight - ReceiveFooterHeight, ReceivePanelWidth - 24, 1), new Color(0.20f, 0.27f, 0.29f));
            GUILayout.BeginArea(new Rect(x, UIHeight - ReceiveFooterHeight + 10, width, ReceiveFooterHeight - 16));
            if (IsStudying)
            {
                float fraction = receiveRunner.Total > 0 ? Mathf.Clamp01((float)receiveRunner.Done / receiveRunner.Total) : 0;
                GUILayout.Label(receiveRunner.TestingCurrent ? "正在测试当前接法…" : $"评估中  {fraction:P0}    {receiveRunner.Done}/{receiveRunner.Total}", small);
                Rect bar = GUILayoutUtility.GetRect(10, 5, GUILayout.ExpandWidth(true));
                DrawRect(bar, new Color(0.18f, 0.25f, 0.28f)); DrawRect(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), mint);
                GUILayout.Space(7);
                if (GUILayout.Button("取消评估")) receiveRunner.Cancel();
            }
            else
            {
                GUI.enabled = CanEvaluateReceive;
                if (GUILayout.Button(receiveReport == null ? "搜索更稳接法" : "重新搜索接法", primaryButton))
                {
                    if (parametersChanged) { Recalculate(); parametersChanged = false; }
                    StartReceiveSearch();
                }
                GUI.enabled = CanEvaluateReceive;
                if (GUILayout.Button(new GUIContent("仅测试当前接法", "只对当前拍型与挥拍做扰动试验，不搜索其他动作。"), quietButton))
                {
                    if (parametersChanged) { Recalculate(); parametersChanged = false; }
                    TestCurrentReceive();
                }
                GUI.enabled = true;
                if (receiveReport != null)
                {
                    if (GUILayout.Button("导出方案与复验数据", quietButton)) ExportReceiveReport();
                }
                else GUILayout.Label(reception == null ? "请先调整发球或接球点。" : "固定击球点 · 每个接法 256 次试验", small);
            }
            GUILayout.EndArea();
        }

        void DrawReceiveSettings()
        {
            GUI.enabled = !IsStudying;
            GUILayout.Label("接旋转思路", section);
            int spinChoice = GUILayout.Toolbar(receiveSpinDirection, ReceiveSpinDirections);
            if (spinChoice != receiveSpinDirection) { receiveSpinDirection = spinChoice; parametersChanged = true; }
            GUILayout.Label(receiveSpinDirection == 0 ? "同时探索顺旋转和逆旋转方向。" : receiveSpinDirection == 1 ?
                "挥拍沿触点球面转动的方向，搜索拍型与速度。" : "挥拍迎着触点球面转动的方向，搜索拍型与速度。", small);
            GUILayout.Label("按接球点的实际旋转和候选拍型判断；不按屏幕左右或发球名称判断。", small);
            if (reception != null && incoming.Spin.Length < 1e-6 && receiveSpinDirection != 0)
                GUILayout.Label("当前接球点无旋转，请选择“不限制”。", small);
            if (Fold("动作范围（高级）", ref receiveDirectionOpen))
            {
                int selectedDirection = GUILayout.SelectionGrid(receiveDirection, ReceiveDirections, 2);
                if (selectedDirection != receiveDirection) { receiveDirection = selectedDirection; parametersChanged = true; }
                GUILayout.Label("限制名义动作方向，仍叠加动作误差。", small);
            }
            GUILayout.Label("回球高度要求", section);
            Slider("过网高度上限", ref receiveNetLimitCm, 1, 100, "cm");
            GUILayout.Label("球底到网顶的距离。每次扰动均检查；上台但过高不计达标。15 cm 是可调实验值，不代表防抢攻保证。", small);
            GUILayout.Label("接球时刻", section);
            float milliseconds = receiveDelay * 1000;
            if (Slider("右半台弹起后", ref milliseconds, 10, 400, "ms")) receiveDelay = milliseconds / 1000;
            GUI.enabled = CanPickReceivePoint;
            if (GUILayout.Button(pickingReceivePoint ? "正在选点 · 点击轨迹" : "在轨迹上选择接球点", quietButton)) BeginPointPicking();
            GUI.enabled = !IsStudying;
            if (reception != null)
            {
                GUILayout.Space(8);
                GUILayout.Label($"到达时  {incoming.Velocity.Length:0.00} m/s  ·  {incoming.Spin.Length / BallPhysics.Rps:0.0} rps", small);
                GUILayout.Label(new GUIContent($"接球点  X {incoming.Position.X:0.00} / Y {incoming.Position.Y:0.00} / Z {incoming.Position.Z:0.00} m", $"角速度 X {incoming.Spin.X / BallPhysics.Rps:0.0} / Y {incoming.Spin.Y / BallPhysics.Rps:0.0} / Z {incoming.Spin.Z / BallPhysics.Rps:0.0} rps"), small);
            }
            if (Fold("手动设置拍型与挥拍", ref receiveManual))
            {
                Slider("拍面开合", ref facePitch, -20, 55, "°");
                GUILayout.Label("0° 为竖直；正值后仰打开，负值前压。", small);
                Slider("拍面侧偏", ref faceYaw, -35, 35, "°");
                Slider("拍柄转角", ref racketRoll, -180, 180, "°");
                bool speedChanged = Slider("迎前速度", ref racketForward, -RacketMotionLimits.Component, RacketMotionLimits.Component, "m/s");
                speedChanged |= Slider("向上速度", ref racketUp, -RacketMotionLimits.Component, RacketMotionLimits.Component, "m/s");
                speedChanged |= Slider("横向速度", ref racketSide, -RacketMotionLimits.Component, RacketMotionLimits.Component, "m/s");
                if (speedChanged) receiveSpeedLimit = Mathf.Min(RacketMotionLimits.Total,
                    Mathf.Max(receiveSpeedLimit, (float)ManualReceiveAction.Velocity.Length + Mathf.Sqrt(3) * receiveRacketError));
                GUILayout.Label("迎前朝发球方；负值为向后缓冲。横向为世界 X 轴，与俯视画面的上下对应；调大速度时同步提高合速度上限。", small);
                if (GUILayout.Button("查看触球特写", quietButton)) { time = 0; playing = false; SetCamera(3); }
            }
            if (Fold("误差范围与挥拍上限", ref receiveAdvanced))
            {
                GUILayout.Label("每项均为正负方向的均匀扰动。", small);
                Slider("球速误差 / 轴", ref receiveBallError, 0, 2, "m/s");
                Slider("转速误差 / 轴", ref receiveSpinError, 0, 50, "rps");
                Slider("拍型角度误差", ref receiveFaceError, 0, 10, "°");
                Slider("挥拍误差 / 轴", ref receiveRacketError, 0, 2, "m/s");
                Slider("挥拍合速度上限", ref receiveSpeedLimit, 1, RacketMotionLimits.Total, "m/s");
            }
            GUILayout.Space(12);
            GUILayout.Label("当前误差设定", section);
            GUILayout.Label($"球速 ±{receiveBallError:0.##} m/s/轴  ·  旋转 ±{receiveSpinError:0.#} rps/轴\n拍型 ±{receiveFaceError:0.#}°  ·  挥拍 ±{receiveRacketError:0.##} m/s/轴", small);
            if (Fold("评估假设与搜索范围", ref receiveHelp))
            {
                GUILayout.Label("假定每次已击中固定接球点。保守排除触球前后 10 ms 球拍可能穿入台板的动作；不包含人体可达性和漏球。参数仍需实测标定。", small);
                GUILayout.Label("仰角 −20…55°、偏角 ±35°；迎前、上提、横向各 ±20 m/s，实际受所选方向和合速度上限限制。先评估 256 个候选，再局部细化；复验使用共同的 256 次扰动，种子 42。", small);
            }
            GUI.enabled = true;
        }

        void DrawReceiveCandidates()
        {
            if (IsStudying)
            {
                GUILayout.Space(25); GUILayout.Label(receiveRunner.TestingCurrent ? "正在测试当前接法" : "正在比较接法", heading);
                GUILayout.Label(receiveRunner.TestingCurrent ? "保留当前拍型与速度，只叠加来球和动作误差。" : "先筛选拍型与速度，再用另一组扰动复验。", small);
                if (receiveReport != null) GUILayout.Label("画面仍为上一轮结果；取消会保留它。新试验完成后再更新。", small);
                return;
            }
            if (receiveReport == null)
            {
                GUILayout.Space(25); GUILayout.Label(studyDirty ? "参数已改变" : "还没有候选方案", heading);
                GUILayout.Space(10);
                GUILayout.Label(studyDirty ? "旧统计已失效。可仅测试当前接法，或搜索新的接法。" : "先在设置页选好接球点，再测试当前接法或搜索更稳接法。", small);
                if (GUILayout.Button("返回接球设置", quietButton)) receiveTab = 0;
                return;
            }
            ReceiveScore current = selectedReceive ?? receiveReport.Ranked[0];
            GUILayout.BeginVertical(cardStyle);
            GUILayout.BeginHorizontal(); GUILayout.Label(receiveReport.EvaluatedCandidates == 1 ? "当前接法 · 上台且高度达标" : "当前方案 · 上台且高度达标", small);
            if (GUILayout.Button("微调", quietButton, GUILayout.Width(50), GUILayout.Height(24))) { receiveTab = 0; receiveManual = true; receiveScroll = Vector2.zero; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label(current.QualifiedRate.ToString("P1"), metricStyle);
            GUILayout.BeginVertical(); GUILayout.Label($"{current.QualifiedSuccesses} / {current.Attempts} 次", small);
            if (receiveReport.EvaluatedCandidates > 1) GUILayout.Label($"手调基线 {receiveReport.Baseline.QualifiedRate:P1}", small);
            GUILayout.EndVertical(); GUILayout.EndHorizontal();
            GUILayout.Label($"合法上台率 {current.Rate:P1} · 上台但过高/未过网 {current.Successes - current.QualifiedSuccesses} 次", small);
            GUILayout.Label($"过网上限 {receiveReport.Settings.MaxNetClearance * 100:0.#} cm · 非实战保证", small);
            GUILayout.EndVertical();
            GUILayout.Label($"接旋转思路：{ReceiveSpinDirections[(int)receiveReport.Settings.SpinDirection]}", small);
            if (!receiveReport.Settings.Allows(current.Action, receiveReport.Incoming)) GUILayout.Label("手调基线不在当前方向范围内，仅供对照。", small);
            GUILayout.Label($"方向范围：{ReceiveDirections[receiveReport.Settings.DirectionConstraint]}", small);
            GUILayout.Label(ActionDescription(current.Action), small);
            GUILayout.BeginHorizontal();
            GUI.enabled = flight != null;
            if (GUILayout.Button("播放回球")) { SetCamera(2); time = 0; playing = true; }
            if (GUILayout.Button("看触球")) { time = 0; playing = false; SetCamera(3); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (receiveReport.EvaluatedCandidates > 1)
            {
                GUI.enabled = selectedReceive != receiveReport.Baseline;
                if (GUILayout.Button($"对照手调基线 · {receiveReport.Baseline.QualifiedRate:P1}", quietButton)) ApplyReceiveScore(receiveReport.Baseline);
                GUI.enabled = true;
            }
            if (Fold($"当前接法失误 · {current.Attempts - current.Successes} 次", ref receiveFailures))
            {
                if (current.Failures.Count == 0) GUILayout.Label("本批未观察到失误，不代表现实中永远成功。", small);
                foreach (var pair in current.Failures) GUILayout.Label($"{pair.Key}   {pair.Value} / {current.Attempts}", small);
            }
            if (receiveReport.EvaluatedCandidates == 1) return;
            GUILayout.Label($"全部 {receiveReport.Ranked.Count} 个复验方案", section);
            GUILayout.BeginHorizontal(); GUILayout.Label("", GUILayout.Width(40));
            GUILayout.Label("达标率", small, GUILayout.Width(84));
            GUILayout.Label(new GUIContent("  余量", "达标样本中距触网、高度上限和台边的最小余量，取 10% 分位值。"), small, GUILayout.Width(72));
            GUILayout.EndHorizontal();
            for (int i = 0; i < receiveReport.Ranked.Count; i++)
            {
                var score = receiveReport.Ranked[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{i + 1:00}", small, GUILayout.Width(40)); GUILayout.Label($"{score.QualifiedRate * 100:0.0}%", value, GUILayout.Width(84));
                GUILayout.Label($"  {score.Margin10 * 100:0.0} cm", small, GUILayout.Width(72));
                bool selected = selectedReceive == score;
                GUI.enabled = !selected;
                if (GUILayout.Button(selected ? "已选" : "选用", GUILayout.Width(52))) ApplyReceiveScore(score);
                GUI.enabled = true; GUILayout.EndHorizontal();
                if (!receiveReport.Settings.Allows(score.Action, receiveReport.Incoming)) GUILayout.Label("手调基线 · 范围外对照", small);
                GUILayout.Label(ActionDescription(score.Action), small);
                GUILayout.Space(8);
            }
        }

        static string ActionDescription(ReceiveAction action) =>
            $"开合 {action.Pitch:+0.0;-0.0;0.0}° · 侧偏 {action.Yaw:+0.0;-0.0;0.0}°\n" +
            $"迎前 {action.Forward:+0.00;-0.00;0.00} / 上提 {action.Up:+0.00;-0.00;0.00} / 横向 {action.Side:+0.00;-0.00;0.00} m/s";

        public void ExportReceiveReport()
        {
            if (receiveReport == null) return;
            try
            {
                string stem = exportFiles.WriteReceive(receiveReport, JsonUtility.ToJson(new ExportSettings(this), true));
                WebPlatform.DeliverExport(stem);
                notice = "稳定性报告已导出：" + stem + ".csv";
            }
            catch (Exception e) { notice = "报告导出失败：" + e.Message; }
        }
    }
}
