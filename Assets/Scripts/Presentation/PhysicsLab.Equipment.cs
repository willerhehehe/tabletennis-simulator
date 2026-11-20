using UnityEngine;

namespace TableTennis
{
    public sealed partial class PhysicsLab
    {
        int equipmentRole; // 0 serving racket, 1 receiver/single-shot racket.
        bool receiveEquipmentChosen;

        RubberCoefficients EquipmentCoefficients(bool serving) => serving
            ? new RubberCoefficients(stroke.Restitution, stroke.Friction, stroke.Tangent)
            : new RubberCoefficients(rubberBounce, rubberFriction, rubberTangent);
        int EquipmentResponse(bool serving) => serving ? stroke.ResponseId : rubberResponseId;

        void SetEquipment(bool serving, RubberCoefficients c, int responseId)
        {
            if (serving)
            {
                stroke.Restitution = c.Restitution; stroke.Friction = c.Friction; stroke.Tangent = c.Tangent; stroke.ResponseId = responseId;
            }
            else
            {
                rubberBounce = (float)c.Restitution; rubberFriction = (float)c.Friction; rubberTangent = (float)c.Tangent;
                rubberResponseId = responseId; receiveEquipmentChosen = true;
            }
            parametersChanged = true;
        }

        void ApplyEquipmentChoice(bool serving, RubberChoice choice)
        {
            SetEquipment(serving, new RubberCoefficients(choice.Profile.Restitution, choice.Profile.Friction, choice.Profile.Tangent),
                choice.Response ? choice.Profile.Id : 0);
            editField = ""; if (Event.current != null) GUI.FocusControl(null);
        }

        void DrawEquipmentSettings()
        {
            bool enabled = GUI.enabled; GUI.enabled = enabled && !IsStudying;
            int role = GUILayout.Toolbar(equipmentRole, new[] { L.T("发球拍"), L.T("接球 / 单板拍") });
            if (role != equipmentRole) { equipmentRole = role; editField = ""; GUI.FocusControl(null); }
            bool serving = equipmentRole == 0;
            GUILayout.Label(serving ? L.T("用于挥拍发球和发球触球示例。") : L.T("用于单板击球与接发评估。"), small);
            var c = EquipmentCoefficients(serving); int responseId = EquipmentResponse(serving);
            var choice = rubberPresets.DrawSelector(c, responseId, small);
            if (choice.HasValue)
            {
                ApplyEquipmentChoice(serving, choice.Value);
                c = EquipmentCoefficients(serving); responseId = EquipmentResponse(serving);
            }
            GUILayout.Label(L.T("选择器材或拟合方式即生效。"), small);
            if (responseId == 0)
            {
                GUILayout.Label(L.T("生效系数 · 可手动修改"), small);
                float e = (float)c.Restitution, mu = (float)c.Friction, beta = (float)c.Tangent;
                bool changed = Slider(L.T("恢复系数 e"), ref e, 0, 1);
                changed |= Slider(L.T("摩擦系数 μ"), ref mu, 0, 1.5f);
                changed |= Slider(L.T("切向恢复 β"), ref beta, -1, 1);
                if (changed) SetEquipment(serving, new RubberCoefficients(e, mu, beta), 0);
            }
            else
            {
                bool active = serving ? mode == 3 || cameraMode == 4 : mode == 1 || mode == 2 && cameraMode != 4;
                BallState? state = active ? serving ? serveContact?.Before : (BallState?)incoming : null;
                V3 normal = serving ? serveContact?.Normal ?? stroke.Normal : RacketNormal;
                V3 velocity = serving ? serveContact?.RacketVelocity ?? stroke.Velocity : RacketVelocity;
                DrawResponseDetails(responseId, state, normal, velocity);
                if (!active) GUILayout.Label(L.T("当前模式没有使用这侧球拍，触球时再计算实际系数。"), small);
                if (GUILayout.Button(L.T("转为手动固定参数"), quietButton))
                {
                    var fixedValues = state.HasValue ? InvertedRubberResponses.Get(responseId).At(state.Value, normal, velocity, out _) : c;
                    SetEquipment(serving, fixedValues, 0);
                }
            }
            rubberPresets.DrawDetails(EquipmentCoefficients(serving), EquipmentResponse(serving), quietButton, small);
            if (GUILayout.Button(L.T("恢复经验初值"), quietButton)) SetEquipment(serving, new RubberCoefficients(.8, .8, .15), 0);
            if (IsStudying) GUILayout.Label(L.T("评估进行中，球拍设置暂时锁定。"), small);
            GUI.enabled = enabled;
        }
    }
}
