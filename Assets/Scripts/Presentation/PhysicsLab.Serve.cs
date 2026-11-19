using System;
using UnityEngine;

namespace TableTennis
{
    public sealed partial class PhysicsLab
    {
        static readonly string[] SpinNames = { "不转", "纯上旋", "纯下旋", "纯侧旋", "侧上旋", "侧下旋" };
        ServeContact serveContact;
        string serveContactError = "";
        Transform serveRacket, serveBall, serveTouch, serveDot;
        LineRenderer serveSwing, serveBrush, serveNormal, serveStripe;
        Renderer[] serveRacketRenderers;
        Color[] serveRacketColors;
        bool? serveStyledAsMatched, serveStyledAsWide;
        float serveTime;
        bool servePlaying, serveZoom, showSpinAxis;
        bool showServeTrajectory = true;
        int cameraBeforeServe = 2;
        int serveRouteView = 2;
        Vector2 serveScroll;
        V3 SpinTravel => Direction(0, direction) * (mode == 1 ? -1 : 1);
        V3 WorldSpinRps => new V3(spinX, spinY, spinZ);
        BallState ServeTarget => new BallState(new V3(originX, originY, originZ), Direction(elevation, direction) * speed, WorldSpinRps * BallPhysics.Rps);

        void SetWorldSpin(V3 spin)
        { spinX = (float)spin.X; spinY = (float)spin.Y; spinZ = (float)spin.Z; editField = ""; }

        void DrawSpinControls()
        {
            GUILayout.Label(mode == 1 ? L.T("来球旋转 · 沿来球方向看") : L.T("旋转 · 从发球者方向看"), section);
            V3 c = BallSpin.Components(WorldSpinRps, SpinTravel);
            int kind = BallSpin.Kind(c);
            int chosen = GUILayout.SelectionGrid(kind == 6 ? -1 : kind, L.All(SpinNames), 3);
            float strength = (float)c.Length;
            float share = Math.Abs(c.X) + Math.Abs(c.Y) > 0.01 ? (float)(Math.Abs(c.Y) / (Math.Abs(c.X) + Math.Abs(c.Y))) * 100 : 50;
            int sign = c.Y < 0 ? -1 : 1;
            if (chosen >= 0 && chosen != kind)
            {
                if (chosen >= 4 && (share < 1 || share > 99)) share = 50;
                SetWorldSpin(BallSpin.Preset(chosen, strength < 0.01 ? 40 : strength, share / 100, sign, SpinTravel));
                parametersChanged = true;
                c = BallSpin.Components(WorldSpinRps, SpinTravel); strength = (float)c.Length; kind = chosen;
            }
            if (kind > 0 && kind < 6)
            {
                bool change = Slider(L.T("旋转强度"), ref strength, 0, 260, "rps");
                if (kind >= 3)
                {
                    GUILayout.Label(L.T("从发球者看：侧旋使球向哪边弯"), small);
                    int side = GUILayout.Toolbar(sign < 0 ? 0 : 1, new[] { L.T("向左弯"), L.T("向右弯") });
                    if (side != (sign < 0 ? 0 : 1)) { sign = side == 0 ? -1 : 1; change = true; }
                }
                if (kind >= 4) change |= Slider(L.T("侧旋比重"), ref share, 0, 100, "%");
                if (change)
                { SetWorldSpin(BallSpin.Preset(kind, strength, share / 100, sign, SpinTravel)); parametersChanged = true; }
                GUILayout.Label(kind == 1 || kind == 4 ? L.T("上表面向前转；侧视更容易看清。") : kind == 2 || kind == 5 ? L.T("上表面向发球者回转；侧视更容易看清。") : L.T("绕竖直方向旋转；俯视更容易看清。"), small);
            }
            else GUILayout.Label(kind == 0 ? L.T("没有自转；选择一种旋转即可开始调节。") : L.T("当前含纵向旋转；选基础类型会清除纵向分量。"), small);
            if (mode != 1 && GUILayout.Button(L.T("反算触球示例"), quietButton)) OpenServeContact();
            if (Fold(L.T("高级 · 世界坐标三轴旋转"), ref serveSpinOpen))
            {
                Slider(L.T("角速度 X"), ref spinX, -150, 150, "rps");
                Slider(L.T("角速度 Y"), ref spinY, -150, 150, "rps");
                Slider(L.T("角速度 Z"), ref spinZ, -150, 150, "rps");
                GUILayout.Label(L.T("这是角速度轴分量，不是球面移动方向。"), small);
            }
        }

        void RecalculateServeContact()
        {
            servePlaying = false; serveTime = 0; serveContact = null; serveContactError = "";
            if (mode == 1) return;
            try { serveContact = ServeContact.Solve(ServeTarget, stroke.Restitution, stroke.Friction, stroke.Tangent, stroke.ResponseId); }
            catch (ArgumentException e) { serveContactError = L.T(e.Message); }
        }

        void OpenServeContact()
        {
            if (mode == 1) return;
            if (cameraMode != 4) cameraBeforeServe = cameraMode;
            EndPointPicking(false); playing = false; servePlaying = false; serveTime = 0;
            SetCamera(4);
        }

        void FitServeCamera()
        {
            if (cameraAdjusted) return;
            V3 n = serveContact?.Normal ?? BallSpin.Forward(ServeTarget.Velocity);
            V3 right = V3.Cross(new V3(0, 1, 0), BallSpin.Forward(ServeTarget.Velocity));
            Vector3 center = U(ServeTarget.Position);
            cameraPivot = center;
            view.transform.position = center + U(-n + right * 1.1 + new V3(0, 0.45, 0)).normalized * 0.7f;
            view.transform.LookAt(center);
            view.orthographic = true; view.orthographicSize = serveZoom ? 0.085f : 0.25f;
            fittedAspect = view.aspect;
        }

        void FitServeTrajectory()
        {
            if (cameraMode != 4) return;
            showServeTrajectory = true;
            var tableBounds = new Bounds(new Vector3(0, 0.7f, 0), new Vector3(1.8f, 1.4f, 3.2f));
            var bounds = tableBounds;
            bounds.Encapsulate(U(ServeTarget.Position));
            foreach (var line in new[] { path, incomingPath, reference })
                for (int i = 0; i < line.positionCount; i++)
                {
                    Vector3 point = line.GetPosition(i);
                    if (!float.IsNaN(point.sqrMagnitude) && !float.IsInfinity(point.sqrMagnitude)) bounds.Encapsulate(point);
                }
            cameraPivot = bounds.center;
            Vector3 from = serveRouteView == 0 ? Vector3.up : serveRouteView == 1 ? Vector3.right : new Vector3(4, 3, -5).normalized;
            if (serveRouteView == 2 && serveContact != null && Mathf.Abs(Vector3.Dot(from, U(serveContact.Normal))) < 0.35f)
                from = (from + U(serveContact.Normal) * 0.7f).normalized;
            view.transform.position = cameraPivot + from * (bounds.extents.magnitude + 2);
            view.transform.LookAt(cameraPivot, serveRouteView == 0 ? -Vector3.right : Vector3.up);
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            void Project(Vector3 point)
            {
                if (float.IsNaN(point.sqrMagnitude) || float.IsInfinity(point.sqrMagnitude)) return;
                Vector3 offset = point - cameraPivot;
                float x = Vector3.Dot(offset, view.transform.right), y = Vector3.Dot(offset, view.transform.up);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            foreach (var box in new[] { tableBounds, new Bounds(U(ServeTarget.Position), Vector3.one * 0.4f) })
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                Project(box.center + Vector3.Scale(box.extents, new Vector3(x, y, z)));
            foreach (var line in new[] { path, incomingPath, reference })
                for (int i = 0; i < line.positionCount; i++) Project(line.GetPosition(i));
            Vector3 shift = view.transform.right * ((minX + maxX) * 0.5f) + view.transform.up * ((minY + maxY) * 0.5f);
            cameraPivot += shift; view.transform.position += shift;
            float horizontal = (maxX - minX) * 0.5f, vertical = (maxY - minY) * 0.5f;
            // Leave space for the legend and view controls inside the viewport.
            float content = Mathf.Max(0.25f, (ViewArea.height - 120) / ViewArea.height);
            view.orthographic = true;
            view.orthographicSize = Mathf.Max(vertical, horizontal / Mathf.Max(view.aspect, 0.2f)) / content + 0.12f;
            cameraAdjusted = true; fittedAspect = view.aspect;
        }

        void BuildServeVisuals()
        {
            if (serveRacket != null) return;
            serveRacket = CreateRacket(L.T("发球拍 · 可复现触球示例"));
            foreach (var r in serveRacket.GetComponentsInChildren<Renderer>())
                if (r.name != "拍柄")
                {
                    r.sharedMaterial.shader = Shader.Find("Sprites/Default");
                    Color c = r.sharedMaterial.color; c.a = r.name == "胶皮" ? 0.25f : 0.09f; r.sharedMaterial.color = c;
                }
            serveRacketRenderers = serveRacket.GetComponentsInChildren<Renderer>();
            serveRacketColors = Array.ConvertAll(serveRacketRenderers, r => r.sharedMaterial.color);
            serveBall = Sphere("发球 · 40 mm", Vector3.zero, 0.04f, new Color(0.98f, 0.96f, 0.85f)).transform;
            serveTouch = Sphere("球面摩擦接触点", Vector3.zero, 0.008f, new Color(1, 0.36f, 0.15f)).transform;
            serveDot = Sphere("球面跟踪标记", Vector3.zero, 0.003f, new Color(0.07f, 0.10f, 0.12f)).transform;
            foreach (var t in new[] { serveBall, serveTouch, serveDot }) t.GetComponent<Renderer>().sharedMaterial.shader = Shader.Find("Unlit/Color");
            serveSwing = Line("发球触点速度", Array.Empty<Vector3>(), new Color(1, 0.86f, 0.35f), 0.003f);
            serveBrush = Line("沿拍面摩擦方向", Array.Empty<Vector3>(), mint, 0.003f);
            serveNormal = Line("发球拍面法向", Array.Empty<Vector3>(), new Color(0.37f, 0.74f, 1), 0.002f);
            serveStripe = Line("球面转动标线", Array.Empty<Vector3>(), new Color(0.07f, 0.10f, 0.12f), 0.001f);
        }

        bool UpdateServeVisuals()
        {
            if (strokePath != null) strokePath.enabled = mode == 3;
            if (mode == 3) return UpdateStrokeVisuals();
            bool detail = cameraMode == 4;
            bool showRoutes = !detail || showServeTrajectory;
            path.enabled = reference.enabled = incomingPath.enabled = showRoutes;
            racketArrow.enabled = faceArrow.enabled = !detail;
            spinArrow.enabled = !detail && showSpinAxis;
            foreach (var marker in markers) marker.SetActive(showRoutes);
            if (detail) BuildServeVisuals();
            bool visible = detail && serveContact != null;
            if (serveRacket != null)
            {
                serveRacket.gameObject.SetActive(detail); serveBall.gameObject.SetActive(detail);
                serveTouch.gameObject.SetActive(visible); serveDot.gameObject.SetActive(visible);
                foreach (var line in new[] { serveSwing, serveBrush, serveNormal, serveStripe }) line.enabled = visible;
                bool matched = serveContact != null;
                bool wide = detail && view.orthographicSize > 0.6f;
                if (serveStyledAsMatched != matched || serveStyledAsWide != wide)
                {
                    for (int i = 0; i < serveRacketRenderers.Length; i++)
                    {
                        Color color = matched ? serveRacketColors[i] : new Color(0.55f, 0.60f, 0.64f, 0.55f);
                        if (matched && wide) color.a = Mathf.Max(color.a, 0.9f);
                        serveRacketRenderers[i].sharedMaterial.color = color;
                    }
                    serveStyledAsMatched = matched; serveStyledAsWide = wide;
                }
            }
            racket.gameObject.SetActive(!detail && mode != 0 && flight != null);
            if (!detail) return false;
            ball.gameObject.SetActive(false);
            if (landingCloud != null) landingCloud.gameObject.SetActive(false);
            if (!visible)
            {
                V3 normal = BallSpin.Forward(ServeTarget.Velocity);
                serveBall.position = U(ServeTarget.Position);
                serveRacket.position = U(ServeTarget.Position - normal * (BallPhysics.Radius + 0.004));
                serveRacket.rotation = Quaternion.LookRotation(U(normal)) * Quaternion.AngleAxis(-70, Vector3.forward);
                return true;
            }
            if (servePlaying)
            {
                serveTime = Mathf.Min(0.02f, serveTime + Time.deltaTime * 0.025f);
                if (serveTime >= 0.02f) servePlaying = false;
            }
            var s = serveTime < 0 ? serveContact.Before : serveContact.After;
            s = BallPhysics.Advance(s, serveTime, settings);
            serveBall.position = U(s.Position);
            V3 center = serveContact.Before.Position - serveContact.Normal * (BallPhysics.Radius + 0.004);
            serveRacket.position = U(center + serveContact.RacketVelocity * serveTime);
            serveRacket.rotation = Quaternion.LookRotation(U(serveContact.Normal), Math.Abs(serveContact.Normal.Y) > 0.999 ? Vector3.forward : Vector3.up) * Quaternion.AngleAxis(-70, Vector3.forward);
            serveTouch.position = U(serveContact.Point); serveTouch.gameObject.SetActive(Math.Abs(serveTime) < 0.001);
            Arrow(serveSwing, serveRacket.position, U(serveContact.RacketVelocity).normalized * 0.20f);
            Arrow(serveBrush, U(serveContact.Point), U(serveContact.BrushVelocity).normalized * 0.12f);
            Arrow(serveNormal, serveRacket.position, U(serveContact.Normal) * 0.10f);
            double seconds = Math.Max(0, serveTime), phase = settings.SpinDecay > 0 ? (1 - Math.Exp(-settings.SpinDecay * seconds)) / settings.SpinDecay : seconds;
            Quaternion rotation = Quaternion.AngleAxis((float)(serveContact.After.Spin.Length * phase * 180 / Math.PI), U(serveContact.After.Spin).normalized);
            serveStripe.positionCount = 49;
            for (int i = 0; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                serveStripe.SetPosition(i, serveBall.position + rotation * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.0202f);
            }
            serveDot.position = serveBall.position + rotation * Vector3.up * 0.0205f;
            return true;
        }

        string ServePointDescription()
        {
            if (serveContact == null) return "";
            V3 arm = -serveContact.Normal, right = V3.Cross(new V3(0, 1, 0), BallSpin.Forward(ServeTarget.Velocity));
            string vertical = arm.Y > 0.15 ? L.T("上部") : arm.Y < -0.15 ? L.T("下部") : L.T("中部");
            double side = V3.Dot(arm, right);
            double depth = V3.Dot(arm, BallSpin.Forward(ServeTarget.Velocity));
            return L.T("球的") + (depth < -0.15 ? L.T("后") : depth > 0.15 ? L.T("前") : "") + vertical + (side > 0.15 ? L.T("，偏右") : side < -0.15 ? L.T("，偏左") : "");
        }

        void DrawServeContactPanel()
        {
            float x = CanvasRight + 18, width = ReceivePanelWidth - 36;
            DrawRect(new Rect(CanvasRight, 0, ReceivePanelWidth, UIHeight), new Color(0.075f, 0.105f, 0.13f));
            GUILayout.BeginArea(new Rect(x, SpinPanelHeight + 12, width, Mathf.Max(30, UIHeight - SpinPanelHeight - 82)));
            GUILayout.Label(L.T("发球触球示例"), heading);
            GUILayout.Label(serveContact != null ? L.T("一组可复现的局部动作，不是唯一解。") : L.T("目标球路保留；当前仅显示位置参照。"), small);
            serveScroll = GUILayout.BeginScrollView(serveScroll);
            if (serveContact == null)
            {
                GUILayout.Label(L.T("尚未找到匹配动作"), section);
                GUILayout.Label(serveContactError, small);
                GUILayout.Label(L.T("灰色球拍只标示离拍位置，不是已验证的拍型。挥拍箭头和慢放暂不提供。目标球速、旋转与球路保持不变。"), small);
            }
            else
            {
                GUILayout.Label(L.T("摩擦哪里"), section);
                GUILayout.Label(ServePointDescription(), heading);
                GUILayout.Label(L.T("沿出球方向看；橙点标出实际接触位置。"), small);
                GUILayout.Label(L.T("球拍怎么动"), section);
                V3 v = serveContact.RacketVelocity, right = V3.Cross(new V3(0, 1, 0), BallSpin.Forward(ServeTarget.Velocity));
                double pitch = Math.Asin(serveContact.Normal.Y) * 180 / Math.PI;
                double yaw = Math.Atan2(V3.Dot(serveContact.Normal, right), V3.Dot(serveContact.Normal, BallSpin.Forward(ServeTarget.Velocity))) * 180 / Math.PI;
                GUILayout.Label(L.F("拍面开合 {0:+0.0;-0.0;0.0}° · 侧偏 {1:+0.0;-0.0;0.0}°", pitch, yaw), small);
                GUILayout.Label(L.F("迎前 {0:+0.00;-0.00;0.00} m/s\n向上 {1:+0.00;-0.00;0.00} m/s\n向右 {2:+0.00;-0.00;0.00} m/s", V3.Dot(v, BallSpin.Forward(ServeTarget.Velocity)), v.Y, V3.Dot(v, right)), small);
                GUILayout.Label(L.T("负数分别表示后撤、向下、向左。\n黄箭头：触点速度；绿箭头：相对球面摩擦；蓝箭头：拍面朝向。"), small);
                if (RacketGeometry.MayTouchTable(serveContact.Before.Position, serveContact.Normal, serveContact.RacketVelocity * 2, -70))
                    GUILayout.Label(L.T("这段局部运动可能碰台；请提高离拍位置或移到台后。"), small);
                GUILayout.Label(L.T("示例的抛球假设"), section);
                double vy = serveContact.Before.Velocity.Y;
                GUILayout.Label(L.F("无旋转、无水平速度，触球前竖直{0} {1:0.00} m/s。", (vy < 0 ? L.T("下落") : L.T("上升")), Math.Abs(vy)), small);
                GUILayout.Label(L.F("自动在 ±{0:0} m/s 内选择可复现的抛球示例；目标球速、转速与胶皮参数未改变。未模拟完整抛球和人体动作。", ServeContact.MaxTossVerticalSpeed), small);
                GUILayout.Label(L.F("重算出球 {0:0.00} m/s\n重算旋转 {1:0.0} rps", serveContact.After.Velocity.Length, serveContact.After.Spin.Length / BallPhysics.Rps), section);
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
            if (GUI.Button(new Rect(x, UIHeight - 60, width, 42), L.T("返回球路与接发研究"), primaryButton)) SetCamera(cameraBeforeServe);
        }

        void DrawServeContactCanvas(float left, float width)
        {
            DrawRect(new Rect(panelWidth, ViewTop, CanvasRight - panelWidth, 58), new Color(0.055f, 0.08f, 0.10f));
            string legend = !showServeTrajectory ? L.T("轨迹已隐藏，可在下方勾选同时显示") :
                mode == 2 ? (flight != null ? L.T("橙线：发球 · 青线：接回球 · 圆点：碰撞位置") : L.T("橙线：发球（暂无有效接回球）")) : L.T("青线：出球轨迹 · 圆点：碰撞位置");
            if (showServeTrajectory && referenceFlight != null) legend += L.T(" · 紫线：参照");
            GUI.Label(new Rect(left, ViewTop + 8, width, 24), legend, small);
            GUI.Label(new Rect(left, ViewTop + 30, width, 24), serveContact != null ? L.T("已匹配当前球速与旋转 · 橙点：接触位置") : L.T("未匹配动作：灰色球拍仅示意离拍位置，未验证拍型与挥拍"), small);
            if (serveRacket != null && view.orthographicSize > 0.6f)
                WorldLabel(serveRacket.position, serveContact != null ? L.T("发球球拍") : L.T("位置参考"), 20);
            GUILayout.BeginArea(new Rect(left, UIHeight - FooterHeight + 10, width, FooterHeight - 16));
            GUILayout.BeginHorizontal();
            GUI.enabled = serveContact != null;
            if (GUILayout.Button(servePlaying ? L.T("暂停") : L.T("慢放触球"), primaryButton, GUILayout.Width(100)))
            { if (servePlaying) servePlaying = false; else { serveTime = -0.02f; servePlaying = true; } }
            if (GUILayout.Button(L.T("停在触球"), GUILayout.Width(88))) { serveTime = 0; servePlaying = false; }
            GUI.enabled = true;
            if (GUILayout.Button(new GUIContent(L.T("看完整球路"), L.T("同时显示球拍、当前轨迹、碰撞落点和已固定的参照，并适配视角。")), GUILayout.Width(100))) FitServeTrajectory();
            GUI.enabled = serveContact != null;
            float t = GUILayout.HorizontalSlider(serveTime, -0.02f, 0.02f);
            if (t != serveTime) { serveTime = t; servePlaying = false; }
            GUILayout.Label($"{serveTime * 1000:+0;-0;0} ms", value, GUILayout.Width(55));
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.BeginHorizontal();
            showServeTrajectory = GUILayout.Toggle(showServeTrajectory, L.T("同时显示轨迹与落点"));
            bool presetZoom = serveZoom && !cameraAdjusted;
            bool zoom = GUILayout.Toggle(presetZoom, L.T("放大球面接触区域"));
            if (zoom != presetZoom) { serveZoom = zoom; ResetCameraView(); }
            GUILayout.EndHorizontal();
            GUILayout.Label(L.T("轨迹为预计算结果；慢放仅触球前后各 20 ms，1/40 速度。"), small);
            GUILayout.EndArea();
        }

        [Serializable] sealed class ServeContactExport
        {
            public string assumption = "No initial spin or horizontal toss velocity. A vertical pre-contact velocity is selected within -5..5 m/s and the target velocity and spin are forward-verified. One example, not a unique human stroke.";
            public double[] incomingVelocity, normal, racketVelocity, brushVelocity, contactPoint;
            public double restitution, friction, tangent;
            public int responseId;
            public string error;
            public ServeContactExport(PhysicsLab lab)
            {
                responseId = lab.stroke.ResponseId; restitution = lab.stroke.Restitution; friction = lab.stroke.Friction; tangent = lab.stroke.Tangent; error = lab.serveContactError;
                if (lab.serveContact == null) return;
                double[] Values(V3 v) => new[] { v.X, v.Y, v.Z };
                var s = lab.serveContact;
                incomingVelocity = Values(s.Before.Velocity); normal = Values(s.Normal); racketVelocity = Values(s.RacketVelocity);
                brushVelocity = Values(s.BrushVelocity); contactPoint = Values(s.Point);
            }
        }
    }
}
