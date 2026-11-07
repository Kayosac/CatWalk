using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DeepField.Editor
{
    /// <summary>Builds only the approved environment kit; never rewrites the game scene or actors.</summary>
    [InitializeOnLoad]
    public static class FuelStop04Authoring
    {
        const string Content = "Assets/SpaceCourier/Content/FuelStop04";
        const string Sources = "Assets/SpaceCourier/Resources/Models/FuelStop04";
        const string Prefabs = "Assets/SpaceCourier/Prefabs/Art/FuelStop04";
        const string PreviousAssemblyPath = Prefabs + "/FuelStop04.prefab";
        public const string AssemblyPath = "Assets/SpaceCourier/Prefabs/FuelStop04.prefab";
        public const string MaterialPath = Content + "/FuelStop04_Atlas.mat";
        static string Output => Path.GetFullPath("../Builds/FuelStop04");
        [Serializable] sealed class Model { public string name; public int vertices, triangles; public float[] size; public bool collider; }
        [Serializable] sealed class Instance { public string asset, name, group; public float[] position, rotation, scale; }
        [Serializable] sealed class Anchor { public string name; public float[] position; }
        [Serializable] sealed class Manifest { public Model[] assets; public Instance[] instances; public Anchor[] anchors; }
        [Serializable] sealed class Request { public string id, action; }
        [Serializable] sealed class Result { public string id, action, error; public bool success; public string[] checks; public int modelCount, instanceCount, triangles, textureCount; }
        static double nextPoll;
        static FuelStop04Authoring() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .4;
            const string queue = "Temp/fuel04-command.json";
            if (!File.Exists(queue)) return;
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(queue)); File.Delete(queue);
            var result = new Result { id = request.id, action = request.action };
            try
            {
                if (request.action == "build") Build();
                else if (request.action == "preview") RenderPreview();
                else if (request.action == "audit") AuditPrefabAsset();
                else if (request.action != "validate") throw new ArgumentException("Unknown Fuel 04 action");
                result.checks = Validate(out result.triangles);
                var manifest = LoadManifest(); result.modelCount = manifest.assets.Length; result.instanceCount = manifest.instances.Length; result.textureCount = 1;
                result.success = true;
            }
            catch (Exception e) { result.error = e.ToString(); Debug.LogException(e); }
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "editor-result.json"), JsonUtility.ToJson(result, true));
            Debug.Log("FUEL04 " + JsonUtility.ToJson(result));
        }
        static Manifest LoadManifest() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Content + "/FuelStop04Manifest.json"));
        static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

        [MenuItem("Tools/Deep Field/Fuel 04/Build approved environment")]
        public static void Build()
        {
            MoveAssemblyToMainPrefabDirectory();
            foreach (string path in new[] { Prefabs, Prefabs + "/Modules", Content + "/Meshes", Content + "/Bakes" }) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            string texturePath = Content + "/FuelStop04_Atlas.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
            importer.maxTextureSize = 512; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true; importer.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var shader = Shader.Find("DeepField/Fuel 04 Single Atlas");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Fuel 04 atlas shader did not compile.");
            if (!material) { material = new Material(shader) { name = "FuelStop04_Atlas" }; AssetDatabase.CreateAsset(material, MaterialPath); }
            material.shader = shader; material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Cutoff", .5f); material.SetFloat("_EmissionStrength", 1.3f); material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            var manifest = LoadManifest();
            foreach (var item in manifest.assets)
            {
                string sourcePath = Sources + "/" + item.name + ".fbx";
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (!source) throw new InvalidOperationException("Missing exported model: " + sourcePath);
                string profilePath = Content + "/Bakes/" + item.name + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<ModelBakeProfile>(profilePath);
                if (!profile) { profile = ScriptableObject.CreateInstance<ModelBakeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
                profile.Source = source; profile.PrefabPath = Prefabs + "/Modules/" + item.name + ".prefab";
                profile.MeshDirectory = Content + "/Meshes"; profile.MaterialDirectory = Content;
                profile.Character = false; profile.ImportRotation = new Vector3(0, 180, 0);
                profile.Materials.Clear(); profile.Materials.Add(new ModelBakeProfile.MaterialBinding { SourceName = "FuelStop04_Atlas", Material = material });
                BakeIsolatedModule(profile, material);
                var module = PrefabUtility.LoadPrefabContents(profile.PrefabPath);
                try
                {
                    foreach (var renderer in module.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.sharedMaterial = material;
                        renderer.shadowCastingMode = item.collider ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        renderer.receiveShadows = true;
                        var collider = renderer.GetComponent<MeshCollider>();
                        if (item.collider)
                        {
                            if (!collider) collider = renderer.gameObject.AddComponent<MeshCollider>();
                            collider.sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh; collider.convex = false;
                        }
                        else if (collider) Object.DestroyImmediate(collider);
                    }
                    PrefabUtility.SaveAsPrefabAsset(module, profile.PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(module); }
            }
            // Rebuild only this generated assembly. Individual model prefab GUIDs stay stable.
            var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("FuelStop04");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var groups = new Dictionary<string, Transform>();
                foreach (var item in manifest.instances)
                {
                    if (!groups.TryGetValue(item.group, out var parent)) { parent = Child(root.transform, item.group); groups[item.group] = parent; }
                    var module = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Modules/" + item.asset + ".prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(module, parent); instance.name = item.name;
                    instance.transform.localPosition = V(item.position); instance.transform.localEulerAngles = V(item.rotation); instance.transform.localScale = V(item.scale);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                }
                var anchors = Child(root.transform, "Placement anchors");
                foreach (var anchor in manifest.anchors) Child(anchors, anchor.name).localPosition = V(anchor.position);
                var lighting = Child(root.transform, "Practical lights");
                PointLight(lighting, "Counter warm pool", new Vector3(-8.5f, 2.85f, 3.3f), new Color(1, .67f, .32f), 5.5f, 3.2f);
                PointLight(lighting, "Pump work light", new Vector3(10.5f, 2.5f, -3.7f), new Color(1, .74f, .44f), 4, 1.4f);
                PointLight(lighting, "Cabin shelf light", new Vector3(-10.65f, 2.12f, 9.94f), new Color(1, .73f, .43f), 6, 2.2f);
                PrefabUtility.SaveAsPrefabAsset(root, AssemblyPath);
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.SaveAssets();
        }
        static void MoveAssemblyToMainPrefabDirectory()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AssemblyPath) || !AssetDatabase.LoadAssetAtPath<GameObject>(PreviousAssemblyPath)) return;
            string error = AssetDatabase.MoveAsset(PreviousAssemblyPath, AssemblyPath);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        [Serializable] sealed class PrefabAssetAudit
        {
            public string path, guid, name, mainAssetType, prefabAssetType, instanceStatus;
            public int renderers, nestedModules, missingMeshes, missingMaterials, missingScripts;
            public bool isPrefabAsset, success;
        }
        [MenuItem("Tools/Deep Field/Fuel 04/Check assembled prefab asset")]
        public static void AuditPrefabAsset()
        {
            MoveAssemblyToMainPrefabDirectory();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssemblyPath);
            if (!asset) throw new InvalidOperationException("The assembled FuelStop04 prefab is missing");
            var audit = new PrefabAssetAudit {
                path = AssemblyPath, guid = AssetDatabase.AssetPathToGUID(AssemblyPath), name = asset.name,
                mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(AssemblyPath).Name,
                prefabAssetType = PrefabUtility.GetPrefabAssetType(asset).ToString(),
                isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(asset)
            };
            var scene = EditorSceneManager.NewPreviewScene();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            try
            {
                audit.instanceStatus = PrefabUtility.GetPrefabInstanceStatus(instance).ToString();
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                audit.renderers = renderers.Length;
                audit.missingMeshes = instance.GetComponentsInChildren<MeshFilter>(true).Count(f => !f.sharedMesh);
                audit.missingMaterials = renderers.Count(r => r.sharedMaterials.Length == 0 || r.sharedMaterials.Any(m => !m));
                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                {
                    audit.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (t != instance.transform && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) audit.nestedModules++;
                }
                audit.success = audit.isPrefabAsset && audit.mainAssetType == "GameObject" && audit.prefabAssetType == "Regular" &&
                    audit.instanceStatus == "Connected" && audit.renderers == LoadManifest().instances.Length && audit.nestedModules == LoadManifest().instances.Length &&
                    audit.missingMeshes == 0 && audit.missingMaterials == 0 && audit.missingScripts == 0;
            }
            finally { Object.DestroyImmediate(instance); EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.SetLabels(asset, new[] { "Fuel04", "Station", "Environment", "Assembled" });
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "prefab-asset-audit.json"), JsonUtility.ToJson(audit, true));
            if (!audit.success) throw new InvalidOperationException("FuelStop04 failed prefab-asset verification; inspect prefab-asset-audit.json");
        }
        // The standard art refresher intentionally requires Edit Mode. This initial kit build
        // touches only its own new assets and preview scenes, so another task's live game can
        // keep running. Profiles remain compatible with the standard refresher afterwards.
        static void BakeIsolatedModule(ModelBakeProfile profile, Material material)
        {
            var parts = profile.Source.GetComponentsInChildren<MeshFilter>();
            var combines = new List<CombineInstance>();
            foreach (var part in parts)
                for (int sub = 0; sub < part.sharedMesh.subMeshCount; sub++)
                    combines.Add(new CombineInstance { mesh=part.sharedMesh,subMeshIndex=sub,
                        transform=Matrix4x4.Rotate(Quaternion.Euler(profile.ImportRotation))*profile.Source.transform.worldToLocalMatrix*part.transform.localToWorldMatrix });
            string meshPath=profile.MeshDirectory+"/"+profile.Source.name+"_FuelStop04_Atlas.asset";
            var combined=new Mesh { name=profile.Source.name+"_FuelStop04_Atlas",indexFormat=IndexFormat.UInt32 };
            combined.CombineMeshes(combines.ToArray());
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(mesh){EditorUtility.CopySerialized(combined,mesh);Object.DestroyImmediate(combined);EditorUtility.SetDirty(mesh);}
            else {mesh=combined;AssetDatabase.CreateAsset(mesh,meshPath);}
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(profile.PrefabPath);
            var scene=existing?default:EditorSceneManager.NewPreviewScene();
            var root=existing?PrefabUtility.LoadPrefabContents(profile.PrefabPath):new GameObject(profile.Source.name);
            if(!existing)UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try
            {
                var geometry=root.transform.Find("FuelStop04_Atlas");if(!geometry)geometry=Child(root.transform,"FuelStop04_Atlas");
                var filter=geometry.GetComponent<MeshFilter>();if(!filter)filter=geometry.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
                var renderer=geometry.GetComponent<MeshRenderer>();if(!renderer)renderer=geometry.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                PrefabUtility.SaveAsPrefabAsset(root,profile.PrefabPath);
                profile.GeneratedParts=new List<string>{"FuelStop04_Atlas"};profile.SourceHash=AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(profile.Source)).ToString();EditorUtility.SetDirty(profile);
            }
            finally {if(existing)PrefabUtility.UnloadPrefabContents(root);else {Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}}
        }
        static Transform Child(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static void PointLight(Transform parent, string name, Vector3 position, Color color, float range, float intensity)
        {
            var t = Child(parent, name); t.localPosition = position;
            var light = t.gameObject.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.range = range;
            light.intensity = intensity; light.shadows = LightShadows.None;
        }

        [MenuItem("Tools/Deep Field/Fuel 04/Validate environment")]
        public static void ValidateMenu() { var checks = Validate(out int triangles); Debug.Log(string.Join("\n", checks) + "\nTriangles: " + triangles); }
        public static string[] Validate(out int triangles)
        {
            var checks = new List<string>();
            void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Fuel 04: " + message); checks.Add(message); }
            var manifest = LoadManifest(); var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssemblyPath);
            Check(prefab && material, "Assembled prefab and shared material exist");
            var texture = material.GetTexture("_BaseMap") as Texture2D;
            Check(texture && texture.width == 512 && texture.height == 512 && texture.filterMode == FilterMode.Point, "One 512 x 512 nearest-filtered atlas");
            Check(!ShaderUtil.ShaderHasError(material.shader), "Atlas shader compiles on the current graphics backend");
            Check(material.GetTexturePropertyNames().Count(n => material.GetTexture(n)) == 1, "The shared material samples one texture, including opacity and lamps");
            Check(AssetDatabase.GetDependencies(AssemblyPath, true).Count(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) == 1, "Entire assembled prefab depends on exactly one image");
            Check(manifest.assets.Length == 35, "All 35 separately reusable models exported, including mounts and cabin furnishings");
            foreach (var item in manifest.assets)
            {
                var module = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Modules/" + item.name + ".prefab");
                Check(module, item.name + ": independent prefab available");
                var filters = module.GetComponentsInChildren<MeshFilter>();
                Check(filters.Length == 1 && filters[0].sharedMesh.subMeshCount == 1, item.name + ": one mesh and one submesh");
                var mesh = filters[0].sharedMesh;
                Check(mesh.uv.Length == mesh.vertexCount && mesh.colors.Length == mesh.vertexCount, item.name + ": atlas UVs and lamp vertex data survived import");
                Check(module.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.Length == 1 && r.sharedMaterial == material), item.name + ": same shared material");
                Vector3 size = mesh.bounds.size, expected = V(item.size);
                Check((size - expected).magnitude < .03f, item.name + ": metre scale and axis conversion match source");
                Check(mesh.uv.All(uv => uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1), item.name + ": UVs remain inside the atlas");
            }
            var scene = EditorSceneManager.NewPreviewScene(); var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            try
            {
                var renderers = root.GetComponentsInChildren<MeshRenderer>();
                triangles = root.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
                Check(renderers.Length == manifest.instances.Length, manifest.instances.Length + " nested module instances assembled according to the layout");
                Check(root.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Environment contains no gameplay scripts, vehicles or characters");
                var decal = root.transform.Find("Decals/ParkingDecal");
                Check(decal && decal.GetComponentsInChildren<Collider>().Length == 0 && decal.GetComponentInChildren<MeshFilter>().sharedMesh.triangles.Length == 6, "Parking is a two-triangle transparent mesh decal, with no collider");
                Check(Vector3.Distance(decal.localPosition, new Vector3(0, .018f, -5)) < .001f, "Parking decal sits above the deck without z-fighting");
                Check(decal.GetComponentInChildren<MeshFilter>().sharedMesh.normals.All(n => n.y > .99f), "Parking decal faces upward");
                Check(texture.GetPixel(64, 64).a < .01f && texture.GetPixel(8, 64).a > .9f, "Parking atlas region contains transparent space and opaque paint");
                var sign = root.transform.Find("Lights/Sign04").GetComponentInChildren<MeshFilter>().sharedMesh;
                Check(sign.normals.Where((n, i) => sign.colors[i].a > .1f).All(n => n.z < -.99f), "Sign and service faces point toward the open approach");
                Check(root.transform.Find("Fuel/Tank 1").localPosition == new Vector3(4,0,7) && root.transform.Find("Fuel/Tank 2").localPosition == new Vector3(11,0,7), "The two tanks match approved coordinates");
                Check(root.transform.Find("Service/CabinShell").localPosition == new Vector3(-9,0,8), "Cabin matches approved back-left footprint");
                Check(triangles < 15000, "Complete station remains below 15,000 triangles");
                var obstacles = WalkingTriangles(root);
                var parcel = new[] { new Vector2(0,-9.4f), new Vector2(-7,-9.4f), new Vector2(-7,1.4f), new Vector2(-9,1.4f), new Vector2(-9,3.3f) };
                var fuel = new[] { new Vector2(-3.4f,-5), new Vector2(-3.4f,2), new Vector2(7.2f,2), new Vector2(7.2f,-3.2f) };
                ValidateRoute(parcel, obstacles, "Parcel"); checks.Add("Courier capsule can traverse the complete parcel route without mesh intersections");
                ValidateRoute(fuel, obstacles, "Fuel"); checks.Add("Courier capsule can traverse the complete fuel route without mesh intersections");
                var interior = new[] {new Vector2(-3.8f,9.35f),new Vector2(-6.2f,9.35f),new Vector2(-8.7f,8.5f),new Vector2(-9,6.65f)};
                ValidateRoute(interior, obstacles, "Cabin interior"); checks.Add("Cabin doorway and route behind the counter fit the courier capsule");
                Check(Mathf.Abs(root.transform.Find("Props/Spanner").position.y-root.transform.Find("Props/Workbench").GetComponentInChildren<Renderer>().bounds.max.y)<.001f,"Spanner rests on the workbench with no gap");
                Check(Mathf.Abs(root.transform.Find("Props/Radio").position.y-root.transform.Find("Service/ServiceCounter").GetComponentInChildren<Renderer>().bounds.max.y)<.001f,"Radio rests on the service counter with no gap");
                var ship = new Bounds(new Vector3(0,1.89f,-5), new Vector3(3.65f,3.75f,7.15f));
                Check(renderers.Where(r => r != decal.GetComponentInChildren<MeshRenderer>() && r.bounds.max.y > .04f).All(r => !r.bounds.Intersects(ship)), "Existing ship collision envelope fits the open pad with no overhead structures");
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "validation.json"), JsonUtility.ToJson(new Result { success=true,checks=checks.ToArray(),triangles=triangles,modelCount=manifest.assets.Length,instanceCount=manifest.instances.Length,textureCount=1 },true));
            return checks.ToArray();
        }
        struct Triangle { public Vector3 a,b,c; public Bounds bounds; }
        static List<Triangle> WalkingTriangles(GameObject root)
        {
            var list = new List<Triangle>();
            foreach (var collider in root.GetComponentsInChildren<MeshCollider>())
            {
                var mesh=collider.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;var matrix=collider.transform.localToWorldMatrix;
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=matrix.MultiplyPoint3x4(vertices[indices[i]]);var b=matrix.MultiplyPoint3x4(vertices[indices[i+1]]);var c=matrix.MultiplyPoint3x4(vertices[indices[i+2]]);
                    var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);
                    if(bounds.max.y < .05f || bounds.min.y > 1.7f) continue;
                    list.Add(new Triangle{a=a,b=b,c=c,bounds=bounds});
                }
            }
            return list;
        }
        static void ValidateRoute(Vector2[] route,List<Triangle> triangles,string name)
        {
            const float radius=.32f;
            for(int segment=0;segment<route.Length-1;segment++)
            {
                int count=Mathf.CeilToInt(Vector2.Distance(route[segment],route[segment+1])/.15f);
                for(int i=0;i<=count;i++)
                {
                    Vector2 p=Vector2.Lerp(route[segment],route[segment+1],i/(float)count);
                    var a=new Vector3(p.x,.43f,p.y);var b=new Vector3(p.x,1.3f,p.y);var area=new Bounds((a+b)/2,new Vector3(radius*2,.87f+radius*2,radius*2));
                    foreach(var t in triangles)
                        if(t.bounds.Intersects(area) && SegmentTriangleDistance(a,b,t)<radius*radius)
                            throw new InvalidOperationException(name+" route blocked near "+p+" by geometry at "+t.bounds.center);
                }
            }
        }
        static float SegmentTriangleDistance(Vector3 a,Vector3 b,Triangle t)
        {
            Vector3 d=b-a,e1=t.b-t.a,e2=t.c-t.a,h=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,h);
            if(Mathf.Abs(det)>.000001f)
            {
                float inv=1/det;Vector3 s=a-t.a;float u=inv*Vector3.Dot(s,h);Vector3 q=Vector3.Cross(s,e1);float v=inv*Vector3.Dot(d,q),along=inv*Vector3.Dot(e2,q);
                if(u>=0&&v>=0&&u+v<=1&&along>=0&&along<=1)return 0;
            }
            return Mathf.Min(PointTriangle(a,t),PointTriangle(b,t),SegmentSegment(a,b,t.a,t.b),SegmentSegment(a,b,t.b,t.c),SegmentSegment(a,b,t.c,t.a));
        }
        static float PointTriangle(Vector3 p,Triangle t)
        {
            var ab=t.b-t.a;var ac=t.c-t.a;var ap=p-t.a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0&&d2<=0)return ap.sqrMagnitude;
            var bp=p-t.b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return bp.sqrMagnitude;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return (p-(t.a+ab*(d1/(d1-d3)))).sqrMagnitude;
            var cp=p-t.c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return cp.sqrMagnitude;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return (p-(t.a+ac*(d2/(d2-d6)))).sqrMagnitude;
            float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return (p-(t.b+(t.c-t.b)*((d4-d3)/((d4-d3)+(d5-d6))))).sqrMagnitude;
            float sum=va+vb+vc;if(Mathf.Abs(sum)<1e-10f)return Mathf.Min(ap.sqrMagnitude,bp.sqrMagnitude,cp.sqrMagnitude);
            return (p-(t.a+ab*(vb/sum)+ac*(vc/sum))).sqrMagnitude;
        }
        static float SegmentSegment(Vector3 p1,Vector3 q1,Vector3 p2,Vector3 q2)
        {
            Vector3 d1=q1-p1,d2=q2-p2,r=p1-p2;float a=Vector3.Dot(d1,d1),e=Vector3.Dot(d2,d2),f=Vector3.Dot(d2,r),s,t;
            if(a<1e-10f&&e<1e-10f)return r.sqrMagnitude;
            if(a<1e-10f){s=0;t=Mathf.Clamp01(f/e);}
            else {float c=Vector3.Dot(d1,r);if(e<1e-10f){t=0;s=Mathf.Clamp01(-c/a);}else {float b=Vector3.Dot(d1,d2),denom=a*e-b*b;s=denom!=0?Mathf.Clamp01((b*f-c*e)/denom):0;t=(b*s+f)/e;if(t<0){t=0;s=Mathf.Clamp01(-c/a);}else if(t>1){t=1;s=Mathf.Clamp01((b-c)/a);}}}
            return (p1+d1*s-p2-d2*t).sqrMagnitude;
        }

        [MenuItem("Tools/Deep Field/Fuel 04/Render prefab preview")]
        public static void RenderPreview()
        {
            Directory.CreateDirectory(Output);
            var scene=EditorSceneManager.NewPreviewScene();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssemblyPath);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);GameObject cameraObject=null,keyObject=null;
            var rt=new RenderTexture(1440,1000,24,RenderTextureFormat.ARGB32){antiAliasing=1};Texture2D image=null;var previous=RenderTexture.active;
            try
            {
                cameraObject=new GameObject("Fuel 04 preview camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.cameraType=CameraType.Preview;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.027f,.033f,.045f);camera.nearClipPlane=.1f;camera.farClipPlane=200;
                camera.orthographic=true;camera.orthographicSize=17;camera.allowHDR=false;camera.allowMSAA=false;camera.targetTexture=rt;
                camera.transform.position=new Vector3(20,21,-46);camera.transform.LookAt(new Vector3(0,1.7f,0));
                var data=cameraObject.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=true;
                keyObject=new GameObject("Preview key");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(keyObject,scene);
                var key=keyObject.AddComponent<Light>();key.type=LightType.Directional;key.color=new Color(.87f,.91f,1);key.intensity=1.05f;key.shadows=LightShadows.Soft;keyObject.transform.rotation=Quaternion.Euler(48,-35,0);
                camera.Render();RenderTexture.active=rt;image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Output,"FuelStop04_unity.png"),image.EncodeToPNG());
                int visible=image.GetPixels32().Count(c=>c.r>35||c.g>35||c.b>35);if(visible<rt.width*rt.height/50)throw new InvalidOperationException("Unity preview is blank");
                var lit=image.GetPixels32();foreach(var practical in root.GetComponentsInChildren<Light>())practical.enabled=false;
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                var unlit=image.GetPixels32();int affected=0;
                for(int i=0;i<lit.Length;i++)if(Mathf.Abs(lit[i].r-unlit[i].r)+Mathf.Abs(lit[i].g-unlit[i].g)+Mathf.Abs(lit[i].b-unlit[i].b)>5)affected++;
                File.WriteAllText(Path.Combine(Output,"lighting-validation.json"),"{\"affectedPixels\":"+affected+",\"success\":"+(affected>100?"true":"false")+"}");
                if(affected<=100)throw new InvalidOperationException("Practical lamps did not illuminate the rendered surfaces");
                foreach(var practical in root.GetComponentsInChildren<Light>())practical.enabled=true;
                void Shot(string name,Vector3 position,Vector3 target,float size,bool perspective=false)
                {
                    camera.orthographic=!perspective;camera.orthographicSize=size;camera.fieldOfView=65;
                    camera.transform.position=position;camera.transform.LookAt(target);camera.Render();RenderTexture.active=rt;
                    image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                    File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
                }
                Shot("FuelStop04_service",new Vector3(-3,5,-5),new Vector3(-9,1.9f,6),8);
                Shot("FuelStop04_interior",new Vector3(-5.65f,2.1f,8.95f),new Vector3(-10.5f,1.4f,8.4f),5,true);
                Shot("FuelStop04_interior_counter",new Vector3(-10.3f,2.3f,9.6f),new Vector3(-8.6f,1.4f,5.4f),5,true);
                Shot("FuelStop04_pipes",new Vector3(21,7,-7),new Vector3(10,.5f,1.2f),10);
                keyObject.transform.rotation=Quaternion.Euler(-45,-35,0);
                Shot("FuelStop04_underside",new Vector3(23,-18,-34),new Vector3(0,-1,0),19);
                keyObject.transform.rotation=Quaternion.Euler(48,-35,0);
                var roof=root.transform.Find("Service/CabinRoof").gameObject;roof.SetActive(false);
                Shot("FuelStop04_cabin_cutaway",new Vector3(-4,12,2),new Vector3(-9,1.5f,8),7);
                roof.SetActive(true);
                Shot("FuelStop04_top",new Vector3(0,45,-.01f),Vector3.zero,17);
            }
            finally {RenderTexture.active=previous;if(image)Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);if(cameraObject)Object.DestroyImmediate(cameraObject);if(keyObject)Object.DestroyImmediate(keyObject);Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
