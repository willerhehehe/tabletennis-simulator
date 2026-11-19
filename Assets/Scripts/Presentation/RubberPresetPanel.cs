using UnityEngine;

namespace TableTennis
{
    public readonly struct RubberChoice
    {
        public readonly RubberCalibration Profile;
        public readonly bool Response;
        public RubberChoice(RubberCalibration profile, bool response) { Profile = profile; Response = response; }
    }

    // Selection is derived from the effective material, never from a separate browse cursor.
    public sealed class RubberPresetPanel
    {
        bool details;
        public static RubberCalibration Current(RubberCoefficients c, int responseId)
        {
            foreach (var p in RubberCalibrations.All)
                if (responseId != 0 ? p.Id == responseId : p.Matches(c.Restitution, c.Friction, c.Tangent)) return p;
            return null;
        }
        public RubberChoice? DrawSelector(RubberCoefficients c, int responseId, GUIStyle small)
        {
            var current = Current(c, responseId); int index = current == null ? -1 : current.Id - 1;
            int next = index;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹", GUILayout.Width(36))) next = index < 0 ? RubberCalibrations.All.Count - 1 : (index + RubberCalibrations.All.Count - 1) % RubberCalibrations.All.Count;
            GUILayout.Label(current == null ? L.T("自定义参数") : $"R{current.Id:00} · {L.T(current.Name)}", small);
            if (GUILayout.Button("›", GUILayout.Width(36))) next = (index + 1) % RubberCalibrations.All.Count;
            GUILayout.EndHorizontal();
            if (next != index)
            {
                var p = RubberCalibrations.All[next];
                return new RubberChoice(p, responseId != 0 && InvertedRubberResponses.Find(p.Id) != null);
            }
            if (current != null)
            {
                foreach (var part in current.Equipment.Split(new[] { " / " }, System.StringSplitOptions.None))
                    GUILayout.Label(part, small);
                if (InvertedRubberResponses.Find(current.Id) != null)
                {
                    int kind = GUILayout.Toolbar(responseId == 0 ? 0 : 1, new[] { L.T("常数拟合"), L.T("随触球速度") });
                    if ((kind == 1) != (responseId != 0)) return new RubberChoice(current, kind == 1);
                }
            }
            return null;
        }
        public void DrawDetails(RubberCoefficients c, int responseId, GUIStyle button, GUIStyle small)
        {
            if (GUILayout.Button((details ? "− " : "+ ") + L.T("标定依据与适用范围"), button)) details = !details;
            if (!details) return;
            var p = Current(c, responseId);
            if (p == null) { GUILayout.Label(L.T("当前是自定义系数，未对应已标定的整拍配置。"), small); return; }
            var response = responseId == 0 ? null : InvertedRubberResponses.Get(responseId);
            GUILayout.Label(L.F("独立测试 {0} 次\n平面速度误差 {1:0.00} m/s · 旋转误差 {2:0.00} rps", p.TestCount, response?.VelocityError ?? p.VelocityError, response?.SpinErrorRps ?? p.SpinErrorRps), small);
            GUILayout.Label(L.F("训练覆盖：法向速度 {0:0.0}–{1:0.0} m/s\n触点滑动速度 {2:0.0}–{3:0.0} m/s", p.NormalMin, p.NormalMax, p.SlipMin, p.SlipMax), small);
            GUILayout.Label(p.Limited ? L.T("长胶常数近似；未模拟逐根颗粒弯曲，μ 也不能由这批数据唯一确定。") : L.T("整拍组合的等效拟合，验证限于平面碰撞。"), small);
            GUILayout.Label(L.T("β=-1 表示无切向作用；β<0 保留同向滑动；β>0 允许切向回弹。"), small);
            if (GUILayout.Button(L.T("查看数据来源"), button)) Application.OpenURL(RubberCalibrations.Source);
        }
    }
}
