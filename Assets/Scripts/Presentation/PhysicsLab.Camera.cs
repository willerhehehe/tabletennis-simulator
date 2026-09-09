using UnityEngine;

namespace TableTennis
{
    public sealed partial class PhysicsLab
    {
        Vector3 cameraPivot;
        bool cameraAdjusted, cameraDraggingPan;
        int cameraDragButton = -1, cameraDragControl;
        bool CanOrbitCamera => !pickingReceivePoint && (cameraMode == 0 || cameraMode == 3 || cameraMode == 4);
        Rect CameraBar => new Rect(ViewArea.x + 12, ViewArea.yMax - 32, ViewArea.width - 24, 28);
        Rect TelemetryRect(float width) => new Rect(CanvasRight - Mathf.Min(300, width) - 12, ViewArea.yMax - 279, Mathf.Min(300, width), 233);

        bool CameraPointerAllowed(Vector2 point)
        {
            if (CompactLayout) return ViewArea.Contains(point) && !MobileSpinRect.Contains(point);
            if (!ViewArea.Contains(point) || CameraBar.Contains(point)) return false;
            float top = cameraMode == 4 ? 58 : cameraMode == 3 ? 94 : 60;
            if (point.y < ViewArea.y + top) return false;
            if (showTelemetry && cameraMode != 4 && flight != null && TelemetryRect(ViewArea.width - 44).Contains(point)) return false;
            if (cameraMode != 4 && !string.IsNullOrEmpty(error) && new Rect(panelWidth + 22, ViewTop + 55, ViewArea.width - 44, 150).Contains(point)) return false;
            return true;
        }

        bool CanStartCameraDrag(Vector2 point, int button) => CameraPointerAllowed(point) &&
            button >= 0 && button <= 2 && !(pickingReceivePoint && button == 0);

        void OrbitCamera(Vector2 delta)
        {
            if (!CanOrbitCamera || delta.sqrMagnitude == 0) return;
            Vector3 offset = view.transform.position - cameraPivot;
            float distance = Mathf.Max(0.12f, offset.magnitude);
            float yaw = Mathf.Atan2(offset.x, offset.z) - delta.x * 0.35f * Mathf.Deg2Rad;
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.y / distance, -1, 1)) * Mathf.Rad2Deg + delta.y * 0.35f, -85, 85) * Mathf.Deg2Rad;
            view.transform.position = cameraPivot + new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * distance;
            view.transform.LookAt(cameraPivot, Vector3.up);
            cameraAdjusted = true;
        }

        void PanCamera(Vector2 delta)
        {
            if (delta.sqrMagnitude == 0) return;
            float distance = Vector3.Distance(view.transform.position, cameraPivot);
            float height = view.orthographic ? 2 * view.orthographicSize : 2 * distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 offset = (-view.transform.right * delta.x + view.transform.up * delta.y) * (height / Mathf.Max(1, ViewArea.height));
            cameraPivot += offset; view.transform.position += offset; cameraAdjusted = true;
        }

        void ZoomCamera(float amount)
        {
            if (amount == 0 || float.IsNaN(amount)) return;
            float scale = Mathf.Exp(Mathf.Clamp(amount, -10, 10) * 0.08f);
            if (view.orthographic)
                view.orthographicSize = Mathf.Clamp(view.orthographicSize * scale, 0.025f, 8);
            else
            {
                Vector3 offset = view.transform.position - cameraPivot;
                view.transform.position = cameraPivot + offset.normalized * Mathf.Clamp(offset.magnitude * scale, 0.12f, 25);
            }
            cameraAdjusted = true;
        }

        void ResetCameraView() => SetCamera(cameraMode);

        void DrawCameraControls()
        {
            Rect bar = CameraBar;
            DrawRect(new Rect(bar.x - 4, bar.y - 2, bar.width + 8, bar.height + 4), new Color(0.055f, 0.08f, 0.10f, 0.94f));
            GUI.Label(new Rect(bar.x + 4, bar.y, Mathf.Max(0, bar.width - 150), bar.height),
                mode == 3 ? "拖圆点改位置 · Shift 改高度 · 空白处调视角" : pickingReceivePoint ? "左键选点 · 右键平移 · 滚轮缩放" : CanOrbitCamera ? "左键旋转 · 右键/Shift平移 · 滚轮缩放" : "拖动平移 · 滚轮缩放", small);
            if (GUI.Button(new Rect(bar.xMax - 140, bar.y, 32, bar.height), new GUIContent("−", "缩小视图"))) ZoomCamera(2);
            if (GUI.Button(new Rect(bar.xMax - 104, bar.y, 32, bar.height), new GUIContent("+", "放大视图"))) ZoomCamera(-2);
            if (GUI.Button(new Rect(bar.xMax - 68, bar.y, 68, bar.height), new GUIContent("复位", "恢复当前预设视角；画面中双击也可复位。"))) ResetCameraView();
            HandleCameraInput();
        }

        void HandleCameraInput()
        {
            if (CompactLayout && Input.touchCount > 1)
            { GUIUtility.hotControl = 0; cameraDragButton = -1; strokeDragging = -1; return; }
            Event e = Event.current;
            Vector2 mouse = InterfaceMousePosition;
            if (cameraDragButton < 0 && cameraDragControl != 0)
            {
                if (GUIUtility.hotControl == cameraDragControl) GUIUtility.hotControl = 0;
                cameraDragControl = 0;
            }
            int control = GUIUtility.GetControlID("PhysicsLabCamera".GetHashCode(), FocusType.Passive, ViewArea);
            if (e.type == EventType.ScrollWheel && GUIUtility.hotControl == 0 && CameraPointerAllowed(mouse))
            { ZoomCamera(e.delta.y); e.Use(); return; }
            if (e.type == EventType.MouseDown && GUIUtility.hotControl == 0 && CanStartCameraDrag(mouse, e.button))
            {
                if (e.button == 0 && e.clickCount == 2) { ResetCameraView(); e.Use(); return; }
                cameraDragButton = e.button; cameraDragControl = control;
                cameraDraggingPan = e.button != 0 || e.shift || !CanOrbitCamera || (CompactLayout && mobilePan);
                GUIUtility.hotControl = control; GUI.FocusControl(null); e.Use(); return;
            }
            if (cameraDragButton < 0) return;
            if (e.rawType == EventType.MouseUp || e.type == EventType.Ignore || GUIUtility.hotControl != cameraDragControl)
            {
                if (GUIUtility.hotControl == cameraDragControl) GUIUtility.hotControl = 0;
                cameraDragButton = -1; cameraDragControl = 0;
                if (e.type == EventType.MouseUp) e.Use();
                return;
            }
            if (e.type == EventType.MouseDrag)
            {
                if (cameraDraggingPan) PanCamera(InterfaceMouseDelta); else OrbitCamera(InterfaceMouseDelta);
                e.Use();
            }
        }
    }
}
