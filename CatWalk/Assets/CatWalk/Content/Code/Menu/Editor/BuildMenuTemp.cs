using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatWalk.Rendering;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ВРЕМЕННЫЙ сборщик раздела Menu. Повторный запуск пересобирает то же самое. После проверки — удалить.
// Build Menu: ассет MetaEnding (если нет), префабы SettingsMenu / MainMenu / MetaAnomaly, сцена MainMenu,
//             порядок сцен в сборке (MainMenu — 0, Prototype — 1). Лог — Temp/menu_build.txt.
// Place Menu: в открытой Prototype — сбой мета-финала в пустоте и настройки у меню паузы. Лог — Temp/menu_place.txt.
static class BuildMenuTemp
{
    const string Prefabs = "Assets/CatWalk/Prefabs/";
    const string Ui = Prefabs + "UI/";
    const string MetaPath = "Assets/CatWalk/Data/Menu/MetaEnding.asset";
    const string Materials = "Assets/CatWalk/Content/Environment/Materials/";
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string PrototypePath = "Assets/Scenes/Prototype.unity";

    static readonly List<string> log = new();

    static void Log(string line)
    {
        log.Add(line);
        Debug.Log("Menu: " + line);
    }

    static void Error(string line)
    {
        log.Add("ОШИБКА: " + line);
        Debug.LogError("Menu: " + line);
    }

    static void Flush(string path)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllLines(path, log);
    }

    [MenuItem("CatWalk/Temp/Build Menu")]
    static void Build()
    {
        log.Clear();
        var window = AssetDatabase.LoadAssetAtPath<GameObject>(Ui + "MenuWindow.prefab");
        if (window == null)
        {
            Error("нет " + Ui + "MenuWindow.prefab");
            Flush("Temp/menu_build.txt");
            return;
        }

        MetaEndingSettings meta = EnsureMeta();
        SettingsMenu settings = BuildSettings(window);
        MainMenu main = BuildMainMenu(window, meta);
        BuildAnomaly(window, meta);
        AssetDatabase.SaveAssets();
        BuildScene(main, settings);
        SetBuildScenes();
        Flush("Temp/menu_build.txt");
    }

    // Ассет не пересоздаётся: автор правит в нём условия и тексты.
    static MetaEndingSettings EnsureMeta()
    {
        var meta = AssetDatabase.LoadAssetAtPath<MetaEndingSettings>(MetaPath);
        if (meta != null)
        {
            Log("MetaEnding.asset уже есть — не трогаю");
            return meta;
        }
        if (!AssetDatabase.IsValidFolder("Assets/CatWalk/Data/Menu")) AssetDatabase.CreateFolder("Assets/CatWalk/Data", "Menu");
        meta = ScriptableObject.CreateInstance<MetaEndingSettings>();
        AssetDatabase.CreateAsset(meta, MetaPath);
        Log("создан " + MetaPath + " (условия-заглушки: calls = call.TODO, minutes >= 120)");
        return meta;
    }

    // Окно меню: вложенный MenuWindow, поля MenuScreen — из его Panel.
    static GameObject NewMenu<T>(GameObject window, string name, string hint) where T : MenuScreen
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(window);
        root.name = name;
        T menu = root.AddComponent<T>();
        menu.title = root.transform.Find("Panel/Title").GetComponent<TMP_Text>();
        menu.status = root.transform.Find("Panel/Status").GetComponent<TMP_Text>();
        menu.list = root.transform.Find("Panel/List");
        menu.rowTemplate = root.transform.Find("Panel/Row").GetComponent<Button>();
        menu.pauseTime = true;
        menu.closeOnE = false;
        Transform h = root.transform.Find("Panel/Hint");
        if (h != null) h.GetComponent<TMP_Text>().text = hint;
        return root;
    }

    static T Save<T>(GameObject root, string path) where T : Component
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Log("префаб " + path);
        return saved != null ? saved.GetComponent<T>() : null;
    }

    static SettingsMenu BuildSettings(GameObject window)
    {
        GameObject root = NewMenu<SettingsMenu>(window, "SettingsMenu", "Клик — изменить | Esc — назад");
        root.SetActive(false);
        return Save<SettingsMenu>(root, Ui + "SettingsMenu.prefab");
    }

    // Главное меню: окно слева (справа — вращающийся космокар), метка-пасхалка в правом нижнем углу.
    static MainMenu BuildMainMenu(GameObject window, MetaEndingSettings meta)
    {
        GameObject root = NewMenu<MainMenu>(window, "MainMenu", "");
        MainMenu menu = root.GetComponent<MainMenu>();
        menu.meta = meta;

        var panel = (RectTransform)root.transform.Find("Panel");
        panel.sizeDelta = new Vector2(640f, panel.sizeDelta.y);
        panel.anchoredPosition = new Vector2(-480f, 0f);

        var egg = new GameObject("Egg", typeof(RectTransform));
        egg.transform.SetParent(root.transform, false);
        var rect = (RectTransform)egg.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 16f);
        rect.sizeDelta = new Vector2(80f, 50f);
        var text = egg.AddComponent<TextMeshProUGUI>();
        text.font = menu.title.font;
        text.fontSize = 28;
        text.alignment = TextAlignmentOptions.BottomRight;
        text.text = "_";
        var button = egg.AddComponent<Button>();
        button.targetGraphic = text;
        button.transition = Selectable.Transition.None;
        egg.SetActive(false);
        menu.egg = button;

        root.SetActive(true);             // Start открывает меню сам
        return Save<MainMenu>(root, Ui + "MainMenu.prefab");
    }

    // Сбой: куски-кубы (Visual с Location «???», выключен) и экран финала (Screen, выключен).
    static void BuildAnomaly(GameObject window, MetaEndingSettings meta)
    {
        Material glow = EnsureMaterial("Prototype_MetaAnomaly", Color.white, new Color(2f, 2f, 2f));
        Material dark = EnsureMaterial("Prototype_MetaAnomalyDark", Color.black, Color.black);

        var root = new GameObject("MetaAnomaly");
        MetaAnomaly anomaly = root.AddComponent<MetaAnomaly>();
        anomaly.settings = meta;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        Location location = visual.AddComponent<Location>();
        location.id = "meta-anomaly";
        location.title = "???";
        location.kind = Location.Kind.Find;
        location.radius = 20f;
        var rnd = new System.Random(406);
        for (int i = 0; i < 9; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "Piece " + (i + 1);
            Object.DestroyImmediate(piece.GetComponent<Collider>());
            piece.transform.SetParent(visual.transform, false);
            piece.transform.localPosition = new Vector3(R(rnd, -8f, 8f), R(rnd, -8f, 8f), R(rnd, -8f, 8f));
            piece.transform.localRotation = Quaternion.Euler(R(rnd, 0f, 360f), R(rnd, 0f, 360f), R(rnd, 0f, 360f));
            piece.transform.localScale = new Vector3(R(rnd, 2f, 9f), R(rnd, 0.5f, 6f), R(rnd, 1f, 7f));
            piece.GetComponent<MeshRenderer>().sharedMaterial = i % 3 == 0 ? glow : dark;
        }
        visual.SetActive(false);
        anomaly.visual = visual;

        TMP_Text font = window.transform.Find("Panel/Title").GetComponent<TMP_Text>();
        var screenObject = new GameObject("Screen", typeof(RectTransform));
        screenObject.transform.SetParent(root.transform, false);
        Canvas canvas = screenObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = screenObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var frameObject = new GameObject("Frame", typeof(RectTransform));
        frameObject.transform.SetParent(screenObject.transform, false);
        var frameRect = (RectTransform)frameObject.transform;
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
        RawImage frame = frameObject.AddComponent<RawImage>();
        frame.raycastTarget = false;
        frameObject.SetActive(false);

        var lineObject = new GameObject("Line", typeof(RectTransform));
        lineObject.transform.SetParent(screenObject.transform, false);
        var lineRect = (RectTransform)lineObject.transform;
        lineRect.anchorMin = lineRect.anchorMax = lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.anchoredPosition = new Vector2(0f, 80f);
        lineRect.sizeDelta = new Vector2(1500f, 220f);
        var line = lineObject.AddComponent<TextMeshProUGUI>();
        line.font = font.font;
        line.fontSize = 34;
        line.alignment = TextAlignmentOptions.Bottom;
        line.color = Color.white;
        line.raycastTarget = false;
        line.text = "";

        MetaEndingScreen screen = screenObject.AddComponent<MetaEndingScreen>();
        screen.line = line;
        screen.frame = frame;
        screen.pauseTime = false;
        screen.closeOnE = false;
        screenObject.SetActive(false);
        anomaly.screen = screen;

        Save<MetaAnomaly>(root, Prefabs + "MetaAnomaly.prefab");
    }

    static float R(System.Random rnd, float min, float max) => min + (float)rnd.NextDouble() * (max - min);

    static Material EnsureMaterial(string name, Color color, Color emission)
    {
        string path = Materials + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
            Log("материал " + path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0f);
        if (emission.maxColorComponent > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Сцена MainMenu: космос (SpaceSky), свет, вращающаяся модель космокара, главное меню и настройки.
    static void BuildScene(MainMenu mainPrefab, SettingsMenu settingsPrefab)
    {
        if (mainPrefab == null || settingsPrefab == null)
        {
            Error("сцена не собрана: нет префабов меню");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Error("сцена не собрана: отменено сохранение открытых сцен");
            return;
        }
        string previous = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.32f);

        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, -10f), Quaternion.Euler(4f, 0f, 0f));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.backgroundColor = Color.black;
        camera.fieldOfView = 45f;
        camera.farClipPlane = 12000f;
        var resolution = AssetDatabase.LoadAssetAtPath<WorldResolutionSettings>("Assets/CatWalk/Rendering/Settings/WorldResolution.asset");
        var presentation = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>("Assets/CatWalk/Rendering/Settings/WorldPresentation_Renderer.asset");
        if (resolution != null && presentation != null)
        {
            WorldResolution world = cameraObject.AddComponent<WorldResolution>();
            world.Settings = resolution;
            world.PresentationRenderer = presentation;
        }
        else Error("нет настроек WorldResolution — камера рисует в разрешении экрана");

        var sunObject = new GameObject("Directional Light", typeof(Light));
        sunObject.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
        Light sun = sunObject.GetComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.3f;
        sun.color = new Color(1f, 0.95f, 0.88f);

        var sky = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CatWalk/SpaceSky/Prefabs/SpaceSky.prefab");
        if (sky != null) PrefabUtility.InstantiatePrefab(sky, scene);
        else Error("нет SpaceSky.prefab");

        // Модель стоит в корне машины так же, как в CosmoCar.prefab.
        var backdrop = new GameObject("Backdrop");
        backdrop.transform.SetPositionAndRotation(new Vector3(3.2f, -0.6f, 0f), Quaternion.Euler(8f, 0f, -6f));
        backdrop.AddComponent<MenuBackdrop>();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CatWalk/Content/CosmoCars/CosmoCar_1/CosmoCar_1.fbx");
        if (model != null)
        {
            var car = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            car.transform.SetParent(backdrop.transform, false);
            car.transform.localPosition = new Vector3(-0.04f, -0.57f, -0.71f);
            car.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        }
        else Error("нет CosmoCar_1.fbx");

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var main = (GameObject)PrefabUtility.InstantiatePrefab(mainPrefab.gameObject, scene);
        var settings = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab.gameObject, scene);
        MainMenu menu = main.GetComponent<MainMenu>();
        menu.settings = settings.GetComponent<SettingsMenu>();
        PrefabUtility.RecordPrefabInstancePropertyModifications(menu);

        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (saved) Log("сцена " + ScenePath);
        else Error("не сохранилась сцена " + ScenePath);
        if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous);
    }

    static void SetBuildScenes()
    {
        if (!File.Exists(ScenePath))
        {
            Error("порядок сцен не изменён: нет " + ScenePath);
            return;
        }
        var scenes = new List<EditorBuildSettingsScene> { new(ScenePath, true), new(PrototypePath, true) };
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.path != ScenePath && s.path != PrototypePath) scenes.Add(s);
        EditorBuildSettings.scenes = scenes.ToArray();
        Log("сцены в сборке: " + string.Join(", ", scenes.Select(s => s.path)));
    }

    [MenuItem("CatWalk/Temp/Place Menu")]
    static void Place()
    {
        log.Clear();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != PrototypePath)
        {
            Error("открой " + PrototypePath);
            Flush("Temp/menu_place.txt");
            return;
        }

        // Сбой мета-финала — в пустоте, как можно дальше от всех мест на карте.
        GameObject anomaly = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MetaAnomaly");
        if (anomaly != null) Log("MetaAnomaly уже есть: " + anomaly.transform.position);
        else
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "MetaAnomaly.prefab");
            if (prefab == null) Error("нет MetaAnomaly.prefab — сначала Build Menu");
            else
            {
                Vector3 spot = EmptySpot(out float clearance);
                anomaly = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                anomaly.transform.position = spot;
                Log($"MetaAnomaly поставлен в {spot}, до ближайшего места {clearance:0} м");
            }
        }

        // Пункт «Настройки» в паузе: SettingsMenu в сцене и ссылка на него.
        PauseMenu pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (pause == null) Error("в сцене нет PauseMenu");
        else if (pause.settings != null) Log("PauseMenu.settings уже задан: " + pause.settings.name);
        else
        {
            SettingsMenu settings = Object.FindFirstObjectByType<SettingsMenu>(FindObjectsInactive.Include);
            if (settings == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Ui + "SettingsMenu.prefab");
                if (prefab == null) Error("нет SettingsMenu.prefab — сначала Build Menu");
                else settings = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<SettingsMenu>();
            }
            if (settings != null)
            {
                pause.settings = settings;
                PrefabUtility.RecordPrefabInstancePropertyModifications(pause);
                EditorUtility.SetDirty(pause);
                Log("PauseMenu.settings -> " + settings.name);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Log("сцена сохранена");
        Flush("Temp/menu_place.txt");
    }

    static Vector3 EmptySpot(out float clearance)
    {
        Vector3[] candidates =
        {
            new(2600f, -900f, -1400f), new(-3400f, 1500f, -600f), new(600f, 2400f, 3200f),
            new(-1200f, -2000f, 4400f), new(4200f, 1200f, -2400f),
        };
        Location[] places = Object.FindObjectsByType<Location>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Vector3 best = candidates[0];
        clearance = -1f;
        foreach (Vector3 c in candidates)
        {
            float d = float.MaxValue;
            foreach (Location l in places) d = Mathf.Min(d, Vector3.Distance(c, l.transform.position) - l.radius);
            if (d > clearance)
            {
                clearance = d;
                best = c;
            }
        }
        return best;
    }
}
