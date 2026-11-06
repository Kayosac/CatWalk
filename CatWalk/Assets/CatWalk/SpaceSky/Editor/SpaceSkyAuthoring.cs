using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CatWalk.Rendering.Editor
{
    public static class SpaceSkyAuthoring
    {
        public const string Root = "Assets/CatWalk/SpaceSky";
        public const string SettingsPath = Root + "/Settings/SpaceSky.asset";
        public const string MaterialPath = Root + "/Materials/DeepSpace.mat";
        public const string PrefabPath = Root + "/Prefabs/SpaceSky.prefab";
        public const string MeshPath = Root + "/Meshes/SpaceSkySphere.asset";

        public static SpaceSkySettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SpaceSkySettings>(SettingsPath);
            if (settings) return settings;
            settings = ScriptableObject.CreateInstance<SpaceSkySettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        public static Material ConfigureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                material = new Material(Shader.Find("CatWalk/Space Sky"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            SpaceSky.ApplyAppearance(material, EnsureSettings());
            SpaceSky.ApplyState(material, SpaceSkyState.Galaxy, 0);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void Attach(GameObject parent)
        {
            if (parent.GetComponentInChildren<SpaceSky>(true)) return;
            PrefabUtility.InstantiatePrefab(EnsureModule(), parent.transform);
        }

        static GameObject EnsureModule()
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("SpaceSky");
            try
            {
                var sky = root.GetComponent<SpaceSky>();
                if (!sky) sky = root.AddComponent<SpaceSky>();
                if (!sky.Settings) sky.Settings = EnsureSettings();
                if (!sky.Template) sky.Template = ConfigureMaterial();
                if (!sky.Dome)
                {
                    var dome = new GameObject("Celestial sphere"); dome.transform.SetParent(root.transform, false);
                    dome.AddComponent<MeshFilter>().sharedMesh = EnsureSphere();
                    sky.Dome = dome.AddComponent<MeshRenderer>();
                }
                sky.Dome.sharedMaterial = sky.Template; sky.Dome.enabled = sky.enabled;
                sky.Dome.shadowCastingMode = ShadowCastingMode.Off; sky.Dome.receiveShadows = false;
                sky.Dome.lightProbeUsage = LightProbeUsage.Off; sky.Dome.reflectionProbeUsage = ReflectionProbeUsage.Off;
                sky.Dome.allowOcclusionWhenDynamic = false; sky.Dome.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static Mesh EnsureSphere()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh) return mesh;
            float t = (1 + Mathf.Sqrt(5)) * .5f;
            var vertices = new List<Vector3> {
                new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
                new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
                new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1)
            };
            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
            var triangles = new List<int> {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            for (int subdivision = 0; subdivision < 3; subdivision++)
            {
                var edges = new Dictionary<ulong, int>();
                int Midpoint(int a, int b)
                {
                    ulong key = ((ulong)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                    if (edges.TryGetValue(key, out int found)) return found;
                    int index = vertices.Count; vertices.Add((vertices[a] + vertices[b]).normalized); edges.Add(key, index); return index;
                }
                var next = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                    next.AddRange(new[] { a,ab,ca, b,bc,ab, c,ca,bc, ab,bc,ca });
                }
                triangles = next;
            }
            mesh = new Mesh { name = "Celestial sphere (no UVs)" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }

        [MenuItem("Tools/CatWalk/Sky/Configure Procedural Sky")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before installing the sky module.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Open a scene before installing its sky.");
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the destination scene first.");
            ConfigureMaterial();
            var prefab = EnsureModule();
            SpaceSky sky = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                sky = root.GetComponentInChildren<SpaceSky>(true);
                if (sky) break;
            }
            if (!sky)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add procedural space sky");
                sky = instance.GetComponent<SpaceSky>();
            }
            RenderSettings.skybox = null;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = sky.gameObject;
            Debug.Log("Procedural spherical sky configured in " + scene.name, sky);
        }

        [MenuItem("Tools/CatWalk/Sky/Select Sky Settings")]
        public static void SelectSettings() => Selection.activeObject = EnsureSettings();
    }

    [CustomEditor(typeof(SpaceSky))]
    public sealed class SpaceSkyInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var sky = (SpaceSky)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Play previews the animation. Presets stop the demo cycle and smoothly change the visual state. The sky has no gameplay dependencies.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                Preset("Quiet", SpaceSkyState.Quiet); Preset("Stars", SpaceSkyState.Starfield);
                Preset("Galaxies", SpaceSkyState.Galaxy); Preset("Nebula", SpaceSkyState.Cloud);
            }
            if (GUILayout.Button("Run demo cycle"))
            { Undo.RecordObject(sky, "Run sky demo"); sky.DemoCycle = true; EditorUtility.SetDirty(sky); }
            if (Application.isPlaying && sky.ActiveMaterial)
            {
                EditorGUILayout.LabelField("Visual time", sky.VisualTime.ToString("F1") + " s");
                Repaint();
            }
        }

        void Preset(string label, SpaceSkyState state)
        {
            if (!GUILayout.Button(label)) return;
            var sky = (SpaceSky)target;
            Undo.RecordObject(sky, "Preview sky " + label); sky.SetVisualState(state); EditorUtility.SetDirty(sky);
            PrefabUtility.RecordPrefabInstancePropertyModifications(sky);
        }
    }
}
