using System;
using UnityEngine;

namespace TableTennis
{
    // Creates and owns scene objects/materials; no experiment or UI state.
    public sealed class LabScene : IDisposable
    {
        public Transform Root { get; }
        public Material LineMaterial { get; }
        public LabScene()
        {
            Root = new GameObject("试验球台").transform;
            LineMaterial = new Material(Shader.Find("Sprites/Default"));
            RenderSettings.ambientLight = new Color(0.55f, 0.60f, 0.67f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            var light = new GameObject("柔光").AddComponent<Light>();
            light.transform.SetParent(Root); light.type = LightType.Directional; light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            var floor = Cube("地面", new Vector3(0, -0.04f, 0), new Vector3(8, 0.08f, 9), new Color(0.065f, 0.085f, 0.11f));
            floor.GetComponent<Renderer>().sharedMaterial.shader = Shader.Find("Unlit/Color");
            Cube("球台", new Vector3(0, 0.72f, 0), new Vector3(1.525f, 0.08f, 2.74f), new Color(0.075f, 0.24f, 0.27f));
            foreach (float x in new[] { -0.58f, 0.58f }) foreach (float z in new[] { -1.06f, 1.06f })
                Cube("台脚", new Vector3(x, 0.35f, z), new Vector3(0.055f, 0.7f, 0.055f), new Color(0.20f, 0.26f, 0.30f));
            Color white = new Color(0.74f, 0.85f, 0.85f);
            foreach (float x in new[] { -0.7525f, 0.7525f })
                Cube("边线", new Vector3(x, 0.761f, 0), new Vector3(0.02f, 0.002f, 2.74f), white);
            foreach (float z in new[] { -1.36f, 1.36f })
                Cube("端线", new Vector3(0, 0.761f, z), new Vector3(1.525f, 0.002f, 0.02f), white);
            Cube("中线", new Vector3(0, 0.761f, 0), new Vector3(0.003f, 0.002f, 2.74f), white);
            for (int i = -20; i <= 20; i++)
                Line("网线", new[] { new Vector3(i * 0.045f, 0.76f, 0), new Vector3(i * 0.045f, 0.9125f, 0) }, white * 0.6f, 0.002f);
            for (int i = 0; i <= 5; i++)
                Line("横网线", new[] { new Vector3(-0.915f, 0.76f + i * 0.0305f, 0), new Vector3(0.915f, 0.76f + i * 0.0305f, 0) }, white * 0.6f, 0.002f);
            Line("网带", new[] { new Vector3(-0.915f, 0.9125f, 0), new Vector3(0.915f, 0.9125f, 0) }, white, 0.008f);
        }

        public Transform CreateRacket(string name)
        {
            var result = new GameObject(name).transform; result.SetParent(Root);
            var blade = Primitive("底板", PrimitiveType.Cylinder, Vector3.zero, new Vector3((float)RacketGeometry.FaceHalfWidth * 2, 0.002f, (float)RacketGeometry.FaceHalfHeight * 2), new Color(0.76f, 0.61f, 0.40f));
            blade.transform.SetParent(result, false); blade.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var face = Primitive("胶皮", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.15f, 0.001f, 0.17f), new Color(0.035f, 0.04f, 0.045f));
            face.transform.SetParent(result, false); face.transform.localRotation = Quaternion.Euler(90, 0, 0);
            face.transform.localPosition = new Vector3(0, 0, 0.003f);
            var back = Primitive("背面", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.15f, 0.001f, 0.17f), new Color(0.8f, 0.055f, 0.07f));
            back.transform.SetParent(result, false); back.transform.localRotation = Quaternion.Euler(90, 0, 0);
            back.transform.localPosition = new Vector3(0, 0, -0.003f);
            var handle = Cube("拍柄", Vector3.zero, new Vector3((float)RacketGeometry.HandleHalfWidth * 2, (float)RacketGeometry.HandleHalfLength * 2, 0.018f), new Color(0.5f, 0.33f, 0.19f));
            handle.transform.SetParent(result, false); handle.transform.localPosition = new Vector3(0, -(float)RacketGeometry.HandleOffset, 0);
            return result;
        }

        GameObject Primitive(string name, PrimitiveType shape, Vector3 pos, Vector3 scale, Color color)
        {
            var obj = GameObject.CreatePrimitive(shape); obj.name = name; obj.transform.SetParent(Root);
            obj.transform.position = pos; obj.transform.localScale = scale;
            UnityEngine.Object.Destroy(obj.GetComponent<Collider>());
            var mat = new Material(Shader.Find("Standard")); mat.color = color;
            mat.SetFloat("_Glossiness", 0.15f); obj.GetComponent<Renderer>().sharedMaterial = mat;
            return obj;
        }
        public GameObject Cube(string name, Vector3 p, Vector3 s, Color c) => Primitive(name, PrimitiveType.Cube, p, s, c);
        public GameObject Sphere(string name, Vector3 p, float s, Color c) => Primitive(name, PrimitiveType.Sphere, p, Vector3.one * s, c);
        public LineRenderer Line(string name, Vector3[] points, Color color, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(Root);
            line.sharedMaterial = LineMaterial; line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width; line.positionCount = points.Length; line.SetPositions(points);
            line.numCapVertices = 4; return line;
        }
        public void Dispose()
        {
            if (Root != null)
            {
                foreach (var renderer in Root.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.sharedMaterial != LineMaterial) UnityEngine.Object.Destroy(renderer.sharedMaterial);
                UnityEngine.Object.Destroy(Root.gameObject);
            }
            if (LineMaterial != null) UnityEngine.Object.Destroy(LineMaterial);
        }
    }
}
