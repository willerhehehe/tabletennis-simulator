using System;
using UnityEngine;

namespace TableTennis
{
    public sealed partial class PhysicsLab
    {
        const float SpinPanelHeight = 244;
        SpinPreview spinPreview;
        int spinPreviewSource = 1; // Follow the currently displayed ball by default.
        bool spinSettingsOpen;
        int settingsPage;
        Vector2 spinSettingsScroll;
        Rect SpinPanelRect => new Rect(CanvasRight, 0, ReceivePanelWidth, SpinPanelHeight);

        BallState? PreviewBallState()
        {
            if (mode == 3)
                return spinPreviewSource == 0 ? serveContact?.After : strokeTimeline.Count > 0 ? CurrentStrokeState() : (BallState?)null;
            if (cameraMode == 4)
            {
                if (serveContact == null) return ServeTarget; // Explicitly labelled target, not a verified action.
                if (spinPreviewSource == 0) return serveContact.After;
                return BallPhysics.Advance(serveTime < 0 ? serveContact.Before : serveContact.After, serveTime, settings);
            }
            if (flight == null || flight.Samples.Count == 0) return null;
            return spinPreviewSource == 0 ? launch : StateAt(time);
        }

        void DrawPersistentSpinPanel()
        {
            bool enabled = GUI.enabled; GUI.enabled = true;
            if (spinPreview == null) spinPreview = new SpinPreview();
            float x = CanvasRight + 16, width = ReceivePanelWidth - 32;
            DrawRect(SpinPanelRect, new Color(0.075f, 0.105f, 0.13f));
            GUI.Label(new Rect(x, 10, width - 78, 30), cameraMode == 4 && serveContact == null ? L.T("球的旋转 · 目标") : L.T("球的旋转"), heading);
            bool opened = GUI.Toggle(new Rect(x + width - 70, 12, 70, 28), spinSettingsOpen, L.T("设置"), GUI.skin.button);
            if (opened && !spinSettingsOpen) { equipmentRole = mode == 3 || mode == 0 || cameraMode == 4 ? 0 : 1; settingsPage = 0; spinSettingsScroll = Vector2.zero; }
            spinSettingsOpen = opened;
            int source = GUI.Toolbar(new Rect(x, 46, width, 30), spinPreviewSource, new[] { L.T("出球旋转"), L.T("随回放") });
            if (source != spinPreviewSource) { spinPreviewSource = source; spinPreview.Reset(); }
            spinPreview.SetViewRotation(view.transform.rotation);
            spinPreview.Draw(new Rect(x, 80, width, 158), PreviewBallState(), small, true);
            DrawRect(new Rect(x, SpinPanelHeight - 1, width, 1), new Color(0.20f, 0.27f, 0.29f));
            GUI.enabled = enabled;
        }

        bool DrawSpinSettingsPanel()
        {
            if (!spinSettingsOpen) return false;
            if (spinPreview == null) spinPreview = new SpinPreview();
            GUILayout.BeginArea(new Rect(CanvasRight + 16, SpinPanelHeight + 12, ReceivePanelWidth - 32, Mathf.Max(30, UIHeight - SpinPanelHeight - 24)));
            LanguageSetting.DrawSelector();
            int page = GUILayout.Toolbar(settingsPage, new[] { L.T("球拍与胶皮"), L.T("旋转示意") });
            if (page != settingsPage) { settingsPage = page; spinSettingsScroll = Vector2.zero; editField = ""; GUI.FocusControl(null); }
            spinSettingsScroll = GUILayout.BeginScrollView(spinSettingsScroll);
            if (settingsPage == 0) DrawEquipmentSettings();
            else
            {
                GUILayout.Label(L.T("旋转示意设置"), section);
                spinPreview.DrawControls(quietButton, small, true);
                GUILayout.Label(L.T("只影响右上角示意球；实际旋转和物理计算保持原值。"), small);
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button(L.T("返回实验面板"), quietButton)) spinSettingsOpen = false;
            GUILayout.EndArea();
            return true;
        }
    }
}
