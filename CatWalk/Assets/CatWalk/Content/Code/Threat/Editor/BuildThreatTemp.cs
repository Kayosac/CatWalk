using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Временный сборщик угрозы (удалить после проверки). Оба пункта идемпотентны, лог — в Temp/threat_build.txt.
// Build Threat: материал Prototype_Red, импорт Pursuer.fbx с материалами, модуль Pursuer, префаб Pursuer
// (модель, двигатели JetBoost, красные огни), настройки Data/Threat/Threat.asset, префаб Threat (менеджер + строка на экране).
// Place Threat: в открытую сцену — объект Threat из префаба, укрытия Shelter на Конбини, исследовательской станции
// и станциях планет Мира, Охра, Ледышка. Сцену сохраняет.
static class BuildThreatTemp
{
    const string FbxPath = "Assets/CatWalk/Content/Environment/Models/Prototype/Pursuer.fbx";
    const string MaterialsDir = "Assets/CatWalk/Content/Environment/Materials/";
    const string ModulePath = "Assets/CatWalk/Content/Environment/Modules/Prototype/Pursuer.prefab";
    const string JetPath = "Assets/Plugins/Retro Arsenal/Prefabs/Interactive/Jet/JetBoost.prefab";
    const string FontPath = "Assets/CatWalk/Fonts/Resources/Fonts & Materials/Monocraft.asset";
    const string PursuerPath = "Assets/CatWalk/Prefabs/Pursuer.prefab";
    const string ThreatPath = "Assets/CatWalk/Prefabs/Threat.prefab";
    const string SettingsPath = "Assets/CatWalk/Data/Threat/Threat.asset";
    const string LogPath = "Temp/threat_build.txt";

    static readonly Color Red = new(1f, 0.08f, 0.05f);

    [MenuItem("CatWalk/Temp/Build Threat")]
    static void Build()
    {
        File.WriteAllText(LogPath, "Build Threat\n");
        try
        {
            Material red = RedMaterial();
            ImportModel(red);
            BuildModule();
            BuildPursuer();
            ThreatSettings settings = Settings();
            BuildThreat(settings);
            AssetDatabase.SaveAssets();
            Log("готово");
        }
        catch (System.Exception e)
        {
            Log("ОШИБКА: " + e);
            throw;
        }
    }

    // URP Lit, плоский красный со свечением — огни охотника.
    static Material RedMaterial()
    {
        string path = MaterialsDir + "Prototype_Red.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
            Log("создан " + path);
        }
        m.SetColor("_BaseColor", Red);
        m.SetFloat("_Smoothness", 0.25f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Red * 4f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Материалы FBX — по имени на общие из Environment/Materials, без копий внутри модели.
    static void ImportModel(Material red)
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) throw new System.Exception("нет модели " + FbxPath);
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Prototype_Dark"), Load<Material>(MaterialsDir + "Prototype_Dark.mat"));
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Prototype_Rust"), Load<Material>(MaterialsDir + "Prototype_Rust.mat"));
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Prototype_Red"), red);
        importer.SaveAndReimport();
        Log("импорт " + FbxPath);
    }

    // Модуль: распакованная модель (меш и материалы), без коллайдеров — охотник ни с чем не сталкивается.
    static void BuildModule()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModulePath) != null) return;
        Scene preview = EditorSceneManager.NewPreviewScene();
        var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(FbxPath), preview);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "Pursuer";
        PrefabUtility.SaveAsPrefabAsset(model, ModulePath);
        EditorSceneManager.ClosePreviewScene(preview);
        Log("создан " + ModulePath);
    }

    // Охотник: модель, два факела JetBoost назад (+Y факела — назад, как у главного двигателя космокара), красные огни.
    static void BuildPursuer()
    {
        GameObject module = Load<GameObject>(ModulePath);
        GameObject jet = Load<GameObject>(JetPath);
        EnsurePrefab(PursuerPath, "Pursuer");
        using (var scope = new PrefabUtility.EditPrefabContentsScope(PursuerPath))
        {
            Transform root = scope.prefabContentsRoot.transform;
            Nested(root, module, "Model", Vector3.zero, Quaternion.identity, 1f);
            for (int s = -1; s <= 1; s += 2)
            {
                string side = s < 0 ? "L" : "R";
                Nested(root, jet, "Engine " + side, new Vector3(s * 1.35f, -0.1f, -5.4f), Quaternion.Euler(-90f, 0f, 0f), 1.6f);
                Lamp(root, "Light " + side, new Vector3(s * 4.95f, -0.75f, 1.45f), 3f, 20f);
            }
            Lamp(root, "Eye light", new Vector3(0f, 0.3f, 4.8f), 4f, 30f);
        }
        Log("собран " + PursuerPath);
    }

    static ThreatSettings Settings()
    {
        ThreatSettings settings = AssetDatabase.LoadAssetAtPath<ThreatSettings>(SettingsPath);
        if (settings != null) return settings;
        if (!AssetDatabase.IsValidFolder("Assets/CatWalk/Data/Threat")) AssetDatabase.CreateFolder("Assets/CatWalk/Data", "Threat");
        settings = ScriptableObject.CreateInstance<ThreatSettings>();
        AssetDatabase.CreateAsset(settings, SettingsPath);
        Log("создан " + SettingsPath);
        return settings;
    }

    // Менеджер угрозы и его строка на экране: сверху по центру, под сообщениями Hud.
    static void BuildThreat(ThreatSettings settings)
    {
        TMP_FontAsset font = Load<TMP_FontAsset>(FontPath);
        EnsurePrefab(ThreatPath, "Threat");
        using (var scope = new PrefabUtility.EditPrefabContentsScope(ThreatPath))
        {
            GameObject root = scope.prefabContentsRoot;
            Transform hudObject = root.transform.Find("Threat HUD");
            if (hudObject == null)
            {
                hudObject = new GameObject("Threat HUD", typeof(RectTransform)).transform;
                hudObject.SetParent(root.transform, false);
            }
            Canvas canvas = Ensure<Canvas>(hudObject.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4;
            CanvasScaler scaler = Ensure<CanvasScaler>(hudObject.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform labelObject = hudObject.Find("Label");
            if (labelObject == null)
            {
                labelObject = new GameObject("Label", typeof(RectTransform)).transform;
                labelObject.SetParent(hudObject, false);
            }
            TextMeshProUGUI label = Ensure<TextMeshProUGUI>(labelObject.gameObject);
            var rect = (RectTransform)labelObject;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -150f);
            rect.sizeDelta = new Vector2(1100f, 80f);
            label.font = font;
            label.fontSize = 26f;
            label.color = new Color(1f, 0.4f, 0.35f);
            label.alignment = TextAlignmentOptions.Top;
            label.raycastTarget = false;
            label.text = "ОХОТНИК | 1,1 км | сзади";

            Threat threat = Ensure<Threat>(root);
            threat.settings = settings;
            threat.pursuerPrefab = Load<GameObject>(PursuerPath);
            threat.hud = canvas;
            threat.label = label;
        }
        Log("собран " + ThreatPath);
    }

    [MenuItem("CatWalk/Temp/Place Threat")]
    static void Place()
    {
        File.WriteAllText(LogPath, "Place Threat\n");
        try
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            Log("сцена " + scene.path);

            GameObject threat = null;
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == "Threat") threat = go;
            if (threat == null)
            {
                threat = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ThreatPath), scene);
                threat.name = "Threat";
                Log("поставлен Threat");
            }

            Location[] locations = Object.FindObjectsByType<Location>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            AddShelter(locations, "konbini", null, 300f);
            AddShelter(locations, "research", null, 400f);
            AddShelter(locations, "mira", "Station", 350f);
            AddShelter(locations, "ochre", "Station", 350f);
            AddShelter(locations, "ice", "Station", 350f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log("готово, сцена сохранена");
        }
        catch (System.Exception e)
        {
            Log("ОШИБКА: " + e);
            throw;
        }
    }

    // Укрытие на объекте Location с этим id или на его дочернем child (станция планеты).
    static void AddShelter(Location[] locations, string id, string child, float radius)
    {
        Location location = System.Array.Find(locations, l => l.id == id);
        if (location == null)
        {
            Log($"нет Location {id}");
            return;
        }
        Transform target = child == null ? location.transform : location.transform.Find(child);
        if (target == null)
        {
            Log($"нет {child} у {id}");
            return;
        }
        Shelter shelter = Ensure<Shelter>(target.gameObject);
        shelter.radius = radius;
        EditorUtility.SetDirty(shelter);
        Log($"укрытие {id}{(child != null ? "/" + child : "")}: {radius} м");
    }

    static void EnsurePrefab(string path, string name)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        Scene preview = EditorSceneManager.NewPreviewScene();
        var empty = new GameObject(name);
        SceneManager.MoveGameObjectToScene(empty, preview);
        PrefabUtility.SaveAsPrefabAsset(empty, path);
        EditorSceneManager.ClosePreviewScene(preview);
        Log("создан " + path);
    }

    // Вложенный префаб под именем name; при повторном запуске только поправляет позу.
    static void Nested(Transform parent, GameObject prefab, string name, Vector3 position, Quaternion rotation, float scale)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)).transform;
            t.name = name;
        }
        t.SetLocalPositionAndRotation(position, rotation);
        t.localScale = Vector3.one * scale;
    }

    static void Lamp(Transform parent, string name, Vector3 position, float intensity, float range)
    {
        Transform t = Child(parent, name);
        t.localPosition = position;
        Light light = Ensure<Light>(t.gameObject);
        light.type = LightType.Point;
        light.color = Red;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new System.Exception("нет ассета " + path);
        return asset;
    }

    static T Ensure<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    static Transform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    static void Log(string line)
    {
        File.AppendAllText(LogPath, line + "\n");
        Debug.Log("Threat: " + line);
    }
}
