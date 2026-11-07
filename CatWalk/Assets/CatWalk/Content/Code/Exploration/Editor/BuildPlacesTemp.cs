using System.Collections.Generic;
using System.IO;
using System.Text;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ВРЕМЕННЫЙ: новые места для исследования — сортировочный узел с потерянной почтой, маяк с записью,
// поселение Галька, заправка-баржа, ремонтная платформа.
// Build Places: импорт FBX с материалами палитры, модули, данные (заказы, профиль, запись), префабы мест. Сцены не трогает.
// Place Places: расставляет места в открытой сцене Prototype, задаёт маршрут баржи и добавляет заказы на Гальку почтам.
// Оба идемпотентны, лог — Temp/places_build.txt. После проверки — удалить.
static class BuildPlacesTemp
{
    const string Prefabs = "Assets/CatWalk/Prefabs/";
    const string Models = "Assets/CatWalk/Content/Environment/Models/Prototype/";
    const string Materials = "Assets/CatWalk/Content/Environment/Materials/";
    const string Modules = "Assets/CatWalk/Content/Environment/Modules/Prototype/";
    const string Fx = "Assets/Plugins/Retro Arsenal/Prefabs/";
    const string Orders = "Assets/CatWalk/Data/Delivery/";
    const string Residents = "Assets/CatWalk/Data/Residents/";
    const string Exploration = "Assets/CatWalk/Data/Exploration/";
    const string Font = "Assets/CatWalk/Fonts/Resources/Fonts & Materials/Monocraft.asset";
    const string TextAnimator = "Assets/Plugins/Febucci/Text Animator for Unity/";

    static readonly string[] Palette =
    {
        "Prototype_Cream", "Prototype_Orange", "Prototype_Dark", "Prototype_Teal", "Prototype_Warm", "Prototype_Rust",
        "Prototype_Green", "Prototype_Yellow", "Prototype_Blue", "Prototype_Rock", "Prototype_Red",
    };

    static readonly string[] Places = { "SortingHub", "Lighthouse", "RecordConsole", "Galka", "FuelBarge", "RepairPlatform" };

    static readonly Color Warm = new(1f, 0.72f, 0.4f);

    static Scene scene;
    static StringBuilder log;

    // ---------------------------------------------------------------- Build

    [MenuItem("CatWalk/Temp/Build Places")]
    static void Build()
    {
        log = new StringBuilder("Build Places\n");
        try
        {
            BuildMaterials();
            foreach (string name in Places) BuildModule(name);
            BuildData();
            scene = EditorSceneManager.NewPreviewScene();
            try
            {
                BuildSortingHub();
                BuildLighthouse();
                BuildGalka();
                BuildFuelBarge();
                BuildRepairPlatform();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.SaveAssets();
            log.AppendLine("готово");
        }
        catch (System.Exception e)
        {
            log.AppendLine("ОШИБКА: " + e);
            Debug.LogException(e);
        }
        WriteLog();
    }

    static void WriteLog()
    {
        string path = Path.Combine(Application.dataPath, "../Temp/places_build.txt");
        File.WriteAllText(path, log.ToString());
        Debug.Log(log.ToString());
    }

    static GameObject Load(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new System.Exception("Нет ассета: " + path);
        return asset;
    }

    static GameObject Nested(string path, Transform parent, Vector3 position, float yaw = 0f)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Load(path), scene);
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return go;
    }

    static GameObject Child(string name, Transform parent, Vector3 position)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        return go;
    }

    static GameObject Root(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    static void Save(GameObject root, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(root, Prefabs + name + ".prefab");
        Object.DestroyImmediate(root);
        log.AppendLine("префаб " + Prefabs + name + ".prefab");
    }

    // Два новых цвета палитры: камень и красные огни (URP Lit, плоский цвет / Emission).
    static void BuildMaterials()
    {
        MakeMaterial("Prototype_Rock", new Color(0.36f, 0.33f, 0.31f), 0f);
        MakeMaterial("Prototype_Red", new Color(0.95f, 0.2f, 0.12f), 2.5f);
    }

    static void MakeMaterial(string name, Color color, float emission)
    {
        string path = Materials + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
            log.AppendLine("материал " + path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Smoothness", 0.3f);
        if (emission > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(mat);
    }

    // FBX -> материалы палитры по имени, без своих копий; модуль — модель с коллайдерами (кроме ламп и разметки).
    static void BuildModule(string name)
    {
        string path = Models + name + ".fbx";
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new System.Exception("Нет модели: " + path);
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        foreach (string mat in Palette)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Material>(Materials + mat + ".mat");
            if (asset != null) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mat), asset);
        }
        importer.SaveAndReimport();

        scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(Load(path), scene);
            foreach (MeshFilter mesh in model.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mesh.gameObject.name;
                if (n.StartsWith("Lamp") || n.StartsWith("Marking") || n.StartsWith("Lights") || n.StartsWith("Floodlights") || n.StartsWith("WindowLit")) continue;
                if (mesh.GetComponent<MeshCollider>() == null) mesh.gameObject.AddComponent<MeshCollider>();
            }
            PrefabUtility.SaveAsPrefabAsset(model, Modules + name + ".prefab");
            Object.DestroyImmediate(model);
            log.AppendLine("модуль " + Modules + name + ".prefab");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static Transform Part(GameObject model, string name)
    {
        Transform t = model.transform.Find(name);
        if (t == null) log.AppendLine($"нет части {name} в {model.name}");
        return t;
    }

    static Light AddLight(string name, Transform parent, Vector3 position, LightType type, Color color, float intensity, float range, float angle = 30f)
    {
        GameObject go = Child(name, parent, position);
        var light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = angle;
        light.shadows = LightShadows.None;
        return light;
    }

    static Location AddLocation(GameObject root, string id, string title, Location.Kind kind, float radius, Vector3 arrival)
    {
        var location = root.AddComponent<Location>();
        location.id = id;
        location.title = title;
        location.kind = kind;
        location.radius = radius;
        location.arrival = Child("Arrival", root.transform, arrival).transform;
        return location;
    }

    static void AddGravity(GameObject root, Vector3 size)
    {
        var gravity = root.AddComponent<PlaneGravity>();
        gravity.size = size;
        gravity.falloff = 3f;
        gravity.priority = 1;
    }

    // Парковка без своей площадки (настил — в модели места).
    static void AddParking(Transform root, Vector3 position, Vector2 pad)
    {
        GameObject parking = Nested(Prefabs + "Parking.prefab", root, position);
        parking.GetComponent<ParkingZone>().padSize = pad;
        Transform plate = parking.transform.Find("Pad");
        if (plate != null) plate.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- Данные

    static T Asset<T>(string folder, string file) where T : ScriptableObject
    {
        string parent = Path.GetDirectoryName(folder.TrimEnd('/')).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder.TrimEnd('/'))) AssetDatabase.CreateFolder(parent, Path.GetFileName(folder.TrimEnd('/')));
        string path = folder + file + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            log.AppendLine("данные " + path);
        }
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static DeliveryOrder Order(string file, string cargo, string to, int reward, bool fragile, string reaction = "")
    {
        var order = Asset<DeliveryOrder>(Orders, file);
        order.cargo = cargo;
        order.to = to;
        order.reward = reward;
        order.fragile = fragile;
        order.reaction = reaction;
        return order;
    }

    static DeliveryOrder[] LostOrders() => new[]
    {
        AssetDatabase.LoadAssetAtPath<DeliveryOrder>(Orders + "LostToKonbini_Postcard.asset"),
        AssetDatabase.LoadAssetAtPath<DeliveryOrder>(Orders + "LostToOchre_Letter.asset"),
        AssetDatabase.LoadAssetAtPath<DeliveryOrder>(Orders + "LostToResearch_Tape.asset"),
        AssetDatabase.LoadAssetAtPath<DeliveryOrder>(Orders + "LostToMira_Seeds.asset"),
    };

    static FactCondition Fact(string fact, int value) => new() { fact = fact, compare = FactCondition.Compare.AtLeast, value = value };

    // Тексты временные — в тоне нарративной доски.
    static void BuildData()
    {
        // Потерянная почта сортировочного узла: адресаты — существующие почты, реакцию говорит житель адресата.
        Order("LostToKonbini_Postcard", "Открытка с узла (потерянная почта)", "konbini", 300, false,
            "Открытка... Штемпель восьмилетней давности. Это письмо шло восемь лет. Адресат — «почта, тому, кто дежурит». Значит, мне.");
        Order("LostToOchre_Letter", "Письмо без марки (потерянная почта)", "ochre", 420, false,
            "Это письмо шло восемь лет. Отвечать уже поздно. Но я всё равно отвечу — пусть тоже идёт восемь лет.");
        Order("LostToResearch_Tape", "Катушка плёнки (потерянная почта)", "research", 240, true,
            "Плёнка с узла? Тут кто-то полчаса считает звёзды. Досчитал до ста двенадцати. Продолжу за него.");
        Order("LostToMira_Seeds", "Пакетик семян (потерянная почта)", "mira", 440, false,
            "Семена с сортировки? Им лет десять. Посажу. Семена терпеливые, они дождутся.");

        // Галька: туда — чай и лампочки, оттуда — носки.
        DeliveryOrder tea = Order("KonbiniToGalka_Tea", "Пачка чая", "galka", 270, false);
        DeliveryOrder bulbs = Order("ResearchToGalka_Bulbs", "Лампочки для окна", "galka", 420, true);
        Order("GalkaToMira_Socks", "Вязаные носки", "mira", 240, false);

        ResidentProfile frosya = Asset<ResidentProfile>(Residents, "Frosya");
        frosya.displayName = "Фрося";
        frosya.greeting = "Ой. Гости. Я Фрося. Свет на окне видел? Это мой. Остальные окна не мои — их хозяева разъехались.";
        frosya.greetingKnown = "А, курьер. Проходи, лапы не вытирай — тут всё равно никого, кто заругается.";
        frosya.greetingWarm = "Мой курьер! Я теперь свет оставляю и для тебя. Раньше — только для тех, кто не вернётся.";
        frosya.warmDeliveries = 3;
        frosya.topics = new[]
        {
            "Раньше на Гальке было сорок окон. Теперь одно. Зато его видно издалека — проверено.",
            "Лампу меняю раз в год. Лампа говорит, что устала. Я говорю — я тоже, и ничего.",
            "Почту сюда не возят. То есть возят: ты. Значит, возят.",
            "Соседи писали, что вернутся, когда станет лучше. Пока не стало. Я жду, мне не трудно.",
        };
        frosya.thanks = "Спасибо. Положу у окна — пусть свет на неё тоже светит.";
        frosya.reactions = new[]
        {
            new ResidentProfile.CargoReaction { order = tea, line = "Чай! Заварю две кружки. Одну тебе, одну — по привычке." },
            new ResidentProfile.CargoReaction { order = bulbs, line = "Лампочки! Теперь окно не погаснет ещё лет десять. Если я не погасну раньше. Шучу. Наверное." },
        };
        frosya.damagedRemark = "Коробка побилась. Ничего, тут всё немножко побитое. Я тоже.";
        frosya.story = new[]
        {
            new ResidentProfile.StoryEvent { id = "frosya-1", minDeliveries = 1, after = "", line = "Почему свет? Тут был узел, сорок окон, все горели. Потом трассу перенесли. Если и я погашу — Гальки не станет даже на карте." },
            new ResidentProfile.StoryEvent { id = "frosya-2", minDeliveries = 3, after = "frosya-1", line = "Вчера кто-то мигнул фарами, пролетая. Я мигнула окном в ответ. Это теперь наш с ним разговор." },
        };

        LighthouseRecord record = Asset<LighthouseRecord>(Exploration, "LighthouseRecord");
        record.title = "Запись маяка";
        record.locked = "Шипение. Где-то глубоко в нём — щелчки, как будто кто-то ещё не начал говорить.";
        record.parts = new[]
        {
            new LighthouseRecord.Part { id = "keeper-1", conditions = new FactCondition[0],
                text = "...говорит маяк на краю, смена сорок один. Трасса чистая. Сегодня прошло девятнадцать бортов, один помахал. Записываю, если вдруг кто спросит, как тут было." },
            new LighthouseRecord.Part { id = "keeper-2", conditions = new[] { Fact("visits.lighthouse", 2) },
                text = "...смена сто двенадцать. Бортов — три. Почтовый привёз пирог от сестры из Гавани. Пирог помялся, но был вкусный. Свет сделаю поярче." },
            new LighthouseRecord.Part { id = "keeper-3", conditions = new[] { Fact("visits.lighthouse", 3), Fact("delivered", 8) },
                text = "...смена триста какая-то. Трассу перенесли. Сказали: маяк больше не нужен. Я сказал, что ему виднее. Свет оставляю. И почту на сортировке — тоже кто-то оставил." },
            new LighthouseRecord.Part { id = "keeper-4", conditions = new[] { Fact("visits.lighthouse", 3), Fact("place.lostmail", 2) },
                text = "...смена последняя. Если кто-то всё-таки довёз ту почту с узла — спасибо. Я так и думал, что довезут. Конец записи. Начало записи." },
        };
    }

    // ---------------------------------------------------------------- Места

    // Сортировочный узел: открытый ангар, мигающие лампы, искрящий провод, спрятанные до прибытия посылки.
    static void BuildSortingHub()
    {
        GameObject root = Root("SortingHub");
        AddLocation(root, "sorting-hub", "Сортировочный узел", Location.Kind.Find, 30f, new Vector3(0f, 20f, -60f));
        AddGravity(root, new Vector3(36f, 12f, 28f));
        GameObject model = Nested(Modules + "SortingHub.prefab", root.transform, Vector3.zero);
        model.name = "Model";
        AddParking(root.transform, new Vector3(0f, 0f, -7f), new Vector2(7f, 10f));
        Nested(Prefabs + "PhoneBooth.prefab", root.transform, new Vector3(10f, 0f, -3f));

        Transform lights = Child("Lights", root.transform, Vector3.zero).transform;
        foreach (string lamp in new[] { "Lamp_A", "Lamp_B", "Lamp_C" })
        {
            Transform bulb = Part(model, lamp);
            if (bulb == null) continue;
            Light light = AddLight("Light " + lamp, lights, bulb.localPosition + Vector3.down * 0.4f, LightType.Point, Warm, 2.2f, 10f);
            var flicker = light.gameObject.AddComponent<Flicker>();
            flicker.lamp = light;
            flicker.glow = bulb.GetComponent<Renderer>();
        }
        var sparks = AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "Environment/Sparkle/Mini/SparkleMiniYellowLoop.prefab");
        if (sparks != null)
        {
            GameObject fx = Nested(Fx + "Environment/Sparkle/Mini/SparkleMiniYellowLoop.prefab", lights, new Vector3(9f, 8.3f, 5f));
            fx.name = "Sparks";
            fx.transform.localScale = Vector3.one * 0.6f;
        }
        else log.AppendLine("нет VFX искр — пропущено");

        // Потерянная почта: лежит на лентах и на полу ангара, спрятана до прибытия.
        GameObject mail = Child("Lost mail", root.transform, Vector3.zero);
        DeliveryOrder[] orders = LostOrders();
        Vector3[] spots = { new(-7f, 0.95f, 6.6f), new(3f, 0.95f, 10.6f), new(9f, 0.05f, 11.8f), new(-11.5f, 0.05f, 3.6f) };
        for (int i = 0; i < orders.Length; i++)
        {
            GameObject box = Nested(Prefabs + "Parcel.prefab", mail.transform, spots[i], 17f + i * 53f);
            box.name = "Lost parcel " + (i + 1);
            box.GetComponent<Parcel>().order = orders[i];
            if (orders[i] == null) log.AppendLine("нет заказа потерянной посылки " + i);
        }
        mail.SetActive(false);

        var lost = root.AddComponent<LostMail>();
        lost.mail = mail;
        lost.orders = orders;
        Save(root, "SortingHub");
    }

    // Маяк: башня с вращающимся фонарём (два прожектора), пульт записи с экраном над ним.
    static void BuildLighthouse()
    {
        GameObject root = Root("Lighthouse");
        AddLocation(root, "lighthouse", "Маяк на краю", Location.Kind.Find, 25f, new Vector3(0f, 20f, -60f));
        AddGravity(root, new Vector3(28f, 10f, 28f));
        GameObject model = Nested(Modules + "Lighthouse.prefab", root.transform, Vector3.zero);
        model.name = "Model";
        AddParking(root.transform, new Vector3(0f, 0f, -6f), new Vector2(7f, 10f));
        Nested(Prefabs + "PhoneBooth.prefab", root.transform, new Vector3(-4.5f, 0f, 1.5f));

        Transform lamp = Part(model, "Lamp");
        if (lamp != null)
        {
            AddLight("Beam A", lamp, Vector3.right * 1.4f, LightType.Spot, Warm, 8f, 140f, 18f).transform.localRotation = Quaternion.LookRotation(Vector3.right);
            AddLight("Beam B", lamp, Vector3.left * 1.4f, LightType.Spot, Warm, 8f, 140f, 18f).transform.localRotation = Quaternion.LookRotation(Vector3.left);
        }

        GameObject console = Nested(Modules + "RecordConsole.prefab", root.transform, new Vector3(4.5f, 0f, 1.5f));
        console.name = "Record console";
        Transform use = Child("Use point", console.transform, new Vector3(0f, 0f, -0.9f)).transform;
        AddLight("Console light", console.transform, new Vector3(0f, 1.8f, -0.8f), LightType.Point, new Color(0.4f, 0.9f, 0.9f), 1.2f, 4f);

        var (canvas, typewriter) = RecordScreen(root.transform, new Vector3(4.5f, 2.5f, 1.5f));
        var lighthouse = root.AddComponent<Lighthouse>();
        lighthouse.record = AssetDatabase.LoadAssetAtPath<LighthouseRecord>(Exploration + "LighthouseRecord.asset");
        lighthouse.usePoint = use;
        lighthouse.screen = canvas;
        lighthouse.typewriter = typewriter;
        lighthouse.lamp = lamp;
        Save(root, "Lighthouse");
    }

    // Экран записи: world-space Canvas с панелью и печатающимся текстом (как облачко жителя, только шире).
    static (Canvas, TypewriterComponent) RecordScreen(Transform parent, Vector3 position)
    {
        GameObject go = new("Screen", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.localPosition = position;
        rect.localScale = Vector3.one * 0.01f;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(420f, 160f);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        GameObject panel = new("Panel", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(panel, scene);
        panel.transform.SetParent(go.transform, false);
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(420f, 0f);
        var image = panel.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.04f, 0.09f, 0.1f, 0.88f);
        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textGo = new("Text", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(textGo, scene);
        textGo.transform.SetParent(panel.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Font);
        if (font != null) text.font = font;
        text.fontSize = 20f;
        text.color = new Color(0.75f, 1f, 0.95f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.text = "";
        textGo.AddComponent<TextAnimator_TMP>();
        var typewriter = textGo.AddComponent<TypewriterComponent>();
        typewriter.TimingSettings = AssetDatabase.LoadAssetAtPath<TypingsTimingsScriptableBase>(TextAnimator + "Timings/Delays By Character.asset");
        typewriter.sharedSettings = AssetDatabase.LoadAssetAtPath<TypewriterSettingsScriptable>(TextAnimator + "Settings/Shared Typewriter Settings.asset");
        return (canvas, typewriter);
    }

    // Галька: астероид с настилом, домик с одним горящим окном, почта с жительницей Фросей.
    static void BuildGalka()
    {
        GameObject root = Root("Galka");
        AddLocation(root, "galka", "Галька", Location.Kind.Station, 25f, new Vector3(0f, 20f, -60f));
        AddGravity(root, new Vector3(24f, 10f, 20f));
        GameObject model = Nested(Modules + "Galka.prefab", root.transform, Vector3.zero);
        model.name = "Model";
        AddParking(root.transform, new Vector3(0f, 0f, -5f), new Vector2(7f, 9f));
        Nested(Prefabs + "PhoneBooth.prefab", root.transform, new Vector3(9.5f, 0f, -6f));

        Transform window = Part(model, "WindowLit");
        Vector3 glow = window != null ? window.GetComponent<Renderer>().bounds.center - model.transform.position : new Vector3(4.4f, 1.8f, 2.47f);
        AddLight("Window light", root.transform, glow + new Vector3(0f, 0f, -1f), LightType.Point, Warm, 3f, 9f);

        GameObject post = Nested(Prefabs + "PostTerminal.prefab", root.transform, new Vector3(-5f, 0f, 4f));
        var terminal = post.GetComponent<PostTerminal>();
        terminal.address = "galka";
        terminal.title = "Почта Гальки";
        terminal.orders = new[] { AssetDatabase.LoadAssetAtPath<DeliveryOrder>(Orders + "GalkaToMira_Socks.asset") };

        GameObject resident = Nested(Prefabs + "Resident.prefab", root.transform, new Vector3(-3.9f, 0f, 4.6f), 180f);
        var r = resident.GetComponent<Resident>();
        r.profile = AssetDatabase.LoadAssetAtPath<ResidentProfile>(Residents + "Frosya.asset");
        r.terminal = terminal;
        Save(root, "Galka");
    }

    // Заправка-баржа: кинематическое тело, колонка (дороже обычной), будка; маршрут задаёт Place Places.
    static void BuildFuelBarge()
    {
        GameObject root = Root("FuelBarge");
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        AddLocation(root, "barge", "Заправка-баржа «Бродяга»", Location.Kind.FuelStop, 25f, new Vector3(0f, 20f, -60f));
        AddGravity(root, new Vector3(14f, 10f, 30f));
        var barge = root.AddComponent<FuelBarge>();
        barge.route = new Vector3[0];
        barge.speed = 6f;
        barge.deck = new Vector3(16f, 20f, 36f);
        GameObject model = Nested(Modules + "FuelBarge.prefab", root.transform, Vector3.zero);
        model.name = "Model";
        AddParking(root.transform, new Vector3(0f, 0f, -8f), new Vector2(7f, 10f));
        GameObject pump = Nested(Prefabs + "FuelPump.prefab", root.transform, new Vector3(4.3f, 0f, -1f));
        pump.GetComponent<FuelPump>().pricePerLiter = 2;
        Nested(Prefabs + "PhoneBooth.prefab", root.transform, new Vector3(-4.5f, 0f, -0.5f));
        AddLight("Pump light", root.transform, new Vector3(3f, 3.5f, -1.5f), LightType.Point, Warm, 2.2f, 8f);
        Save(root, "FuelBarge");
    }

    // Ремонтная платформа «Заплатка»: только ремонт корпуса, вдвое дешевле мастерских.
    static void BuildRepairPlatform()
    {
        GameObject root = Root("RepairPlatform");
        AddLocation(root, "patch", "Ремонт «Заплатка»", Location.Kind.Station, 15f, new Vector3(0f, 20f, -50f));
        AddGravity(root, new Vector3(16f, 10f, 15f));
        GameObject model = Nested(Modules + "RepairPlatform.prefab", root.transform, Vector3.zero);
        model.name = "Model";
        AddParking(root.transform, new Vector3(0f, 0f, -3f), new Vector2(7f, 9f));
        GameObject terminal = Nested(Prefabs + "WorkshopTerminal.prefab", root.transform, new Vector3(4.5f, 0f, 2.5f));
        var workshop = terminal.GetComponent<WorkshopTerminal>();
        workshop.repairOnly = true;
        workshop.pricePerPercent = 1;
        Nested(Prefabs + "PhoneBooth.prefab", root.transform, new Vector3(-4.5f, 0f, 2f));
        AddLight("Flood A", root.transform, new Vector3(-7.3f, 5f, 4f), LightType.Spot, Warm, 6f, 16f, 70f).transform.localRotation = Quaternion.Euler(50f, 150f, 0f);
        AddLight("Flood B", root.transform, new Vector3(7.3f, 5f, -6.6f), LightType.Spot, Warm, 6f, 16f, 70f).transform.localRotation = Quaternion.Euler(50f, -40f, 0f);
        Save(root, "RepairPlatform");
    }

    // ---------------------------------------------------------------- Place

    static readonly Vector3[] BargeRoute =
    {
        new(2400f, 900f, -600f), new(3600f, 900f, -200f), new(3800f, 700f, 1000f), new(2700f, 800f, 600f),
    };

    [MenuItem("CatWalk/Temp/Place Places")]
    static void Place()
    {
        log = new StringBuilder("Place Places\n");
        try
        {
            Scene active = EditorSceneManager.GetActiveScene();
            if (active.name != "Prototype") throw new System.Exception("Открой сцену Prototype, сейчас: " + active.name);

            Put(active, "SortingHub", "Сортировочный узел", new Vector3(3000f, -300f, -1800f), 5f);
            Put(active, "Lighthouse", "Маяк на краю", new Vector3(-4600f, 800f, -200f), 0f);
            Put(active, "Galka", "Галька", new Vector3(-500f, 900f, 3600f), -5f);
            Put(active, "RepairPlatform", "Ремонт «Заплатка»", new Vector3(-3800f, 200f, -4600f), 0f);

            GameObject barge = Put(active, "FuelBarge", "Заправка-баржа «Бродяга»", BargeRoute[0], 0f, false);
            var mover = barge.GetComponent<FuelBarge>();
            Undo.RecordObject(mover, "Barge route");
            mover.route = (Vector3[])BargeRoute.Clone();
            PrefabUtility.RecordPrefabInstancePropertyModifications(mover);
            barge.transform.SetPositionAndRotation(BargeRoute[0], Quaternion.identity);

            // Заказы на Гальку выдают Конбини и исследовательская станция.
            AddOrder("konbini", "KonbiniToGalka_Tea");
            AddOrder("research", "ResearchToGalka_Bulbs");

            Distances();
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            log.AppendLine("сцена сохранена");
        }
        catch (System.Exception e)
        {
            log.AppendLine("ОШИБКА: " + e);
            Debug.LogException(e);
        }
        WriteLog();
    }

    // Найти по имени в корне сцены или поставить префаб; передом (+Z) от центра карты, парковкой (-Z) к нему.
    static GameObject Put(Scene active, string prefab, string name, Vector3 position, float tilt, bool face = true)
    {
        GameObject go = System.Array.Find(active.GetRootGameObjects(), g => g.name == name);
        if (go == null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(Load(Prefabs + prefab + ".prefab"), active);
            go.name = name;
            log.AppendLine("поставлено: " + name);
        }
        Vector3 away = new(position.x, 0f, position.z);
        Quaternion rotation = face && away.sqrMagnitude > 1f ? Quaternion.LookRotation(away.normalized) * Quaternion.Euler(0f, 0f, tilt) : Quaternion.identity;
        Undo.RecordObject(go.transform, "Place");
        go.transform.SetPositionAndRotation(position, rotation);
        log.AppendLine($"{name}: {position}");
        return go;
    }

    static void AddOrder(string address, string file)
    {
        var order = AssetDatabase.LoadAssetAtPath<DeliveryOrder>(Orders + file + ".asset");
        PostTerminal post = System.Array.Find(Object.FindObjectsByType<PostTerminal>(FindObjectsSortMode.None), t => t.address == address);
        if (order == null || post == null)
        {
            log.AppendLine($"не связано: {file} -> почта {address}");
            return;
        }
        if (System.Array.IndexOf(post.orders, order) >= 0) return;
        Undo.RecordObject(post, "Add order");
        var list = new List<DeliveryOrder>(post.orders) { order };
        post.orders = list.ToArray();
        PrefabUtility.RecordPrefabInstancePropertyModifications(post);
        log.AppendLine($"заказ {file} -> выдаёт почта {address}");
    }

    // Проверка расстановки: до ближайшей другой точки карты (у планет — до поверхности).
    static void Distances()
    {
        Location[] all = Object.FindObjectsByType<Location>(FindObjectsSortMode.None);
        foreach (string id in new[] { "sorting-hub", "lighthouse", "galka", "patch", "barge" })
        {
            Location me = System.Array.Find(all, l => l.id == id);
            if (me == null) { log.AppendLine("нет Location " + id); continue; }
            float best = float.MaxValue;
            string near = "";
            foreach (Location other in all)
            {
                if (other == me || other.transform.IsChildOf(me.transform)) continue;
                float d = Vector3.Distance(me.transform.position, other.transform.position) - (other.kind == Location.Kind.Planet ? other.radius : 0f);
                if (d < best) { best = d; near = other.Label; }
            }
            log.AppendLine($"{me.Label}: ближайшее — {near}, {best:0} м");
        }
    }
}
