using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CatWalk.Rendering.Editor
{
    /// <summary>Render the real URP shader and check its visual inputs, determinism and infinite distance.</summary>
    public static class SpaceSkyValidation
    {
        [Serializable] sealed class Report { public bool success; public string backend; public string[] checks; }
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/SpaceSky"));

        [MenuItem("Tools/CatWalk/Sky/Validate and Capture Sky")]
        public static void ValidateMenu() => Debug.Log(Validate().Length + " procedural sky checks passed");

        [MenuItem("Tools/CatWalk/Sky/Capture Demo Frames")]
        public static void CaptureDemo()
        {
            Directory.CreateDirectory(Path.Combine(Output, "Frames"));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpaceSkyAuthoring.PrefabPath).GetComponent<SpaceSky>();
            using (var fixture = new SkyRender(prefab.Template, prefab.Settings, 768, 432))
                for (int frame = 0; frame < 96; frame++)
                {
                    float phase = frame / 96f;
                    fixture.Draw(SpaceSkyState.Demo(phase), phase * 120, "Frames/" + frame.ToString("D3"));
                }
        }

        [MenuItem("Tools/CatWalk/Sky/Capture Sphere Orbit")]
        public static void CaptureOrbit()
        {
            Directory.CreateDirectory(Path.Combine(Output, "Orbit"));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpaceSkyAuthoring.PrefabPath).GetComponent<SpaceSky>();
            using (var fixture = new SkyRender(prefab.Template, prefab.Settings, 960, 540))
                for (int frame = 0; frame < 120; frame++)
                {
                    fixture.Camera.transform.rotation = Quaternion.Euler(-12, -18 + frame * 3, 0);
                    fixture.Draw(SpaceSkyState.Cloud, 12, "Orbit/" + frame.ToString("D3"));
                }
        }

        public static string[] Validate()
        {
            Directory.CreateDirectory(Output);
            var checks = new List<string>();
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Space sky: " + message); checks.Add(message); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpaceSkyAuthoring.PrefabPath);
            var skies = prefab.GetComponentsInChildren<SpaceSky>(true);
            Check(skies.Length == 1 && skies[0].Settings && skies[0].Template, "Standalone sky prefab contains one module with explicit material and settings references");
            Check(!AssetDatabase.GetDependencies(SpaceSkyAuthoring.PrefabPath, true).Any(p => p.StartsWith("Assets/SpaceCourier/")),
                "Portable sky has no dependencies on the previous branch's courier prototype");
            Check(skies[0].Dome && skies[0].Dome.GetComponent<MeshFilter>().sharedMesh,
                "The sky is rendered by an authored sphere MeshRenderer");
            var mesh = skies[0].Dome.GetComponent<MeshFilter>().sharedMesh;
            Check(mesh.vertexCount == 642 && mesh.triangles.Length == 3840 && mesh.vertices.All(v => Mathf.Abs(v.magnitude - 1) < .0001f) && mesh.uv.Length == 0,
                "The sky mesh is a closed subdivided icosphere without texture UVs");
            var source = skies[0].Template;
            Check(source.shader.name == "CatWalk/Space Sky" && source.shader.isSupported, "Procedural sky is supported by the active graphics backend");
            Check(Enumerable.Range(0, ShaderUtil.GetPropertyCount(source.shader)).All(i => ShaderUtil.GetPropertyType(source.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv),
                "Sky shader has zero texture and cubemap inputs");
            string before = EditorJsonUtility.ToJson(source);
            using (var fixture = new SkyRender(source, skies[0].Settings))
            {
                var empty = fixture.Draw(new SpaceSkyState(0, 0, 0, 0, .3f), 0, "00-empty");
                var sparse = fixture.Draw(new SpaceSkyState(.15f, 0, 0, 0, .3f), 0, "01-sparse");
                var medium = fixture.Draw(new SpaceSkyState(.5f, 0, 0, 0, .3f), 0);
                var dense = fixture.Draw(new SpaceSkyState(1, 0, 0, 0, .3f), 0, "02-dense");
                Check(Bright(dense) > Bright(medium) && Bright(medium) > Bright(sparse) && Bright(sparse) > Bright(empty),
                    "Rendered star counts increase monotonically at 0, 15, 50 and 100 percent density");
                Check(Bright(empty) == 0, "Zero density and zero galaxy formation extinguish every star");
                var galaxy = fixture.Draw(new SpaceSkyState(.15f, 1, 0, 0, .3f), 0, "03-galaxy");
                Check(Bright(galaxy) > Bright(sparse) * 1.4f, "A galaxy forms a concentrated field of resolved stars independently of background density");
                var cloud = fixture.Draw(new SpaceSkyState(0, 0, 1, 0, .3f), 0, "04-nebula-only");
                Check(Mean(cloud) > Mean(empty) + .012f, "Nebula formation adds a visible cloud without requiring stars or galaxies");
                var state = SpaceSkyState.Galaxy;
                var hero = fixture.Draw(state, 12, "05-galaxies");
                var repeat = fixture.Draw(state, 12);
                Check(Difference(hero, repeat) == 0, "Identical seed, state and clock produce identical rendered pixels");
                fixture.Camera.transform.position = new Vector3(12000, -5000, 8000);
                var moved = fixture.Draw(state, 12);
                Check(Difference(hero, moved) < .0001f, "A 15km camera translation leaves the sky unchanged (including floating-origin rebases)");
                fixture.Camera.transform.position = Vector3.zero;
                var later = fixture.Draw(state, 23, "06-twinkle");
                Check(Difference(hero, later) > .0005f, "Independent stars visibly fade and relight as the visual clock advances");
                fixture.Settings.Seed += 17;
                var reseeded = fixture.Draw(state, 12);
                Check(Difference(hero, reseeded) > .002f, "Changing the seed produces a different deterministic star distribution");
                fixture.Settings.Seed -= 17;
                fixture.Draw(SpaceSkyState.Cloud, 28, "07-nebula");
                fixture.Camera.transform.rotation = Quaternion.Euler(10, 126, 0);
                var second = fixture.Draw(SpaceSkyState.Galaxy, 12, "08-second-galaxy");
                fixture.Camera.transform.rotation = Quaternion.Euler(-38, -132, 0);
                var third = fixture.Draw(SpaceSkyState.Galaxy, 12, "09-third-galaxy");
                Check(Bright(second) > 100 && Bright(third) > 100, "Both other hemispheres contain visible independently shaped galaxies");
                int view = 0;
                foreach (var angle in new[] { new Vector3(0, 45, 0), new Vector3(0, 135, 0), new Vector3(0, -45, 0),
                    new Vector3(0, -135, 0), new Vector3(-90, 0, 0), new Vector3(90, 180, 0) })
                {
                    fixture.Camera.transform.rotation = Quaternion.Euler(angle);
                    var pixels = fixture.Draw(SpaceSkyState.Cloud, 12, "seam-check-" + view++);
                    Check(Mean(pixels) > .01f && !pixels.Any(p => p.r > 240 && p.b > 240 && p.g < 20), "Sphere covers the whole view at former cube edges and exact poles: " + angle);
                    float squarePixels = SquarePixelFraction(pixels, 1280, 720, Mathf.Max(1, Mathf.FloorToInt(720f / fixture.Settings.PixelResolution + .5f)));
                    Check(squarePixels > .98f, "Pixel grid stays square without directional stretching at " + angle + " (" + squarePixels.ToString("P1") + ")");
                }
                fixture.Camera.transform.rotation = Quaternion.Euler(-8, -13, 0);
                fixture.Settings.PixelResolution = 144;
                var coarse = fixture.Draw(state, 12, "10-extra-coarse");
                Check(Difference(hero, coarse) > .002f, "Screen pixel resolution changes the pixel-art scale without changing the sphere");
                fixture.Camera.transform.rotation = Quaternion.Euler(-8, -13, 0);
                fixture.Camera.transform.position = new Vector3(120000, -50000, 80000);
                var distant = fixture.Draw(state, 12, "11-distant-camera");
                Check(Difference(coarse, distant) < .0001f, "The real sphere stays visible and stationary after a 150km camera translation");
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var blockerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                blockerMaterial.SetColor("_BaseColor", Color.red);
                blocker.GetComponent<MeshRenderer>().sharedMaterial = blockerMaterial;
                blocker.layer = 31;
                if (fixture.PreviewScene.IsValid()) SceneManager.MoveGameObjectToScene(blocker, fixture.PreviewScene);
                try
                {
                    blocker.transform.position = fixture.Camera.transform.position + fixture.Camera.transform.forward * 3;
                    var occluded = fixture.Draw(state, 12, "12-depth-occlusion");
                    var centre = occluded[360 * 1280 + 640];
                    Check(centre.r > 200 && centre.g < 30 && centre.b < 30,
                        "Foreground geometry occludes the infinite sphere correctly");
                }
                finally { Object.DestroyImmediate(blocker); Object.DestroyImmediate(blockerMaterial); }
                Check(!ShaderUtil.ShaderHasError(source.shader), "The actual sky pass compiles and renders without shader errors");
            }
            Check(before == EditorJsonUtility.ToJson(source), "Preview captures leave the authored sky material unchanged");
            File.WriteAllText(Path.Combine(Output, "validation.json"), JsonUtility.ToJson(new Report {
                success = true, backend = SystemInfo.graphicsDeviceType.ToString(), checks = checks.ToArray()
            }, true));
            return checks.ToArray();
        }

        [MenuItem("Tools/CatWalk/Sky/Validate Live Sky")]
        public static void ValidateLiveMenu() => Debug.Log(ValidateLive().Length + " live sky checks passed");

        public static string[] ValidateLive()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Requires Play mode; checks use a temporary sky without touching gameplay.");
            var live = Object.FindFirstObjectByType<SpaceSky>();
            var checks = new List<string>();
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Live sky: " + message); checks.Add(message); }
            Check(live && live.Dome && live.ActiveMaterial && live.ActiveMaterial != live.Template && live.Dome.sharedMaterial == live.ActiveMaterial && !RenderSettings.skybox,
                "Play binds an independent material to the sphere and disables the old cube skybox");
            // Complete synchronously inside one editor update, restoring the live sky before another frame.
            var originalSky = RenderSettings.skybox;
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SpaceSkyAuthoring.PrefabPath));
            root.hideFlags = HideFlags.HideAndDontSave;
            var sky = root.GetComponent<SpaceSky>();
            string templateBefore = EditorJsonUtility.ToJson(sky.Template);
            try
            {
                sky.AnimationSpeed = 1;
                sky.SetVisualState(SpaceSkyState.Quiet); sky.Advance(60);
                float start = sky.CurrentState.StarDensity;
                sky.SetVisualState(SpaceSkyState.Starfield);
                Check(!sky.DemoCycle && sky.CurrentState.StarDensity == start, "External visual input stops the demo without jumping to the target");
                sky.Advance(1f / 60);
                Check(sky.CurrentState.StarDensity > start && sky.CurrentState.StarDensity < start + .02f, "A density change begins gradually on the next frame");
                sky.Advance(60);
                Check(Mathf.Abs(sky.CurrentState.StarDensity - SpaceSkyState.Starfield.StarDensity) < .0001f, "The transition converges to the requested world visual state");
                float time = sky.VisualTime; float density = sky.CurrentState.StarDensity;
                sky.SetVisualState(SpaceSkyState.Cloud); sky.Advance(0);
                Check(sky.VisualTime == time && sky.CurrentState.StarDensity == density, "Pause freezes the visual clock and state transitions");
                sky.AnimationSpeed = 0; sky.Advance(10);
                Check(sky.VisualTime == time, "Zero animation speed freezes stars, clouds and demonstration");
                var active = sky.ActiveMaterial;
                sky.enabled = false;
                Check(!sky.ActiveMaterial && !sky.Dome.enabled && sky.Dome.sharedMaterial != active, "Disabling the module hides its sphere and releases its material");
                sky.enabled = true;
                Check(sky.ActiveMaterial && sky.Dome.enabled && sky.Dome.sharedMaterial == sky.ActiveMaterial, "Re-enabling creates and binds a fresh sphere material");
                Check(templateBefore == EditorJsonUtility.ToJson(sky.Template), "Animation and manual state changes never alter the shared source material");
            }
            finally { sky.enabled = false; Object.DestroyImmediate(root); }
            Check(RenderSettings.skybox == originalSky, "Isolated checks restore the live sky without changing gameplay or its visual clock");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "runtime-validation.json"), JsonUtility.ToJson(new Report {
                success = true, backend = SystemInfo.graphicsDeviceType.ToString(), checks = checks.ToArray()
            }, true));
            return checks.ToArray();
        }

        static int Bright(Color32[] pixels) => pixels.Count(p => p.r + p.g + p.b > 420);
        static float Mean(Color32[] pixels) => (float)(pixels.Sum(p => (double)p.r + p.g + p.b) / (pixels.Length * 765.0));
        static float Difference(Color32[] a, Color32[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
            return (float)(sum / (a.Length * 765.0));
        }

        static float SquarePixelFraction(Color32[] pixels, int width, int height, int size)
        {
            int consistent = 0, total = 0;
            // ReadPixels is bottom-up; start at the top to match fragment screen positions on Metal.
            for (int y = 0; y + size <= height; y += size)
                for (int x = 0; x + size <= width; x += size)
                {
                    var first = pixels[(height - 1 - y) * width + x]; bool same = true;
                    for (int dy = 0; dy < size; dy++)
                        for (int dx = 0; dx < size; dx++)
                        {
                            var p = pixels[(height - 1 - y - dy) * width + x + dx];
                            same &= Math.Abs(p.r - first.r) <= 2 && Math.Abs(p.g - first.g) <= 2 && Math.Abs(p.b - first.b) <= 2;
                        }
                    total++; if (same) consistent++;
                }
            return (float)consistent / total;
        }

        sealed class SkyRender : IDisposable
        {
            readonly Scene scene;
            readonly GameObject root;
            readonly Material material;
            readonly RenderTexture target;
            readonly Texture2D readback;
            readonly MeshRenderer dome;
            public Camera Camera { get; }
            public SpaceSkySettings Settings { get; }
            public Scene PreviewScene => scene;

            public SkyRender(Material source, SpaceSkySettings settings, int width = 1280, int height = 720)
            {
                scene = Application.isPlaying ? default : EditorSceneManager.NewPreviewScene();
                root = new GameObject("Sky render validation") { hideFlags = HideFlags.HideAndDontSave };
                if (scene.IsValid()) SceneManager.MoveGameObjectToScene(root, scene);
                Camera = root.AddComponent<Camera>(); Camera.enabled = false; Camera.cameraType = CameraType.Preview;
                if (scene.IsValid()) Camera.scene = scene;
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = Color.magenta;
                Camera.cullingMask = 1 << 31; Camera.fieldOfView = 60;
                Camera.allowHDR = true; Camera.allowMSAA = false;
                Camera.transform.rotation = Quaternion.Euler(-8, -13, 0);
                Camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                Camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                Settings = Object.Instantiate(settings); material = new Material(source);
                var sphere = new GameObject("Actual celestial sphere") { layer = 31 };
                sphere.transform.SetParent(root.transform, false);
                sphere.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SpaceSkyAuthoring.MeshPath);
                dome = sphere.AddComponent<MeshRenderer>(); dome.sharedMaterial = material;
                dome.shadowCastingMode = ShadowCastingMode.Off; dome.receiveShadows = false;
                dome.lightProbeUsage = LightProbeUsage.Off; dome.reflectionProbeUsage = ReflectionProbeUsage.Off;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); Camera.targetTexture = target;
                readback = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            }

            public Color32[] Draw(SpaceSkyState state, float time, string name = null)
            {
                SpaceSky.ApplyAppearance(material, Settings); SpaceSky.ApplyState(material, state, time);
                dome.bounds = new Bounds(Camera.transform.position, Vector3.one * 10);
                RenderPipeline.SubmitRenderRequest(Camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readback.Apply();
                    if (name != null) File.WriteAllBytes(Path.Combine(Output, name + ".png"), readback.EncodeToPNG());
                    return readback.GetPixels32();
                }
                finally { RenderTexture.active = previous; }
            }

            public void Dispose()
            {
                Camera.targetTexture = null; target.Release();
                Object.DestroyImmediate(readback); Object.DestroyImmediate(target); Object.DestroyImmediate(material);
                Object.DestroyImmediate(Settings); Object.DestroyImmediate(root);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
