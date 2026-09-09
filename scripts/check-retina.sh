#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
"${UNITY_CLI:-$HOME/.unity/bin/unity}" command eval --code '
var lab=UnityEngine.Object.FindFirstObjectByType<TableTennis.PhysicsLab>();
if (!UnityEditor.EditorApplication.isPlaying || lab==null) throw new System.Exception("Run PhysicsLab first.");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
var views=UnityEngine.Resources.FindObjectsOfTypeAll(type);
if (views.Length==0) throw new System.Exception("Open the Game view first.");
var game=(UnityEditor.EditorWindow)views[0];
if ((bool)type.GetProperty("lowResolutionForAspectRatios",flags).GetValue(game)) throw new System.Exception("FAIL: low-resolution Game view is enabled.");
var render=(UnityEngine.Vector2)type.GetProperty("targetRenderSize",flags).GetValue(game);
float density=(float)type.GetProperty("backingScale",flags).GetValue(game);
float width=(float)lab.GetType().GetField("guiWidth",flags).GetValue(lab);
float height=(float)lab.GetType().GetField("guiHeight",flags).GetValue(lab);
if (UnityEngine.Mathf.Abs(width-render.x/density)>1 || UnityEngine.Mathf.Abs(height-render.y/density)>1)
    throw new System.Exception("FAIL: UI layout is not expressed in logical points at native resolution.");
var camera=(UnityEngine.Camera)lab.GetType().GetField("view",flags).GetValue(lab);
var area=(UnityEngine.Rect)lab.GetType().GetProperty("ViewArea",flags).GetValue(lab);
if (UnityEngine.Mathf.Abs(camera.rect.x-area.x/width)>0.001f || UnityEngine.Mathf.Abs(camera.rect.width-area.width/width)>0.001f)
    throw new System.Exception("FAIL: camera viewport is misaligned with the scaled UI.");
string report="PASS: native rendering with low-resolution mode disabled.\nPASS: logical UI size is preserved at display density "+density+".\nPASS: camera viewport matches the scaled UI.\nRender: "+render+"; logical UI: "+width+"x"+height;
System.IO.File.WriteAllText("Artifacts/retina-checks.txt",report);
return report;
' --json
