using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TableTennis
{
    public static class WebPlatform
    {
        public static string ExportDirectory => Application.platform == RuntimePlatform.WebGLPlayer
            ? Path.Combine(Application.persistentDataPath, "Exports") : Path.Combine(Application.dataPath, "../Artifacts");
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void TT_Download(byte[] bytes, int length, string filename);
        [DllImport("__Internal")] static extern float TT_DeviceScale();
        [DllImport("__Internal")] static extern void TT_Number(string target, string label, string value, float min, float max);
#endif
        public static float DeviceScale
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return Mathf.Clamp(TT_DeviceScale(), 1, 3);
#else
                return 1;
#endif
            }
        }
        public static void EditNumber(string target, string label, float value, float min, float max)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
            TT_Number(target, label, value.ToString("G7", System.Globalization.CultureInfo.InvariantCulture), min, max);
#endif
        }
        public static void DeliverExport(string stem)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                    foreach (var path in Directory.GetFiles(Path.GetDirectoryName(stem), Path.GetFileName(stem) + "*"))
                    {
                        using (var output = zip.CreateEntry(Path.GetFileName(path)).Open())
                        { var bytes = File.ReadAllBytes(path); output.Write(bytes, 0, bytes.Length); }
                    }
                var data = stream.ToArray(); TT_Download(data, data.Length, Path.GetFileName(stem) + ".zip");
            }
#endif
        }
    }
}
