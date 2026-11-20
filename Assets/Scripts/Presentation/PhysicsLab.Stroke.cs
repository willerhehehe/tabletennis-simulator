using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TableTennis
{
    public sealed partial class PhysicsLab
    {
        ServeStroke stroke = new ServeStroke();
        readonly ServePresetPanel presetPanel = new ServePresetPanel();
        ServeStrokeResult strokeResult;
        int strokeSettingsTab = 1;
        readonly List<BallState> strokeTimeline = new List<BallState>();
        readonly List<Quaternion> strokeRotations = new List<Quaternion>();
        LineRenderer strokePath;
        Vector2 strokeScroll, strokeResultScroll;
        bool strokePositionOpen, strokeEndpointsOpen = true, strokeRubberOpen, strokeCloseup;
        bool strokeTableOpen = true;
        bool strokeAutoContact = true;
        double strokeContactHeight = 0.95;
        int strokeDragging = -1, strokeDragControl;
        bool strokeDragHeight;
        Vector2 strokeDragMouse, strokeHeightAxis;
        Vector3 strokeDragOrigin, strokeDragHit;
        Plane strokeDragPlane;
        Texture2D strokeHandleTexture;
#if UNITY_EDITOR
        string strokeInputTrace = "";
#endif
        float StrokeEnd => strokeTimeline.Count == 0 ? 0 : Mathf.Max((float)strokeTimeline[strokeTimeline.Count - 1].Time, (float)(stroke.StartTime + stroke.Duration));

        void OpenForwardServe()
        {
            mode = 3; EndPointPicking(false); playing = servePlaying = false;
            referenceFlight = null; reference.positionCount = 0;
            strokeCloseup = false; SetCamera(0); editField = "";
        }

        public void LoadServePreset(ServePreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            var next = preset.CreateStroke(settings, new RubberCoefficients(stroke.Restitution, stroke.Friction, stroke.Tangent), stroke.ResponseId);
            mode = 3; EndPointPicking(false);
            stroke = next; strokeContactHeight = preset.ContactHeight; strokeAutoContact = true;
            presetPanel.Select(preset); editField = ""; strokeCloseup = false;
            Recalculate(); SetCamera(cameraMode == 1 ? 1 : cameraMode == 2 ? 2 : 0);
            notice = L.T(preset.Name) + L.T(" · 已按当前胶皮匹配动作并验证球路");
        }

        static void SetLineSamples(LineRenderer line, IList<BallState> samples)
        {
            line.positionCount = samples?.Count ?? 0;
            for (int i = 0; i < line.positionCount; i++) line.SetPosition(i, U(samples[i].Position));
        }

        void RecalculateStroke()
        {
            spinPreview?.Reset();
            playing = servePlaying = false; time = 0; error = notice = "";
            strokeResult = null; flight = null; serveContact = null;
            strokeTimeline.Clear(); strokeRotations.Clear(); ClearMarkers();
            path.positionCount = incomingPath.positionCount = 0;
            try
            {
                if (strokeAutoContact)
                {
                    strokeContactHeight = Math.Min(strokeContactHeight, stroke.Apex.Y - 0.03);
                    var contact = stroke.AlignContact(settings, strokeContactHeight);
                    V3 normal = stroke.Normal * (stroke.BackFace ? -1 : 1);
                    if (V3.Dot(contact.Velocity - stroke.Velocity, normal) >= -0.01)
                        throw new ArgumentException(L.T("当前方向无法形成迎球撞击，请调整挥拍方向或拍面角度。"));
                }
                strokeResult = stroke.Simulate(settings);
                serveContact = strokeResult.Contact;
                flight = strokeResult.Flight;
                if (serveContact != null) { incoming = serveContact.Before; launch = serveContact.After; }
                strokeTimeline.AddRange(strokeResult.Approach);
                if (flight != null) strokeTimeline.AddRange(flight.Samples);
                SetLineSamples(incomingPath, strokeResult.Approach);
                SetLineSamples(path, flight?.Samples);
                if (flight != null)
                    foreach (var hit in flight.Events)
                        markers.Add(Sphere(hit.Kind, U(hit.Before.Position), 0.045f, hit.Kind == "球台" ? mint : new Color(1, 0.4f, 0.3f)));
                Quaternion rotation = Quaternion.identity;
                for (int i = 0; i < strokeTimeline.Count; i++)
                {
                    if (i > 0)
                    {
                        var previous = strokeTimeline[i - 1];
                        double dt = strokeTimeline[i].Time - previous.Time;
                        double phase = settings.SpinDecay > 0 ? (1 - Math.Exp(-settings.SpinDecay * dt)) / settings.SpinDecay : dt;
                        rotation = Quaternion.AngleAxis((float)(previous.Spin.Length * phase * 180 / Math.PI), U(previous.Spin).normalized) * rotation;
                    }
                    strokeRotations.Add(rotation);
                }
                time = serveContact != null ? (float)serveContact.Before.Time + 0.000001f : 0;
            }
            catch (ArgumentException e) { error = L.T(e.Message); }
            FitCamera();
        }

        int StrokeSampleIndex(float t)
        {
            int low = 0, high = strokeTimeline.Count - 1;
            while (low < high)
            { int mid = (low + high + 1) / 2; if (strokeTimeline[mid].Time <= t) low = mid; else high = mid - 1; }
            return low;
        }

        BallState CurrentStrokeState()
        {
            int index = StrokeSampleIndex(time);
            BallState s = strokeTimeline[index];
            return BallPhysics.Advance(s, index == strokeTimeline.Count - 1 ? 0 : Math.Max(0, time - s.Time), settings);
        }

        bool UpdateStrokeVisuals()
        {
            BuildServeVisuals();
            if (strokePath == null) strokePath = Line("直线挥拍起终点", Array.Empty<Vector3>(), new Color(1, 0.86f, 0.35f), 0.004f);
            strokePath.enabled = true; strokePath.positionCount = 2; strokePath.SetPositions(new[] { U(stroke.Start), U(stroke.End) });
            path.enabled = incomingPath.enabled = true; reference.enabled = false;
            racket.gameObject.SetActive(false); ball.gameObject.SetActive(false);
            racketArrow.enabled = faceArrow.enabled = spinArrow.enabled = false;
            if (landingCloud != null) landingCloud.gameObject.SetActive(false);
            serveRacket.gameObject.SetActive(true); serveBall.gameObject.SetActive(strokeTimeline.Count > 0);
            // The same opaque black/red surfaces appear in every forward view.
            if (serveStyledAsMatched != true || serveStyledAsWide != true)
            {
                for (int i = 0; i < serveRacketRenderers.Length; i++)
                { Color c = serveRacketColors[i]; c.a = 1; serveRacketRenderers[i].sharedMaterial.color = c; }
                serveStyledAsMatched = serveStyledAsWide = true;
            }
            if (playing) { time = Mathf.Min(StrokeEnd, time + Time.deltaTime * playbackSpeed); if (time >= StrokeEnd) playing = false; }
            V3 n = stroke.Normal;
            RacketGeometry.Axes(n, stroke.Roll, out _, out V3 up);
            serveRacket.position = U(stroke.CenterAt(time));
            serveRacket.rotation = Quaternion.LookRotation(U(n), U(up));
            bool hitVisible = serveContact != null && Math.Abs(time - serveContact.Before.Time) < 0.015;
            serveTouch.gameObject.SetActive(hitVisible);
            serveBrush.enabled = hitVisible;
            serveSwing.enabled = serveNormal.enabled = true;
            Arrow(serveNormal, serveRacket.position, U(n) * 0.12f);
            Arrow(serveSwing, serveRacket.position, U(stroke.VelocityAt(time)).normalized * 0.22f);
            if (hitVisible)
            {
                serveTouch.position = U(serveContact.Point);
                Arrow(serveBrush, serveTouch.position, U(serveContact.BrushVelocity).normalized * 0.15f);
            }
            serveDot.gameObject.SetActive(strokeTimeline.Count > 0); serveStripe.enabled = strokeTimeline.Count > 0;
            if (strokeTimeline.Count == 0) return true;
            int index = StrokeSampleIndex(time);
            BallState s = strokeTimeline[index];
            double delta = index == strokeTimeline.Count - 1 ? 0 : Math.Max(0, time - s.Time);
            BallState shown = CurrentStrokeState();
            serveBall.position = U(shown.Position);
            double phaseNow = settings.SpinDecay > 0 ? (1 - Math.Exp(-settings.SpinDecay * delta)) / settings.SpinDecay : delta;
            Quaternion rotation = Quaternion.AngleAxis((float)(s.Spin.Length * phaseNow * 180 / Math.PI), U(s.Spin).normalized) * strokeRotations[index];
            serveStripe.positionCount = 49;
            for (int i = 0; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                serveStripe.SetPosition(i, serveBall.position + rotation * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.0202f);
            }
            serveDot.position = serveBall.position + rotation * Vector3.up * 0.0205f;
            return true;
        }

        void FitStrokeCamera()
        {
            if (cameraAdjusted) return;
            if (strokeCloseup)
            {
                cameraPivot = U(serveContact?.Before.Position ?? stroke.CenterAt(time));
                view.transform.position = cameraPivot + (U(stroke.Normal) + Vector3.right + Vector3.up * 0.3f).normalized;
                view.transform.LookAt(cameraPivot); view.orthographic = true; view.orthographicSize = 0.19f;
            }
            else
            {
                var bounds = new Bounds(new Vector3(0, 0.8f, 0), new Vector3(1.8f, 1.6f, 3));
                bounds.Encapsulate(U(stroke.Apex)); bounds.Encapsulate(U(stroke.Start)); bounds.Encapsulate(U(stroke.End));
                foreach (var s in strokeTimeline) bounds.Encapsulate(U(s.Position));
                cameraPivot = bounds.center;
                Vector3 from = cameraMode == 1 ? Vector3.up : cameraMode == 2 ? Vector3.right : new Vector3(4, 3, -5).normalized;
                view.transform.position = cameraPivot + from * (bounds.extents.magnitude + 3);
                view.transform.LookAt(cameraPivot, cameraMode == 1 ? -Vector3.right : Vector3.up);
                float vertical = 0, horizontal = 0;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 offset = Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    vertical = Mathf.Max(vertical, Mathf.Abs(Vector3.Dot(offset, view.transform.up)));
                    horizontal = Mathf.Max(horizontal, Mathf.Abs(Vector3.Dot(offset, view.transform.right)));
                }
                view.orthographic = true; view.orthographicSize = Mathf.Max(vertical, horizontal / Mathf.Max(0.2f, view.aspect)) + 0.22f;
            }
            fittedAspect = view.aspect;
        }

        void StrokeVector(string label, ref V3 p, bool height)
        {
            GUILayout.Label(label, section);
            double x = p.X, y = p.Y, z = p.Z;
            SettingSlider(label + L.T(" · 左右"), ref x, height ? -3 : -1.5f, height ? 3 : 1.5f, "m");
            if (height) SettingSlider(label + L.T(" · 高度"), ref y, 0.1f, 4, "m");
            SettingSlider(label + L.T(" · 前后"), ref z, height ? -4 : -3, height ? 4 : -0.5f, "m");
            p = new V3(x, y, z);
        }

        void AlignStrokeEndpoint(int index, V3 requested)
        {
            var aim = stroke.Copy(); aim.Start = aim.End = new V3(0, 1, -2); aim.Speed = 0;
            var hit = aim.AlignContact(settings, strokeContactHeight);
            V3 center = aim.CenterAt(hit.Time);
            if (index == 1) { stroke.Start = requested; stroke.End = center * 2 - requested; }
            else { stroke.End = requested; stroke.Start = center * 2 - requested; }
        }

        void DrawStrokeInterface()
        {
            parametersChanged = false; inputError = "";
            DrawRect(new Rect(0, 0, panelWidth, UIHeight), new Color(0.075f, 0.105f, 0.13f));
            GUILayout.BeginArea(new Rect(18, 16, panelWidth - 36, UIHeight - 32));
            GUILayout.Label(L.T("挥拍发球"), heading);
            GUILayout.Label(L.T("落球 + 球拍动作 → 出球"), small);
            GUILayout.Space(12);
            GUILayout.Label(L.T("实验模式"), small);
            int selected = GUILayout.SelectionGrid(0, new[] { L.T("挥拍发球"), L.T("出球模拟"), L.T("单板击球"), L.T("接发研究") }, 2);
            if (selected != 0)
            {
                strokeCloseup = false;
                if (selected == 2) ApplyForehand();
                else if (selected == 3) ApplyServeStudy();
                else { mode = 0; }
                SetCamera(2); parametersChanged = true; editField = "";
            }
            GUILayout.Space(12);
            int settingsTab = GUILayout.Toolbar(strokeSettingsTab, new[] { L.T("发球预设"), L.T("动作参数") });
            if (settingsTab != strokeSettingsTab) { strokeSettingsTab = settingsTab; strokeScroll = Vector2.zero; }
            strokeScroll = GUILayout.BeginScrollView(strokeScroll);
            if (strokeSettingsTab == 0)
            {
            var requestedPreset = presetPanel.Draw(section, small, quietButton, strokeResult);
            if (requestedPreset != null)
            {
                try { LoadServePreset(requestedPreset); }
                catch (ArgumentException e) { notice = L.T("预设载入失败：") + L.T(e.Message); }
            }
            }
            else
            {
            GUILayout.Label(L.T("01  球从顶点下落"), section);
            double height = stroke.Apex.Y;
            SettingSlider(L.T("顶点高度"), ref height, 0.85f, 2.5f, "m");
            stroke.Apex = new V3(stroke.Apex.X, height, stroke.Apex.Z);
            GUILayout.Label(L.T("从无旋、静止的抛球顶点开始计时。高度相对地面，台高 0.76 m。"), small);
            if (Fold(L.T("落球左右与前后位置"), ref strokePositionOpen)) StrokeVector(L.T("落球"), ref stroke.Apex, false);
            GUILayout.Label(L.T("02  拍型与朝向"), section);
            SettingSlider(L.T("拍面与台面夹角"), ref stroke.TableAngle, 0, 180, "°");
            GUILayout.Label(L.T("0° 水平朝上 · 90° 竖直\n180° 水平朝下；角度改变真实拍型。"), small);
            SettingSlider(L.T("拍面朝向 · 左右"), ref stroke.Yaw, -90, 90, "°");
            SettingSlider(L.T("拍柄转角"), ref stroke.Roll, -180, 180, "°");
            GUILayout.Label(L.T("拍柄转角：绕拍面法线旋转，不改变拍面朝向。"), small);
            bool back = GUILayout.Toggle(stroke.BackFace, L.T("翻面：以红面击球"));
            if (back != stroke.BackFace) { stroke.BackFace = back; parametersChanged = true; }
            GUILayout.Label(L.T("正面黑，反面红；蓝箭头指向黑面。"), small);
            GUILayout.Label(L.T("03  直线挥拍"), section);
            SettingSlider(L.T("挥拍速度"), ref stroke.Speed, 0, 25, "m/s");
            GUILayout.Label(L.T("速度独立设置；改变路径长度只改变挥拍时长。"), small);
            if (strokeAutoContact)
            {
                GUILayout.Label(L.T("自动对准触球已开启，位置与时机自动配合落球。"), small);
                SettingSlider(L.T("击球高度"), ref strokeContactHeight, 0.8f, (float)Math.Max(0.8, stroke.Apex.Y - 0.03), "m");
                V3 pathDirection = (stroke.End - stroke.Start).Unit;
                double upAngle = Math.Asin(pathDirection.Y) * 180 / Math.PI;
                double sideAngle = Math.Atan2(pathDirection.X, pathDirection.Z) * 180 / Math.PI;
                SettingSlider(L.T("挥拍向上角"), ref upAngle, -90, 90, "°");
                SettingSlider(L.T("挥拍左右角"), ref sideAngle, -180, 180, "°");
                V3 directionVector = Direction(upAngle, sideAngle);
                if ((directionVector - pathDirection).Length > 1e-6) stroke.End = stroke.Start + directionVector * stroke.PathLength;
            }
            else SettingSlider(L.T("启动时刻"), ref stroke.StartTime, -60, 1.5f, "s");
            GUILayout.Label(stroke.Speed == 0 ? L.T("速度为 0：球拍保持在路径中点。") : L.F("路径长度 {0:0.000} m · 时长 {1:0.000} s", stroke.PathLength, stroke.Duration), small);
            GUILayout.Label(L.F("向右 {0:+0.00;-0.00;0} · 向上 {1:+0.00;-0.00;0} · 向前 {2:+0.00;-0.00;0} m/s", stroke.Velocity.X, stroke.Velocity.Y, stroke.Velocity.Z), small);
            if (stroke.StartTime < 0) GUILayout.Label(L.T("该路径在抛球顶点之前已开始挥动；画面仍从顶点开始回放。"), small);
            if (Fold(L.T("挥拍起点与终点"), ref strokeEndpointsOpen))
            {
                GUILayout.Label(strokeAutoContact ? L.T("拖动一端，另一端联动，保持经过触球点。") : L.T("自由寻碰：起终点与时机独立，可挥空。"), small);
                V3 oldStart = stroke.Start; StrokeVector(L.T("起点"), ref stroke.Start, true);
                if (strokeAutoContact && (oldStart - stroke.Start).Length > 1e-9)
                { try { AlignStrokeEndpoint(1, stroke.Start); } catch (ArgumentException e) { error = L.T(e.Message); } }
                V3 oldEnd = stroke.End; StrokeVector(L.T("终点"), ref stroke.End, true);
                if (strokeAutoContact && (oldEnd - stroke.End).Length > 1e-9)
                { try { AlignStrokeEndpoint(2, stroke.End); } catch (ArgumentException e) { error = L.T(e.Message); } }
            }
            if (Fold(L.T("高级 · 触球"), ref strokeRubberOpen))
            {
                bool auto = GUILayout.Toggle(strokeAutoContact, L.T("自动对准触球（推荐）"));
                if (auto != strokeAutoContact) { strokeAutoContact = auto; parametersChanged = true; }
                GUILayout.Label(L.T("关闭后可独立设置时机、自由寻碰，可能挥空。"), small);
            }
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
            DrawStrokeResultPanel();
            if (parametersChanged) Recalculate();
            DrawStrokeCanvas(); DrawStrokeHandles(); DrawCameraControls();
        }

        V3 StrokePoint(int index) => index == 0 ? stroke.Apex : index == 1 ? stroke.Start : stroke.End;

        Vector2 StrokePointOnScreen(int index)
        {
            Vector3 p = view.WorldToViewportPoint(U(StrokePoint(index)));
            return new Vector2(ViewArea.x + p.x * ViewArea.width, ViewArea.y + (1 - p.y) * ViewArea.height);
        }

        Ray StrokePointerRay(Vector2 mouse) => view.ViewportPointToRay(new Vector3(
            (mouse.x - ViewArea.x) / ViewArea.width, 1 - (mouse.y - ViewArea.y) / ViewArea.height, 0));

        bool BeginStrokePointDrag(int index, Vector2 mouse, bool heightOnly)
        {
            if (mode != 3 || index < 0 || index > 2 || !CameraPointerAllowed(mouse)) return false;
            strokeDragOrigin = U(StrokePoint(index)); strokeDragMouse = mouse; strokeDragHeight = heightOnly || (CompactLayout && mobileHeightDrag);
            if (strokeDragHeight)
            {
                Vector3 a = view.WorldToViewportPoint(strokeDragOrigin), b = view.WorldToViewportPoint(strokeDragOrigin + Vector3.up);
                strokeHeightAxis = new Vector2((b.x - a.x) * ViewArea.width, -(b.y - a.y) * ViewArea.height);
                if (strokeHeightAxis.sqrMagnitude < 1)
                    strokeHeightAxis = Vector2.down * ViewArea.height / (2 * view.orthographicSize);
            }
            else
            {
                strokeDragPlane = new Plane(cameraMode == 2 ? Vector3.right : Vector3.up, strokeDragOrigin);
                Ray ray = StrokePointerRay(mouse);
                if (!strokeDragPlane.Raycast(ray, out float distance))
                { notice = L.T("当前方向难以拖动水平面，请切到俯视或侧视。"); return false; }
                strokeDragHit = ray.GetPoint(distance);
            }
            strokeDragging = index; playing = false; cameraAdjusted = true;
            if (serveContact != null) time = (float)serveContact.Before.Time + 0.000001f;
            return true;
        }

        void MoveStrokePoint(Vector2 mouse)
        {
            if (strokeDragging < 0) return;
            Vector3 point;
            if (strokeDragHeight)
                point = strokeDragOrigin + Vector3.up * (Vector2.Dot(mouse - strokeDragMouse, strokeHeightAxis) / strokeHeightAxis.sqrMagnitude);
            else
            {
                Ray ray = StrokePointerRay(mouse);
                if (!strokeDragPlane.Raycast(ray, out float distance)) return;
                point = strokeDragOrigin + ray.GetPoint(distance) - strokeDragHit;
            }
            bool ballPoint = strokeDragging == 0;
            var p = new V3(Mathf.Clamp(point.x, ballPoint ? -1.5f : -3, ballPoint ? 1.5f : 3),
                Mathf.Clamp(point.y, ballPoint ? 0.85f : 0.1f, ballPoint ? 2.5f : 4),
                Mathf.Clamp(point.z, ballPoint ? -3 : -4, ballPoint ? -0.5f : 4));
            if (strokeDragging == 0) stroke.Apex = p;
            else if (strokeAutoContact)
            { try { AlignStrokeEndpoint(strokeDragging, p); } catch (ArgumentException ex) { error = L.T(ex.Message); return; } }
            else if (strokeDragging == 1) stroke.Start = p; else stroke.End = p;
            Recalculate(); editField = "";
        }

        void DrawStrokeHandles()
        {
            if (strokeHandleTexture == null)
            {
                strokeHandleTexture = new Texture2D(32, 32); strokeHandleTexture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                    strokeHandleTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(15 - Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)))));
                strokeHandleTexture.Apply();
            }
            Event e = Event.current;
            Vector2 mouse = InterfaceMousePosition;
            int control = GUIUtility.GetControlID("StrokePoints".GetHashCode(), FocusType.Passive, ViewArea);
            if (strokeDragging >= 0)
            {
                if (e.rawType == EventType.MouseUp || e.type == EventType.Ignore || GUIUtility.hotControl != strokeDragControl)
                {
                    if (GUIUtility.hotControl == strokeDragControl) GUIUtility.hotControl = 0;
                    strokeDragging = -1; strokeDragControl = 0;
                    if (e.type == EventType.MouseUp) e.Use();
                }
                else if (e.type == EventType.MouseDrag)
                { MoveStrokePoint(mouse); e.Use(); }
            }
            int nearest = -1; float nearestDistance = 18 * 18;
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = StrokePointOnScreen(i);
                if (view.WorldToViewportPoint(U(StrokePoint(i))).z <= 0 || !CameraPointerAllowed(p)) continue;
                float distance = (p - mouse).sqrMagnitude;
                if (distance < nearestDistance) { nearest = i; nearestDistance = distance; }
                Color color = i == 0 ? new Color(1, 0.63f, 0.3f) : new Color(1, 0.86f, 0.35f);
                var rect = new Rect(p.x - 6, p.y - 6, 12, 12);
                Color previous = GUI.color; GUI.color = new Color(0.04f, 0.07f, 0.09f);
                GUI.DrawTexture(rect, strokeHandleTexture); GUI.color = color;
                GUI.DrawTexture(new Rect(p.x - 4, p.y - 4, 8, 8), strokeHandleTexture); GUI.color = previous;
                GUI.Label(new Rect(p.x - 18, p.y - 18, 36, 36), new GUIContent("", (i == 0 ? L.T("拖动落球顶点") : i == 1 ? L.T("拖动挥拍起点") : L.T("拖动挥拍终点")) + L.T("；Shift 只改高度")), small);
                if (strokeDragging == i)
                    GUI.Label(new Rect(ViewArea.x + 18, ViewTop + 35, ViewArea.width - 36, 26),
                        $"{(i == 0 ? L.T("落球顶点") : i == 1 ? L.T("挥拍起点") : L.T("挥拍终点"))}  X {StrokePoint(i).X:0.00} · Y {StrokePoint(i).Y:0.00} · Z {StrokePoint(i).Z:0.00} m", small);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && GUIUtility.hotControl == 0 && nearest >= 0)
            {
                if (BeginStrokePointDrag(nearest, mouse, e.shift))
                { strokeDragControl = control; GUIUtility.hotControl = control; GUI.FocusControl(null); }
                e.Use();
            }
#if UNITY_EDITOR
            if (e.rawType == EventType.MouseDown) strokeInputTrace = $"raw={e.mousePosition}; logical={mouse}; handle={StrokePointOnScreen(2)}; nearest={nearest}; hot={GUIUtility.hotControl}; captured={strokeDragging}; event={e.type}";
#endif
        }

        void DrawStrokeResultPanel()
        {
            if (DrawSpinSettingsPanel()) return;
            DrawRect(new Rect(CanvasRight, 0, ReceivePanelWidth, UIHeight), new Color(0.075f, 0.105f, 0.13f));
            GUILayout.BeginArea(new Rect(CanvasRight + 16, SpinPanelHeight + 12, ReceivePanelWidth - 32, Mathf.Max(30, UIHeight - SpinPanelHeight - 24)));
            GUILayout.Label(L.T("实际出球结果"), heading);
            GUILayout.Label(L.T("由碰撞算出速度与旋转"), small);
            strokeResultScroll = GUILayout.BeginScrollView(strokeResultScroll);
            if (strokeResult == null) GUILayout.Label(error, section);
            else if (serveContact == null) GUILayout.Label(L.T(strokeResult.MissReason) + L.T("\n请调整挥拍位置或启动时刻。"), section);
            else
            {
                var hit = serveContact;
                GUILayout.Label(L.F("{0}击中 · {1:0.000} s", (strokeResult.FrontFace ? L.T("黑面") : L.T("红面")), hit.Before.Time), section);
                double shotAngle = hit.After.Velocity.Length > 1e-10 ? Math.Asin(hit.After.Velocity.Y / hit.After.Velocity.Length) * 180 / Math.PI : 0;
                GUILayout.Label(L.F("拍面夹角 {0:0.#}°\n出球速度 {1:0.00} m/s\n出球仰角 {2:0.0}°", stroke.TableAngle, hit.After.Velocity.Length, shotAngle), small);
                V3 spin = BallSpin.Components(hit.After.Spin / BallPhysics.Rps, hit.After.Velocity);
                GUILayout.Label(L.F("上下旋 {0:+0.0;-0.0;0} rps\n侧旋 {1:+0.0;-0.0;0} rps\n沿球路轴 {2:+0.0;-0.0;0} rps", spin.X, spin.Y, spin.Z), section);
                GUILayout.Label(L.T("上下旋：正为上旋，负为下旋。侧旋：正向发球者右侧弯。"), small);
                GUILayout.Label(L.T("分量沿出球方向定义；示意图固定沿台长边观察。"), small);
                GUILayout.Label(L.T("摩擦位置与方向"), section);
                V3 arm = -hit.Normal;
                GUILayout.Label(L.F("触点在球的{0}；橙点标记触点。\n绿箭头：胶皮相对球面的摩擦方向。", (arm.Y < -0.3 ? L.T("下部") : arm.Y > 0.3 ? L.T("上部") : L.T("侧部"))), small);
                if (GUILayout.Button(L.T("放大并停在触球"), quietButton)) FocusStrokeContact();
                GUILayout.Label(strokeResult.TwoBounces ? L.T("已先落本方，再落对方半台") : L.T("未完成本方 → 对方两次落台"), section);
                if (strokeResult.MayTouchTable) GUILayout.Label(L.T("球拍路径可能碰台，请调整。"), section);
                GUI.enabled = strokeResult.TwoBounces && !strokeResult.MayTouchTable;
                if (GUILayout.Button(L.T("用这个发球研究接法"), primaryButton)) UseStrokeForReceive();
                GUI.enabled = true;
            }
            if (Fold(L.T("球台摩擦与反弹"), ref strokeTableOpen))
            {
                SettingSlider(L.T("球台摩擦 μ"), ref settings.TableFriction, 0, 1);
                SettingSlider(L.T("球台恢复 e"), ref settings.TableRestitution, 0, 1);
                GUILayout.Label(L.T("摩擦同时改变线速度和旋转。下列为模型结果，系数尚待实测标定。"), small);
                int bounce = 0;
                if (flight != null) foreach (var e in flight.Events)
                {
                    if (e.Kind != "球台") continue;
                    GUILayout.Label(L.F("第 {0} 跳 · {1}", ++bounce, (e.Before.Position.Z < 0 ? L.T("本方") : L.T("对方"))), section);
                    V3 before = BallSpin.Components(e.Before.Spin / BallPhysics.Rps, launch.Velocity);
                    V3 after = BallSpin.Components(e.After.Spin / BallPhysics.Rps, launch.Velocity);
                    GUILayout.Label(L.F("速度 {0:0.00} → {1:0.00} m/s\n上下旋 {2:+0.0;-0.0;0} → {3:+0.0;-0.0;0} rps\n侧旋 {4:+0.0;-0.0;0} → {5:+0.0;-0.0;0} rps\n纵轴 {6:+0.0;-0.0;0} → {7:+0.0;-0.0;0} rps", e.Before.Velocity.Length, e.After.Velocity.Length, before.X, after.X, before.Y, after.Y, before.Z, after.Z), small);
                }
            }
            GUILayout.Space(12);
            GUI.enabled = strokeResult != null;
            if (GUILayout.Button(L.T("导出动作与轨迹"), quietButton)) ExportStroke();
            GUI.enabled = true;
            GUILayout.Label(L.T("固定拍型、直线匀速；未建模抛球手、拍边击球及人体动作。"), small);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }

        void DrawStrokeCanvas()
        {
            float left = panelWidth + 18, width = CanvasRight - left - 18;
            DrawRect(new Rect(panelWidth, 0, CanvasRight - panelWidth, ViewTop), new Color(0.055f, 0.08f, 0.10f));
            DrawRect(new Rect(panelWidth, UIHeight - FooterHeight, CanvasRight - panelWidth, FooterHeight), new Color(0.055f, 0.08f, 0.10f));
            GUI.Label(new Rect(left, 16, width, 30), L.T("落球 → 挥拍触球 → 弧线落点"), heading);
            GUI.Label(new Rect(left, 50, width, 26), L.T("橙：落球   黄：球拍路径   青：出球与反弹"), small);
            GUILayout.BeginArea(new Rect(left, 82, width, 38));
            int selected = GUILayout.Toolbar(strokeCloseup ? 3 : cameraMode == 1 ? 0 : cameraMode == 2 ? 1 : 2, new[] { L.T("俯视"), L.T("侧视"), L.T("三维"), L.T("触球放大") });
            int current = strokeCloseup ? 3 : cameraMode == 1 ? 0 : cameraMode == 2 ? 1 : 2;
            if (selected != current)
            {
                if (selected == 3) FocusStrokeContact();
                else { strokeCloseup = false; SetCamera(selected == 0 ? 1 : selected == 1 ? 2 : 0); }
            }
            GUILayout.EndArea();
            if (serveRacket != null) WorldLabel(serveRacket.position, L.F("球拍 {0:0.#}°", stroke.TableAngle), 20);
            if (!strokeCloseup)
            {
                WorldLabel(U(stroke.Apex), L.T("落球顶点"), -30);
                WorldLabel(U(stroke.Start), L.T("挥拍起点"), 20); WorldLabel(U(stroke.End), L.T("挥拍终点"), 20);
            }
            if (strokeTimeline.Count > 0)
            {
                var s = CurrentStrokeState();
                string stage = serveContact == null ? L.T("落球 / 漏球") : time < serveContact.Before.Time - 0.001 ? L.T("下落中") : Math.Abs(time - serveContact.Before.Time) < 0.015 ? L.T("触球瞬间") : L.T("出球与反弹");
                GUI.Label(new Rect(left, ViewTop + 6, width, 25), L.F("{0}  ·  当前 {1:0.00} m/s / {2:0.0} rps", stage, s.Velocity.Length, s.Spin.Length / BallPhysics.Rps), small);
            }
            string problem = string.IsNullOrEmpty(inputError) ? error : inputError;
            if (strokeResult != null && serveContact == null) problem = L.T(strokeResult.MissReason);
            if (!string.IsNullOrEmpty(problem))
            {
                DrawRect(new Rect(left, ViewTop + 38, width, 64), new Color(0.23f, 0.16f, 0.12f));
                GUI.Label(new Rect(left + 10, ViewTop + 44, width - 20, 56), problem, small);
            }
            GUILayout.BeginArea(new Rect(left, UIHeight - FooterHeight + 8, width, FooterHeight - 12));
            GUILayout.BeginHorizontal();
            GUI.enabled = strokeTimeline.Count > 0;
            if (GUILayout.Button(playing ? L.T("暂停") : L.T("慢放全程"), primaryButton, GUILayout.Width(104))) ToggleStrokePlayback();
            if (GUILayout.Button(L.T("重播"), GUILayout.Width(L.Current == Language.French ? 64 : 60))) { time = 0; playing = true; }
            if (GUILayout.Button($"{playbackSpeed:0.###}×", GUILayout.Width(72))) playbackSpeed = playbackSpeed == 0.1f ? 0.025f : playbackSpeed == 0.025f ? 1 : 0.1f;
            GUI.enabled = serveContact != null;
            if (GUILayout.Button(L.T("停在触球"))) { time = (float)serveContact.Before.Time + 0.000001f; playing = false; }
            GUI.enabled = true; GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            float t = GUILayout.HorizontalSlider(time, 0, Mathf.Max(0.001f, StrokeEnd));
            if (t != time) { time = t; playing = false; }
            GUILayout.Label($"{time:0.000} s", value, GUILayout.Width(90)); GUILayout.EndHorizontal();
            GUILayout.Label(string.IsNullOrEmpty(notice) ? L.T("参数改变即重算；空格播放/暂停 · 球与拍保持实际尺寸") : notice, small);
            GUILayout.EndArea();
        }

        void FocusStrokeContact()
        { playing = false; time = serveContact != null ? (float)serveContact.Before.Time + 0.000001f : 0; strokeCloseup = true; SetCamera(3); }

        void ToggleStrokePlayback()
        {
            if (strokeTimeline.Count == 0) return;
            if (!playing && (time >= StrokeEnd || (serveContact != null && Math.Abs(time - serveContact.Before.Time) < 0.001))) time = 0;
            playing = !playing;
        }

        void UseStrokeForReceive()
        {
            if (strokeResult == null || !strokeResult.TwoBounces || strokeResult.MayTouchTable) return;
            BallState s = strokeResult.Contact.After; PhysicsSettings p = settings.Copy();
            ApplyServeStudy(); settings = p;
            speed = (float)s.Velocity.Length; elevation = (float)(Math.Asin(s.Velocity.Y / speed) * 180 / Math.PI);
            direction = (float)(Math.Atan2(s.Velocity.X, s.Velocity.Z) * 180 / Math.PI);
            originX = (float)s.Position.X; originY = (float)s.Position.Y; originZ = (float)s.Position.Z;
            SetWorldSpin(s.Spin / BallPhysics.Rps); Recalculate(); SetCamera(2);
            notice = L.T("已复制这次实际出球；在右半台轨迹选接球点，再测试接法。");
        }

        [Serializable] sealed class StrokeExport
        {
            public string model = "apex-to-straight-stroke-v2; independent speed; SI units; angles degrees; spin rad/s";
            public double[] apex, start, end, outgoingVelocity, outgoingSpin;
            public double startTime, duration, tableAngle, yaw, roll, restitution, friction, tangent, contactTime;
            public double speed;
            public bool backFace;
            public bool automaticContact;
            public string presetReference;
            public string rubberCalibration;
            public int responseId;
            public double contactHeight;
            public string result;
            public double gravity, airDensity, drag, liftSlope, liftCap, spinDecay, tableFriction, tableRestitution, step, flightDuration;
            public bool netEnabled;
            public StrokeExport(PhysicsLab lab)
            {
                double[] Values(V3 v) => new[] { v.X, v.Y, v.Z };
                var s = lab.stroke; var p = lab.settings;
                apex = Values(s.Apex); start = Values(s.Start); end = Values(s.End);
                startTime = s.StartTime; duration = s.Duration; tableAngle = s.TableAngle; yaw = s.Yaw; roll = s.Roll;
                speed = s.Speed;
                restitution = s.Restitution; friction = s.Friction; tangent = s.Tangent; backFace = s.BackFace;
                responseId = s.ResponseId;
                rubberCalibration = responseId == 0 ? RubberCalibrations.Identify(restitution, friction, tangent) : InvertedRubberResponses.Get(responseId).Key;
                automaticContact = lab.strokeAutoContact; contactHeight = lab.strokeContactHeight;
                presetReference = lab.presetPanel.Selected?.Id;
                result = lab.strokeResult.Contact == null ? lab.strokeResult.MissReason : lab.strokeResult.TwoBounces ? "two-bounce serve" : "hit, invalid two-bounce serve";
                if (lab.strokeResult.Contact != null)
                { var c = lab.strokeResult.Contact; contactTime = c.Before.Time; outgoingVelocity = Values(c.After.Velocity); outgoingSpin = Values(c.After.Spin); }
                gravity = p.Gravity; airDensity = p.AirDensity; drag = p.Drag; liftSlope = p.LiftSlope; liftCap = p.LiftCap;
                spinDecay = p.SpinDecay; tableFriction = p.TableFriction; tableRestitution = p.TableRestitution;
                step = p.Step; flightDuration = p.Duration; netEnabled = p.NetEnabled;
            }
        }

        void ExportStroke()
        {
            if (strokeResult == null) return;
            try
            {
                string root = exportFiles.WriteStroke(strokeTimeline, JsonUtility.ToJson(new StrokeExport(this), true));
                WebPlatform.DeliverExport(root);
                notice = L.T("已导出 ") + Path.GetFileName(root) + L.T("（参数与全程数据）");
            }
            catch (Exception e) { notice = L.T("导出失败：") + L.T(e.Message); }
        }
    }
}
