using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TableTennis
{
    public sealed partial class PhysicsLab : MonoBehaviour
    {
        LabScene scene;
        readonly RubberPresetPanel rubberPresets = new RubberPresetPanel();
        ExperimentFiles exportFiles;
        PhysicsSettings settings = new PhysicsSettings();
        FlightResult flight;
        BallState launch, incoming;
        Camera view;
        Transform ball, racket, world;
        LineRenderer path, reference, incomingPath, racketArrow, faceArrow, spinArrow;
        List<BallState> approach = new List<BallState>();
        readonly List<GameObject> markers = new List<GameObject>();
        LabTheme theme;
        FlightResult referenceFlight;
        BallState referenceLaunch;
        Vector3 contactRacket;
        Material lineMaterial;
        Font font;
        GUISkin labSkin;
        GUIStyle heading, small, value, section, statusStyle, primaryButton, quietButton, cardStyle, metricStyle, tooltipStyle;
        Vector2 scroll;
        float speed = 8, elevation = 10, direction, spinX = 60, spinY, spinZ;
        float originX = -0.15f, originY = 1.1f, originZ = -1.65f;
        float racketForward = 3, racketUp = 3, racketSide, facePitch = 8, faceYaw, racketRoll = -70;
        int rubberResponseId;
        float rubberBounce = 0.8f, rubberFriction = 0.8f, rubberTangent = 0.15f;
        float time, playbackSpeed = 0.25f;
        int mode, cameraMode;
        bool playing, advanced, parametersChanged, showGuides = true;
        string shotExample = "";
        string error = "", notice = "", referenceLabel = "", editField = "", editText = "", inputError = "";
        float panelWidth;
        float fittedAspect;
        const float FooterHeight = 132, ViewTop = 124;
        float StartTime => mode != 0 && approach.Count > 0 ? (float)approach[0].Time : 0;
        readonly Color mint = new Color(0.45f, 0.96f, 0.77f);
        readonly Color lavender = new Color(0.66f, 0.60f, 0.94f);

        void Start()
        {
            Application.targetFrameRate = 60;
            font = Resources.Load<Font>("NotoSansCJKsc-Regular");
            exportFiles = new ExperimentFiles(WebPlatform.ExportDirectory);
            BuildWorld(); OpenForwardServe(); Recalculate();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lab-capture") >= 0) StartCoroutine(CaptureReview());
        }

        System.Collections.IEnumerator CaptureReview()
        {
            for (int i = 0; i < 15; i++) yield return null;
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory("Artifacts");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/unity-preview.png"));
        }

        static Vector3 U(V3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static V3 Direction(double pitch, double yaw)
        {
            pitch *= Math.PI / 180; yaw *= Math.PI / 180;
            return new V3(Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch));
        }

        void BuildWorld()
        {
            scene = new LabScene(); world = scene.Root; lineMaterial = scene.LineMaterial;
            ball = Sphere("球 · 40 mm", Vector3.zero, 0.04f, new Color(1, 0.78f, 0.39f)).transform;
            ball.GetComponent<Renderer>().sharedMaterial.shader = Shader.Find("Unlit/Color");
            racket = CreateRacket("球拍 · 触球前后 10 ms");
            path = Line("当前轨迹", Array.Empty<Vector3>(), mint, 0.009f);
            reference = Line("参照轨迹", Array.Empty<Vector3>(), lavender, 0.006f);
            incomingPath = Line("触球前自由飞行", Array.Empty<Vector3>(), new Color(1, 0.63f, 0.3f), 0.009f);
            racketArrow = Line("挥拍方向", Array.Empty<Vector3>(), new Color(1, 0.86f, 0.35f), 0.01f);
            faceArrow = Line("拍面法向", Array.Empty<Vector3>(), new Color(0.37f, 0.74f, 1), 0.005f);
            spinArrow = Line("角速度轴", Array.Empty<Vector3>(), lavender, 0.009f);
            view = new GameObject("试验视角").AddComponent<Camera>(); view.transform.SetParent(world);
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(0.065f, 0.085f, 0.11f);
            view.nearClipPlane = 0.03f; SetCamera(2);
        }

        Transform CreateRacket(string name) => scene.CreateRacket(name);
        GameObject Sphere(string name, Vector3 p, float size, Color color) => scene.Sphere(name, p, size, color);
        LineRenderer Line(string name, Vector3[] points, Color color, float width) => scene.Line(name, points, color, width);

        void SetCamera(int index)
        {
            cameraAdjusted = false; cameraDragButton = -1;
            cameraMode = index; view.orthographic = index != 0;
            Vector3 target = new Vector3(0, 0.8f, 0);
            cameraPivot = target;
            if (index == 0) { view.transform.position = new Vector3(2.6f, 2.5f, -3.5f); view.fieldOfView = 35; }
            if (index == 1) { view.transform.position = new Vector3(0, 6, -0.01f); view.orthographicSize = 2.2f; }
            if (index == 2) { view.transform.position = new Vector3(5, 1.2f, 0); view.orthographicSize = 1.9f; }
            view.transform.LookAt(target);
            FitCamera();
        }

        void FitCamera()
        {
            fittedAspect = view.aspect;
            if (cameraAdjusted) return;
            if (mode == 3) { FitStrokeCamera(); return; }
            if (cameraMode == 4) { FitServeCamera(); return; }
            if ((flight == null && serveFlight == null) || cameraMode == 0) return;
            fittedAspect = view.aspect;
            if (cameraMode == 3 && flight != null)
            {
                view.transform.position = contactRacket + new Vector3(0.85f, 0.45f, mode == 2 ? -0.70f : 0.70f);
                cameraPivot = contactRacket + Vector3.up * 0.04f;
                view.transform.LookAt(cameraPivot);
                view.orthographicSize = 0.42f;
                return;
            }
            var bounds = new Bounds(new Vector3(0, 0.7f, 0), new Vector3(1.8f, 1.4f, 3.2f));
            foreach (var s in (flight ?? serveFlight).Samples)
                if (Math.Abs(s.Position.X) < 3 && Math.Abs(s.Position.Z) < 5 && s.Position.Y < 3) bounds.Encapsulate(U(s.Position));
            if (referenceFlight != null)
                foreach (var s in referenceFlight.Samples)
                    if (Math.Abs(s.Position.X) < 3 && Math.Abs(s.Position.Z) < 5 && s.Position.Y < 3) bounds.Encapsulate(U(s.Position));
            foreach (var s in approach) bounds.Encapsulate(U(s.Position));
            StudyBounds(ref bounds);
            Vector3 center = bounds.center;
            cameraPivot = center;
            if (cameraMode == 1)
            {
                view.transform.position = center + new Vector3(0, 6, -0.001f);
                view.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z / Mathf.Max(view.aspect, 0.5f)) + 0.3f;
            }
            else
            {
                view.transform.position = center + Vector3.right * 6;
                view.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.z / Mathf.Max(view.aspect, 0.5f)) + 0.25f;
            }
            view.transform.LookAt(center, cameraMode == 1 ? -Vector3.right : Vector3.up);
        }

        public void Recalculate()
        {
            spinPreview?.Reset();
            if (!keepStudyResults)
            {
                if (receiveReport != null) studyDirty = true;
                receiveReport = null; selectedReceive = null; ClearLandingCloud();
            }
            keepStudyResults = false; reception = null; serveFlight = null;
            if (mode == 3) { RecalculateStroke(); return; }
            RecalculateServeContact();
            try
            {
                V3 spin = new V3(spinX, spinY, spinZ) * BallPhysics.Rps;
                V3 position = new V3(originX, originY, originZ);
                incoming = new BallState(position, Direction(elevation, direction) * speed, spin);
                launch = incoming;
                V3 normal = RacketNormal;
                if (mode == 1)
                {
                    incoming.Velocity = Direction(elevation, direction) * -speed;
                    launch = BallPhysics.Strike(incoming, normal, RacketVelocity, rubberBounce, rubberFriction, rubberTangent, rubberResponseId);
                }
                if (mode == 2)
                {
                    serveInitial = incoming;
                    serveFlight = BallPhysics.Simulate(serveInitial, settings);
                    reception = Reception.FromServe(serveFlight, settings, receiveDelay);
                    incoming = reception.Incoming;
                    launch = BallPhysics.Strike(incoming, normal, RacketVelocity, rubberBounce, rubberFriction, rubberTangent, rubberResponseId);
                    position = incoming.Position;
                }
                flight = BallPhysics.Simulate(launch, settings, mode == 2 ? -1 : 1);
                approach = mode == 2 ? reception.Approach : mode == 1 ? BallPhysics.PreviewIncoming(incoming, settings) : new List<BallState>();
                var incomingPoints = new Vector3[approach.Count];
                for (int i = 0; i < incomingPoints.Length; i++) incomingPoints[i] = U(approach[i].Position);
                incomingPath.positionCount = incomingPoints.Length; incomingPath.SetPositions(incomingPoints);
                var points = new Vector3[flight.Samples.Count];
                for (int i = 0; i < points.Length; i++) points[i] = U(flight.Samples[i].Position);
                path.positionCount = points.Length; path.SetPositions(points);
                ClearMarkers();
                foreach (var contact in flight.Events)
                {
                    Color color = contact.Kind == "球台" ? mint : new Color(1, 0.4f, 0.32f);
                    markers.Add(Sphere(contact.Kind, U(contact.Before.Position), 0.055f, color));
                }
                if (mode == 2)
                    foreach (var bounce in reception.Bounces) markers.Add(Sphere(bounce.Kind, U(bounce.Before.Position), 0.04f, new Color(1, 0.63f, 0.3f)));
                racket.gameObject.SetActive(mode != 0);
                contactRacket = U(position - normal * (BallPhysics.Radius + 0.004));
                racket.position = contactRacket;
                racket.rotation = Quaternion.LookRotation(U(normal)) * Quaternion.AngleAxis(racketRoll, Vector3.forward);
                time = StartTime; playing = false; error = ""; notice = "";
                FitCamera();
                if (pickingReceivePoint) BuildPickGuide();
            }
            catch (Exception e)
            {
                EndPointPicking(false);
                error = e.Message; playing = false; flight = null; path.positionCount = 0;
                approach.Clear(); incomingPath.positionCount = racketArrow.positionCount = faceArrow.positionCount = spinArrow.positionCount = 0;
                ball.gameObject.SetActive(false); racket.gameObject.SetActive(false);
                ClearMarkers();
                if (mode == 2 && serveFlight != null)
                {
                    incomingPath.positionCount = serveFlight.Samples.Count;
                    for (int i = 0; i < serveFlight.Samples.Count; i++) incomingPath.SetPosition(i, U(serveFlight.Samples[i].Position));
                    if (cameraMode == 3) SetCamera(2); else FitCamera();
                }
            }
        }

        void ClearMarkers()
        {
            foreach (var marker in markers) { Destroy(marker.GetComponent<Renderer>().sharedMaterial); Destroy(marker); }
            markers.Clear();
        }

        void Update()
        {
            UpdateTouchCamera(); UpdateReceiveTask();
            if (UpdateServeVisuals()) return;
            if (flight == null) return;
            float end = (float)flight.Samples[flight.Samples.Count - 1].Time;
            if (playing) { time = Mathf.Min(time + Time.deltaTime * playbackSpeed, end); if (time >= end) playing = false; }
            BallState current = StateAt(time);
            ball.gameObject.SetActive(true); ball.position = U(current.Position);
            // ponytail: this is a local contact visualization; full strokes need measured motion or an arm model.
            racket.position = contactRacket + U(RacketVelocity) * Mathf.Clamp(time, -(float)RacketGeometry.ContactWindow, (float)RacketGeometry.ContactWindow);
            Arrow(spinArrow, ball.position, showGuides ? U(current.Spin).normalized * 0.22f : Vector3.zero);
            Arrow(racketArrow, racket.position, showGuides && mode != 0 ? U(RacketVelocity).normalized * 0.4f : Vector3.zero);
            Arrow(faceArrow, racket.position, showGuides && mode != 0 ? U(RacketNormal) * 0.22f : Vector3.zero);
        }

        static void Arrow(LineRenderer line, Vector3 origin, Vector3 delta)
        {
            if (delta.sqrMagnitude < 1e-10f) { line.positionCount = 0; return; }
            Vector3 tip = origin + delta, axis = delta.normalized;
            float head = Mathf.Min(0.065f, delta.magnitude * 0.22f);
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up).normalized * Mathf.Min(0.035f, delta.magnitude * 0.12f);
            line.positionCount = 5;
            line.SetPositions(new[] { origin, tip, tip - axis * head + side, tip, tip - axis * head - side });
        }

        BallState StateAt(float t)
        {
            var points = t < 0 && approach.Count > 0 ? approach : flight.Samples;
            int low = 0, high = points.Count - 1;
            while (low < high) { int mid = (low + high + 1) / 2; if (points[mid].Time <= t) low = mid; else high = mid - 1; }
            if (low == points.Count - 1) return points[low];
            BallState a = points[low], b = points[low + 1];
            double span = b.Time - a.Time, f = span > 0 ? Math.Max(0, Math.Min(1, (t - a.Time) / span)) : 1;
            return new BallState(a.Position + (b.Position - a.Position) * f, a.Velocity + (b.Velocity - a.Velocity) * f,
                a.Spin + (b.Spin - a.Spin) * f, t);
        }

        void Styles()
        {
            if (theme != null) return;
            theme = new LabTheme(GUI.skin, font); labSkin = theme.Skin;
            heading = theme.heading;
            small = theme.small;
            value = theme.value;
            section = theme.section;
            statusStyle = theme.statusStyle;
            primaryButton = theme.primaryButton;
            quietButton = theme.quietButton;
            cardStyle = theme.cardStyle;
            metricStyle = theme.metricStyle;
            tooltipStyle = theme.tooltipStyle;
        }

        void OnGUI()
        {
            if (view == null) return;
            GUISkin previous = GUI.skin;
            Matrix4x4 previousMatrix = GUI.matrix;
            try
            {
                guiScale = InterfaceScale();
                GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1));
                Styles(); GUI.skin = labSkin; LayoutInterface(); HandleInterfaceKeys();
                if (CompactLayout) { DrawMobileInterface(); return; }
                DrawRect(new Rect(CanvasRight, 0, ReceivePanelWidth, UIHeight), new Color(0.075f, 0.105f, 0.13f));
                DrawInterface(); DrawPersistentSpinPanel(); DrawHelpTooltip();
            }
            finally { GUI.matrix = previousMatrix; GUI.skin = previous; }
        }

        bool Slider(string label, ref float number, float min, float max, string unit = "")
        {
            bool changed = false;
            if (webNumbers.TryGetValue(label, out float edited))
            {
                webNumbers.Remove(label);
                if (edited >= min && edited <= max && GUI.enabled) { number = edited; changed = parametersChanged = true; }
            }
            bool editable = GUI.enabled;
            string control = "parameter:" + label;
            bool focused = GUI.GetNameOfFocusedControl() == control;
            EventType inputEvent = Event.current.type;
            string text = focused && editField == control ? editText : number.ToString(focused ? "G7" : "0.###", CultureInfo.InvariantCulture);
            GUILayout.BeginHorizontal(); GUI.enabled = true;
            GUILayout.Label(new GUIContent(label, $"范围 {min} 至 {max} {unit}"));
            string typed = text;
            if (editable && Application.platform == RuntimePlatform.WebGLPlayer)
            {
                if (GUILayout.Button(text, GUILayout.Width(80))) WebPlatform.EditNumber(gameObject.name, label, number, min, max);
            }
            else if (editable) { GUI.SetNextControlName(control); typed = GUILayout.TextField(text, GUILayout.Width(80)); }
            else GUILayout.Label(number.ToString("0.###", CultureInfo.InvariantCulture), value, GUILayout.Width(80));
            GUILayout.Label(unit, value, GUILayout.Width(32)); GUILayout.EndHorizontal();
            GUI.enabled = editable;
            if (typed != text && (inputEvent == EventType.KeyDown || inputEvent == EventType.ExecuteCommand))
            {
                editField = control; editText = typed;
                if (PhysicsSettings.TryNumber(typed, min, max, out double parsed) && number != (float)parsed)
                { number = (float)parsed; changed = parametersChanged = true; }
            }
            if (GUI.GetNameOfFocusedControl() == control && editField == control)
            {
                if (!PhysicsSettings.TryNumber(editText, min, max, out _)) inputError = $"{label}：请输入 {min} 至 {max} {unit}，无效输入尚未应用。";
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                { GUI.FocusControl(null); editField = ""; Event.current.Use(); }
            }
            float slid = GUILayout.HorizontalSlider(number, min, max);
            if (slid != number) { number = slid; changed = parametersChanged = true; editField = ""; }
            return changed;
        }
        void SettingSlider(string label, ref double number, float min, float max, string unit = "")
        { float original = (float)number, f = original; Slider(label, ref f, min, max, unit); if (f != original) number = f; }
        static void DrawRect(Rect rect, Color color)
        { Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous; }

        void DrawResponseDetails(int id, BallState? state, V3 normal, V3 velocity)
        {
            GUILayout.Label($"已启用 R{id:00} 整拍速度响应", small);
            if (!state.HasValue) return;
            var c = InvertedRubberResponses.Get(id).At(state.Value, normal, velocity, out bool inRange);
            GUILayout.Label($"当前触球 e {c.Restitution:0.000} · μ {c.Friction:0.000} · β {c.Tangent:0.000}", small);
            if (!inRange) GUILayout.Label("当前触球超出训练覆盖范围；系数取边界值，结果属于外推。", small);
        }

        void ApplyForehand()
        {
            shotExample = "正手拉下旋 · 触球示例";
            editField = "";
            mode = 1; settings = new PhysicsSettings();
            racketRoll = -70;
            speed = ForehandBackspin.Speed; elevation = ForehandBackspin.Descent;
            direction = spinY = spinZ = racketSide = faceYaw = 0;
            spinX = ForehandBackspin.SpinRps;
            originX = ForehandBackspin.X; originY = ForehandBackspin.Y; originZ = ForehandBackspin.Z;
            facePitch = ForehandBackspin.FacePitch; racketForward = ForehandBackspin.Forward; racketUp = ForehandBackspin.Up;
            if (!receiveEquipmentChosen) { rubberResponseId = 0; rubberBounce = ForehandBackspin.Restitution; rubberFriction = ForehandBackspin.Friction; rubberTangent = ForehandBackspin.Tangent; }
        }

        public void ApplyBackhandFlick()
        {
            EndPointPicking(false); editField = ""; mode = 1; settings = new PhysicsSettings();
            speed = BackhandFlick.Speed; elevation = BackhandFlick.Descent; direction = spinY = spinZ = 0;
            spinX = BackhandFlick.SpinRps;
            originX = BackhandFlick.X; originY = BackhandFlick.Y; originZ = BackhandFlick.Z;
            facePitch = BackhandFlick.FacePitch; faceYaw = BackhandFlick.FaceYaw; racketRoll = BackhandFlick.Roll;
            racketForward = BackhandFlick.Forward; racketUp = BackhandFlick.Up; racketSide = BackhandFlick.Side;
            if (!receiveEquipmentChosen) { rubberResponseId = 0; rubberBounce = (float)BackhandFlick.Rubber.Restitution; rubberFriction = (float)BackhandFlick.Rubber.Friction; rubberTangent = (float)BackhandFlick.Rubber.Tangent; }
            shotExample = receiveEquipmentChosen ? "右手反手拧拉 · 当前器材 · 触球示例" : "右手反手拧拉 · R01 反胶 · 触球示例";
            referenceFlight = null; reference.positionCount = 0; referenceLabel = "";
            parametersChanged = true; scroll = Vector2.zero;
        }

        public void Export()
        {
            if (mode == 3) { ExportStroke(); return; }
            if (flight == null) return;
            try
            {
                string stem = exportFiles.WriteFlight(flight, mode == 0 ? null : approach, incoming, launch, JsonUtility.ToJson(new ExportSettings(this), true));
                WebPlatform.DeliverExport(stem);
                notice = "已导出：" + stem + ".csv（含事件和参数文件）";
                Debug.Log(notice);
            }
            catch (Exception e) { notice = "导出失败：" + e.Message; Debug.LogError(notice); }
        }

        [Serializable] sealed class ExportSettings
        {
            public string units = "SI; angular velocity rad/s", model = "empirical-v1";
            public string verdict;
            public double incomingPreviewSeconds;
            public int targetSide;
            public double[] servePosition, serveVelocity, serveSpin;
            public double receiveAfterBounce;
            public ReceiveStudySettings receiveUncertainty;
            public ServeContactExport servingContact;
            public int inputMode;
            public double[] position, velocity, spin;
            public double[] incomingVelocity, incomingSpin, racketNormal, racketContactVelocity;
            public double rubberRestitution, rubberFriction, rubberTangentRestitution, racketRollDegrees;
            public string rubberCalibration;
            public int rubberResponseId;
            public double gravity, airDensity, drag, liftSlope, liftCap, spinDecay, tableRestitution, tableFriction, step, duration;
            public bool netEnabled;
            public ExportSettings(PhysicsLab lab)
            {
                PhysicsSettings p = lab.settings; BallState s = lab.flight != null ? lab.launch : lab.incoming;
                verdict = lab.flight?.Verdict ?? lab.error; incomingPreviewSeconds = lab.approach.Count > 0 ? -lab.approach[0].Time : 0;
                targetSide = lab.flight?.TargetSide ?? (lab.mode == 2 ? -1 : 1);
                inputMode = lab.mode; position = new[] { s.Position.X, s.Position.Y, s.Position.Z };
                if (lab.mode != 1) servingContact = new ServeContactExport(lab);
                velocity = new[] { s.Velocity.X, s.Velocity.Y, s.Velocity.Z }; spin = new[] { s.Spin.X, s.Spin.Y, s.Spin.Z };
                if (lab.mode != 0)
                {
                    V3 v = lab.incoming.Velocity, w = lab.incoming.Spin, n = lab.RacketNormal, rv = lab.RacketVelocity;
                    incomingVelocity = new[] { v.X, v.Y, v.Z }; incomingSpin = new[] { w.X, w.Y, w.Z };
                    racketNormal = new[] { n.X, n.Y, n.Z };
                    racketContactVelocity = new[] { rv.X, rv.Y, rv.Z };
                    racketRollDegrees = lab.racketRoll;
                    rubberRestitution = lab.rubberBounce; rubberFriction = lab.rubberFriction; rubberTangentRestitution = lab.rubberTangent;
                    rubberResponseId = lab.rubberResponseId;
                    rubberCalibration = rubberResponseId == 0 ? RubberCalibrations.Identify(rubberRestitution, rubberFriction, rubberTangentRestitution) : InvertedRubberResponses.Get(rubberResponseId).Key;
                }
                if (lab.mode == 2)
                {
                    var v = lab.serveInitial;
                    servePosition = new[] { v.Position.X, v.Position.Y, v.Position.Z }; serveVelocity = new[] { v.Velocity.X, v.Velocity.Y, v.Velocity.Z };
                    serveSpin = new[] { v.Spin.X, v.Spin.Y, v.Spin.Z }; receiveAfterBounce = lab.receiveDelay;
                    receiveUncertainty = lab.receiveReport?.Settings ?? lab.StudySettings();
                }
                gravity = p.Gravity; airDensity = p.AirDensity; drag = p.Drag; liftSlope = p.LiftSlope; liftCap = p.LiftCap;
                spinDecay = p.SpinDecay; tableRestitution = p.TableRestitution; tableFriction = p.TableFriction;
                step = p.Step; duration = p.Duration; netEnabled = p.NetEnabled;
            }
        }

        void OnDestroy()
        {
            spinPreview?.Dispose();
            receiveRunner.Dispose(); ClearLandingCloud();
            scene?.Dispose();
            theme?.Dispose();
            if (strokeHandleTexture != null) Destroy(strokeHandleTexture);
        }
    }
}
