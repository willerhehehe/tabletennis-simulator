using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class WebBuild
{
    public static void Build()
    {
        const BuildTarget target = BuildTarget.WebGL;
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, target)) throw new InvalidOperationException("请安装 Web Build Support。");
        string output = Path.GetFullPath(Environment.GetEnvironmentVariable("TABLETENNIS_BUILD_OUTPUT") ?? "Builds/Web");
        PlayerSettings.productName = "Table Tennis Physics Lab"; PlayerSettings.companyName = "Willer";
        PlayerSettings.bundleVersion = Environment.GetEnvironmentVariable("TABLETENNIS_BUILD_VERSION") ?? "0.2.0";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.WebGL), ScriptingImplementation.IL2CPP);
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.template = "PROJECT:TableTennis";
        PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
        PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.OpenGLES3 });
        WindowsBuild.IncludeRuntimeShaders();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/PhysicsLab.unity" }, target = target,
            locationPathName = output, options = BuildOptions.CompressWithLz4HC });
        File.WriteAllText(Path.Combine(output, "build-info.txt"), $"Version {PlayerSettings.bundleVersion}\nUnity {Application.unityVersion}\nWeb / IL2CPP / WebGL 2\nBuild: {report.summary.result}\nErrors: {report.summary.totalErrors}\nBytes: {report.summary.totalSize}\n");
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Web 构建失败，请查看日志。");
    }
}
