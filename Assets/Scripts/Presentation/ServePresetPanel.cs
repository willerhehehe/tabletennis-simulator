using UnityEngine;

namespace TableTennis
{
    // Owns preset browsing only; the experiment applies the selected recipe.
    public sealed class ServePresetPanel
    {
        int type = -1, length;
        bool showNotes;
        public ServePreset Selected { get; private set; }
        public void Select(ServePreset preset)
        { Selected = preset; type = (int)preset.Type; length = preset.TargetLength == ServeLength.Long ? 1 : 0; }

        public ServePreset Draw(GUIStyle heading, GUIStyle small, GUIStyle button, ServeStrokeResult current)
        {
            GUILayout.Label(L.T("发球预设 · 右手"), heading);
            int chosenType = GUILayout.SelectionGrid(type, L.All(ServePresetLibrary.TypeNames), 2);
            int chosenLength = GUILayout.Toolbar(length, new[] { L.T("短球"), L.T("长球") });
            if (chosenType < 0) length = chosenLength;
            ServePreset requested = null;
            if (chosenType != type || chosenLength != length)
            {
                if (chosenType >= 0) requested = ServePresetLibrary.Get(chosenType, chosenLength);
            }
            if (Selected == null) GUILayout.Label(L.T("选一种旋转与长短，即可载入拍型和动作。"), small);
            else
            {
                GUILayout.Label(L.T("参考：") + L.T(Selected.Name), small);
                ServeLength actual = ServePresetLibrary.Classify(current?.Flight);
                GUILayout.Label(L.T("实际路线：") + (actual == ServeLength.Short ? L.T("短球（对方半台可再跳）") : actual == ServeLength.Long ? L.T("长球（对方端线出）") : L.T("未形成预设长短路线")), small);
                bool match = Selected.Matches(current);
                var before = GUI.contentColor; GUI.contentColor = match ? new Color(0.45f, 0.96f, 0.77f) : new Color(1, 0.75f, 0.44f);
                GUILayout.Label(match ? L.T("当前旋转与长短符合预设") : L.T("当前结果已偏离预设，可重新匹配或继续调整"), small);
                GUI.contentColor = before;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(L.T("重新匹配"), button)) requested = Selected;
                if (GUILayout.Button(showNotes ? L.T("收起要点") : L.T("动作要点"), button)) showNotes = !showNotes;
                GUILayout.EndHorizontal();
                if (showNotes)
                {
                    GUILayout.Label(L.T(Selected.ActionHint), small);
                    GUILayout.Label(L.T(Selected.Tactic), small);
                    if (GUILayout.Button(L.T("查看教学来源"), button)) Application.OpenURL(Selected.SourceUrl);
                }
            }
            GUILayout.Label(L.T("载入时按当前胶皮匹配拍型与挥拍。\n短球：无人接时能在对方半台再跳一次。"), small);
            return requested;
        }
    }
}
