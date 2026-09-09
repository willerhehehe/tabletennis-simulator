using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class WindowsBuild
{
    public static void Build()
    {
        const BuildTarget target = BuildTarget.StandaloneWindows64;
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            throw new InvalidOperationException("请先安装 Windows Build Support (Mono)。");
        string directory = Environment.GetEnvironmentVariable("TABLETENNIS_BUILD_OUTPUT");
        if (string.IsNullOrEmpty(directory)) directory = "Builds/Windows11/TableTennisLab";
        directory = Path.GetFullPath(directory); Directory.CreateDirectory(directory);
        PlayerSettings.productName = "Table Tennis Physics Lab";
        PlayerSettings.companyName = "Willer";
        PlayerSettings.bundleVersion = Environment.GetEnvironmentVariable("TABLETENNIS_BUILD_VERSION") ?? "0.1.0";
        PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
        PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11 });
        // Scene objects create their materials at runtime, so include these shaders explicitly.
        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
        foreach (string name in new[] { "Standard", "Sprites/Default", "Unlit/Color" })
        {
            var shader = Shader.Find(name);
            if (shader == null) throw new InvalidOperationException("缺少着色器：" + name);
            bool found = false;
            for (int i = 0; i < shaders.arraySize; i++) found |= shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader;
            if (!found) { shaders.InsertArrayElementAtIndex(shaders.arraySize); shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = shader; }
        }
        graphics.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/PhysicsLab.unity" }, target = target,
            locationPathName = Path.Combine(directory, "TableTennisLab.exe"), options = BuildOptions.CompressWithLz4HC
        });
        File.WriteAllText(Path.Combine(directory, "build-info.txt"),
            $"Version {PlayerSettings.bundleVersion}\nUnity {Application.unityVersion}\nWindows x64 / Mono / Direct3D 11\nBuild: {report.summary.result}\nBytes: {report.summary.totalSize}\nErrors: {report.summary.totalErrors}\n");
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows 构建失败，请查看构建日志。");
    }
}
