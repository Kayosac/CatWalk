using System;
using System.Collections.Generic;
using System.IO;
using Febucci.TextAnimatorForUnity;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// ВРЕМЕННЫЙ: линия трикстера.
// Build — материалы и импорт моделей, модули, данные (профиль, заказ находки) и префабы; сцены не трогает.
// Place — в открытой сцене: авантюрист и точки встреч у жителей станций, руины, чёрная дыра у Края, улики.
// Оба идемпотентны: Build перезаписывает свои ассеты, Place ищет объекты по имени и создаёт недостающие (стоящие не двигает).
// Лог — Temp/trickster_build.txt. После проверки — удалить.
static class BuildTricksterTemp
{
    const string Models = "Assets/CatWalk/Content/Environment/Models/Prototype/";
    const string Materials = "Assets/CatWalk/Content/Environment/Materials/";
    const string Modules = "Assets/CatWalk/Content/Environment/Modules/Prototype/";
    const string Prefabs = "Assets/CatWalk/Prefabs/";
    const string Data = "Assets/CatWalk/Data/Trickster/";
    const string Fx = "Assets/Plugins/Retro Arsenal/Prefabs/";
    const string MenuWindow = "Assets/CatWalk/Prefabs/UI/MenuWindow.prefab";
    const string Log = "Temp/trickster_build.txt";

    static readonly Vector3 RuinsPosition = new Vector3(-2400f, 800f, -5000f); // ~5,6 км от Конбини, за Краем

    static Scene scene;

    // ---------------------------------------------------------------- Build

    [MenuItem("CatWalk/Temp/Build Trickster")]
    static void Build()
    {
        File.WriteAllText(Log, "Build Trickster " + DateTime.Now + "\n");
        try
        {
            BuildMaterials();
            scene = EditorSceneManager.NewPreviewScene();
            try
            {
                BuildModules();
                TricksterProfile profile = BuildProfile();
                DeliveryOrder order = BuildOrder();
                GameObject find = BuildFind(order);
                BuildTrickster(profile, find);
                BuildRuins();
                BuildBlackHole(profile);
                BuildNote();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.SaveAssets();
            Write("готово");
        }
        catch (Exception e)
        {
            Write("ОШИБКА: " + e);
            throw;
        }
    }

    static void Write(string line)
    {
        File.AppendAllText(Log, line + "\n");
        Debug.Log("Trickster: " + line);
    }

    static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new Exception("Нет ассета: " + path);
        return asset;
    }

    static GameObject Root(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    static GameObject Child(string name, Transform parent, Vector3 position = default)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        return go;
    }

    static GameObject Nested(string path, Transform parent, string name = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(path), parent);
        if (name != null) go.name = name;
        return go;
    }

    static Transform Descendant(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        throw new Exception($"Нет {name} в {root.name}");
    }

    static GameObject Save(GameObject go, string path)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path, out bool ok);
        Object.DestroyImmediate(go);
        Write((ok ? "ok    " : "FAIL  ") + path);
        return saved;
    }

    // Новые материалы пака: цвет и сила свечения (0 — без свечения). Шар дыры — Unlit, чтобы был чёрным без бликов.
    static readonly (string name, Color color, float glow)[] NewMaterials =
    {
        ("Prototype_TricksterFur", new Color(0.32f, 0.33f, 0.38f), 0f),
        ("Prototype_TricksterScarf", new Color(0.62f, 0.12f, 0.16f), 0f),
        ("Prototype_RuinsStone", new Color(0.42f, 0.40f, 0.45f), 0f),
        ("Prototype_RuinsGlow", new Color(0.55f, 0.35f, 0.95f), 1.5f),
        ("Prototype_TricksterFind", new Color(0.30f, 0.24f, 0.22f), 0f),
        ("Prototype_TricksterFindGlow", new Color(1.0f, 0.55f, 0.25f), 4f),
        ("Prototype_TricksterHole", Color.black, 0f),
        ("Prototype_TricksterHoleRing", new Color(1.0f, 0.5f, 0.85f), 6f),
    };

    static void BuildMaterials()
    {
        foreach (var (name, color, glow) in NewMaterials)
        {
            string path = Materials + name + ".mat";
            bool unlit = name == "Prototype_TricksterHole";
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            if (!unlit) m.SetFloat("_Smoothness", 0.25f);
            if (glow > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * glow);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            Write("ok    " + path);
        }
        AssetDatabase.SaveAssets();
    }

    // FBX: материалы — только ссылки на общие из Environment/Materials, без своих копий.
    static GameObject Model(string name, params string[] materials)
    {
        string path = Models + name + ".fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new Exception("Нет модели: " + path);
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        foreach (string m in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m), Load<Material>(Materials + m + ".mat"));
        importer.SaveAndReimport();
        return Load<GameObject>(path);
    }

    static GameObject Module(GameObject model, string name, bool colliders)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        go.name = name;
        if (colliders)
            foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
                f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;
        return Save(go, Modules + name + ".prefab");
    }

    static void BuildModules()
    {
        GameObject gear = Model("TricksterGear", "Prototype_TricksterScarf", "Prototype_Dark", "Prototype_Rust", "Prototype_Teal");
        Module(Model("Ruins", "Prototype_RuinsStone", "Prototype_RuinsGlow"), "Ruins", true);
        Module(Model("TricksterFind", "Prototype_TricksterFind", "Prototype_TricksterFindGlow"), "TricksterFind", false);
        Module(Model("TricksterHole", "Prototype_TricksterHole", "Prototype_TricksterHoleRing"), "TricksterHole", false);

        var note = (GameObject)PrefabUtility.InstantiatePrefab(Model("TricksterNote", "Prototype_Rust", "Prototype_Cream", "Prototype_TricksterScarf"), scene);
        note.name = "TricksterNote";
        var box = note.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.65f, 0f);
        box.size = new Vector3(0.26f, 1.3f, 0.08f);
        Save(note, Modules + "TricksterNote.prefab");

        // Авантюрист — вариант жителя: шерсть другого цвета, без шапок, шарф, очки и сумка.
        var cat = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Modules + "ResidentCat.prefab"), scene);
        cat.name = "TricksterCat";
        foreach (Transform t in cat.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Hat_")) t.gameObject.SetActive(false);
        var body = Descendant(cat.transform, "Cat").GetComponent<Renderer>();
        Material[] mats = body.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null && mats[i].name == "Prototype_Fur") mats[i] = Load<Material>(Materials + "Prototype_TricksterFur.mat");
        body.sharedMaterials = mats;
        ((GameObject)PrefabUtility.InstantiatePrefab(gear, cat.transform)).name = "Gear";
        Save(cat, Modules + "TricksterCat.prefab");
    }

    // ---------------------------------------------------------------- Данные

    static T Asset<T>(string file) where T : ScriptableObject
    {
        if (!AssetDatabase.IsValidFolder("Assets/CatWalk/Data/Trickster")) AssetDatabase.CreateFolder("Assets/CatWalk/Data", "Trickster");
        string path = Data + file + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        EditorUtility.SetDirty(asset);
        Write("ok    " + path);
        return asset;
    }

    static FactCondition AtLeast(string fact, int value) => new FactCondition { fact = fact, compare = FactCondition.Compare.AtLeast, value = value };

    static DeliveryOrder BuildOrder()
    {
        DeliveryOrder order = Asset<DeliveryOrder>("TricksterFind");
        order.cargo = "Находка из руин (тёплая)";
        order.to = "trickster";                 // у авантюриста нет почты: сдаётся ему в руки (Trickster.hand)
        order.reward = 0;
        order.fragile = false;
        return order;
    }

    // Тексты временные — в тоне доски; мотив авантюриста и природа находки открыты.
    static TricksterProfile BuildProfile()
    {
        TricksterProfile p = Asset<TricksterProfile>("Trickster");
        p.displayName = "Авантюрист";
        p.meetings = new[]
        {
            new TricksterProfile.Meeting
            {
                at = "konbini", trust = 1, conditions = new[] { AtLeast("delivered", 2) },
                lines = new[]
                {
                    "О! Курьер. Настоящий, с коробкой и усталыми ушами. Я думал, вас уже не делают.",
                    "Я так, путешествую. Ищу то, что потеряли до нас. Звучит красиво, платят плохо. Пока.",
                    "По пыли на хвосте вижу: летаешь далеко. Ещё увидимся, курьер.",
                },
            },
            new TricksterProfile.Meeting
            {
                at = "fuelstop04", trust = 1, conditions = new[] { AtLeast("delivered", 5) },
                lines = new[]
                {
                    "Опять ты! Это судьба или маршрут? По-моему, маршрут — он надёжнее.",
                    "Сколько тебе платят за коробку? Не говори. По глазам вижу: мало. Обидно мало.",
                    "Есть дела, где за один рейс платят как за год. Я не уговариваю. Я просто вслух думаю.",
                },
            },
            new TricksterProfile.Meeting
            {
                at = "research", trust = 2, conditions = new[] { AtLeast("debt.paid", 1) },
                lines = new[]
                {
                    "Учёные тут хорошие. Скучные, но хорошие. Показал им чертёж — так долго молчали, что я обиделся.",
                    "Знаешь, что лежит на дальней окраине? Древнее. Старше станций, старше почты. Может, старше скуки.",
                    "Если это то, что я думаю, — это открытие. Имя в учебниках. И деньги. Деньги тоже в учебниках, на последней странице.",
                    "Найди меня на Краю, у заправки 06. Там поговорим без лишних ушей.",
                },
            },
            new TricksterProfile.Meeting
            {
                at = "fuelstop06", trust = 1, offer = true, conditions = new FactCondition[0],
                lines = new[]
                {
                    "Прилетел. Я знал. То есть надеялся. То есть знал.",
                    "Руины на дальней окраине. Там лежит одна вещь — увидишь, сразу поймёшь, что она.",
                    "Привези её мне. Деньги — честно пополам. Ну, почти пополам. Ну, тебе хватит.",
                    "Координаты уже у тебя на карте. Лети осторожно: там тихо, как в выключенном радио.",
                },
            },
        };
        p.idle = new[]
        {
            "Я тут проездом. Я везде проездом.",
            "Погода сегодня хорошая. В космосе она всегда одинаковая, но я всё равно замечаю.",
            "Береги машину. Она у тебя одна. Как и всё хорошее.",
        };
        p.waiting = "Ну как, руины? Я тут, никуда не денусь. Это моё лучшее качество.";
        p.handover = "Тёплая... Так и должно быть. Наверное. Спасибо, курьер. Насчёт денег — свяжусь завтра, честное слово. Лети, отдыхай.";
        p.afterHandover = "Завтра, курьер. Всё завтра. Лети.";
        p.messages = new[]
        {
            new TricksterProfile.Message { at = 0f, text = "Позади, у Края, — беззвучная вспышка." },
            new TricksterProfile.Message { at = 8f, text = "Почта, общий канал: всем курьерам у Края — уходите. Груз можно бросить." },
            new TricksterProfile.Message { at = 20f, text = "Мама: По новостям что-то про Край. Ты же не там? Перезвони, как сможешь." },
            new TricksterProfile.Message { at = 32f, text = "Кредиторы: Платёж в срок. Природные явления уважительной причиной не считаются." },
            new TricksterProfile.Message { at = 45f, text = "Почта: Курьер, ваш маяк дрейфует. Ответьте." },
            new TricksterProfile.Message { at = 58f, text = "Папа: Мы тут, у телефона. Мы никуда не уходим." },
            new TricksterProfile.Message { at = 72f, text = "Почта: ...курьер... сигнал... (помехи)" },
            new TricksterProfile.Message { at = 85f, text = "...ш-ш-ш..." },
        };
        return p;
    }

    // ---------------------------------------------------------------- Префабы

    // Находка — посылка (вариант Parcel): вместо коробки «яйцо» со светящимися швами, искры и тёплый свет.
    static GameObject BuildFind(DeliveryOrder order)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Prefabs + "Parcel.prefab"), scene);
        root.name = "TricksterFind";
        Descendant(root.transform, "Model").gameObject.SetActive(false);
        Nested(Modules + "TricksterFind.prefab", root.transform, "Find model");
        GameObject sparkle = Nested(Fx + "Environment/Sparkle/Mini/SparkleMiniYellowLoop.prefab", root.transform, "Sparkle");
        sparkle.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        sparkle.transform.localScale = Vector3.one * 0.4f;
        var light = Child("Warm light", root.transform, new Vector3(0f, 0.3f, 0f)).AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.3f);
        light.range = 3f;
        light.intensity = 2f;
        light.shadows = LightShadows.None;
        root.GetComponent<Parcel>().order = order;
        return Save(root, Prefabs + "TricksterFind.prefab");
    }

    // Авантюрист: модель-вариант жителя, облачко и «!» — из Resident.prefab, слот для находки перед ним.
    static void BuildTrickster(TricksterProfile profile, GameObject find)
    {
        GameObject root = Root("Trickster");
        GameObject body = Child("Body", root.transform);
        GameObject visual = Nested(Modules + "TricksterCat.prefab", body.transform, "Visual");

        var resident = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Prefabs + "Resident.prefab"), scene);
        PrefabUtility.UnpackPrefabInstance(resident, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        Transform bubble = resident.transform.Find("Bubble"), news = resident.transform.Find("News");
        bubble.SetParent(body.transform, false);
        news.SetParent(body.transform, false);
        Object.DestroyImmediate(resident);

        GameObject hand = Child("Hand", body.transform, new Vector3(0.6f, 0f, 0.9f));
        hand.AddComponent<ParcelSlot>();
        hand.SetActive(false);

        var t = root.AddComponent<Trickster>();
        t.profile = profile;
        t.body = body;
        t.visual = visual.transform;
        t.findPrefab = find.GetComponent<Parcel>();
        t.hand = hand.GetComponent<ParcelSlot>();
        t.bubble = bubble.GetComponent<Canvas>();
        t.text = bubble.GetComponentInChildren<TMP_Text>(true);
        t.typewriter = bubble.GetComponentInChildren<TypewriterComponent>(true);
        t.newsIcon = news.gameObject;
        Save(root, Prefabs + "Trickster.prefab");
    }

    // Руины: модуль, парковка перед алтарём, гравитация над плитой, слот находки на алтаре. На карте (Location) — выключены.
    static void BuildRuins()
    {
        GameObject root = Root("Ruins");
        Nested(Modules + "Ruins.prefab", root.transform, "Model");
        Nested(Prefabs + "Parking.prefab", root.transform).transform.localPosition = new Vector3(0f, 0.05f, 5f);
        var gravity = Child("Gravity", root.transform).AddComponent<PlaneGravity>();
        gravity.size = new Vector3(36f, 12f, 36f);
        Child("Find slot", root.transform, new Vector3(0f, 1f, -9f)).AddComponent<ParcelSlot>();
        GameObject arrival = Child("Arrival", root.transform, new Vector3(0f, 20f, 60f));

        var location = root.AddComponent<Location>();
        location.id = "ruins";
        location.title = "Руины";
        location.kind = Location.Kind.Find;
        location.radius = 40f;
        location.arrival = arrival.transform;
        location.enabled = false;
        Save(root, Prefabs + "Ruins.prefab");
    }

    // Чёрная дыра: шар с кольцами и диском (масштаб — радиус), взрыв, свет пустоты и её меню.
    static void BuildBlackHole(TricksterProfile profile)
    {
        GameObject root = Root("BlackHole");
        GameObject visual = Child("Visual", root.transform);
        Nested(Modules + "TricksterHole.prefab", visual.transform, "Model");
        Nested(Fx + "Interactive/Portal/PortalPurple.prefab", visual.transform, "Disk").transform.localScale = Vector3.one * 0.6f;
        visual.SetActive(false);

        GameObject explosion = Child("Explosion", root.transform);
        Nested(Fx + "Combat/Explosions/Nuke/NukeExplosionPurple.prefab", explosion.transform).transform.localScale = Vector3.one * 40f;
        Nested(Fx + "Combat/Explosions/BlackHole/BlackHoleExplosionPurple.prefab", explosion.transform).transform.localScale = Vector3.one * 60f;
        explosion.SetActive(false);

        GameObject voidLight = Child("Void light", root.transform);
        voidLight.transform.localRotation = Quaternion.Euler(40f, -30f, 0f);
        var light = voidLight.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.7f, 0.75f, 1f);
        light.intensity = 0.35f;
        light.shadows = LightShadows.None;
        voidLight.SetActive(false);

        var hole = root.AddComponent<BlackHole>();
        hole.profile = profile;
        hole.visual = visual.transform;
        hole.explosion = explosion;
        hole.voidLight = voidLight;
        hole.menu = Menu<VoidMenu>(root.transform, "Void menu", "Esc — продолжить скитаться");
        Save(root, Prefabs + "BlackHole.prefab");
    }

    // Окно меню из MenuWindow.prefab со своим компонентом; останавливает время, E его не закрывает.
    static T Menu<T>(Transform parent, string name, string hint) where T : MenuScreen
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(MenuWindow), parent);
        go.name = name;
        T menu = go.AddComponent<T>();
        Transform panel = go.transform.Find("Panel");
        menu.title = panel.Find("Title").GetComponent<TMP_Text>();
        menu.status = panel.Find("Status").GetComponent<TMP_Text>();
        menu.list = panel.Find("List");
        menu.rowTemplate = panel.Find("Row").GetComponent<Button>();
        menu.pauseTime = true;
        menu.closeOnE = false;
        Transform hintText = panel.Find("Hint");
        if (hintText != null) hintText.GetComponent<TMP_Text>().text = hint;
        go.SetActive(false);
        return menu;
    }

    // Записка у почты — улика, читается по F.
    static void BuildNote()
    {
        GameObject root = Root("TricksterNote");
        Nested(Modules + "TricksterNote.prefab", root.transform, "Model");
        var clue = root.AddComponent<TricksterClue>();
        clue.id = "note";
        clue.needKey = true;
        clue.radius = 2.2f;
        clue.conditions = new FactCondition[0];
        clue.text = "Записка без подписи: «Если тот, в шарфе, попросит что-то привезти — не вози. Попросит отдать — тем более».";
        Save(root, Prefabs + "TricksterNote.prefab");
    }

    // ---------------------------------------------------------------- Place

    [MenuItem("CatWalk/Temp/Place Trickster")]
    static void Place()
    {
        File.AppendAllText(Log, "Place Trickster " + DateTime.Now + "\n");
        Scene active = EditorSceneManager.GetActiveScene();
        Location[] locations = Object.FindObjectsByType<Location>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Resident[] residents = Object.FindObjectsByType<Resident>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PostTerminal[] posts = Object.FindObjectsByType<PostTerminal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Location Loc(string id) => Array.Find(locations, l => l.id == id);
        var profile = Load<TricksterProfile>(Data + "Trickster.asset");

        // Точки встреч: на 4,5 м правее жителя станции, лицом туда же (дальше, чем оба говорят по F).
        GameObject spotsRoot = SceneRoot(active, "Trickster spots");
        var spots = new List<Transform>();
        foreach (TricksterProfile.Meeting m in profile.meetings)
        {
            if (spots.Exists(s => s.name == m.at)) continue;
            Location loc = Loc(m.at);
            if (loc == null)
            {
                Write("ОШИБКА: нет локации " + m.at);
                continue;
            }
            Transform spot = spotsRoot.transform.Find(m.at);
            if (spot == null)
            {
                spot = new GameObject(m.at).transform;
                spot.SetParent(spotsRoot.transform, false);
                Resident r = Nearest(residents, loc.transform.position);
                if (r != null) spot.SetPositionAndRotation(r.transform.position + r.transform.right * 4.5f, r.transform.rotation);
                else spot.SetPositionAndRotation(loc.transform.position, loc.transform.rotation);
                Write($"точка {m.at}: {spot.position} (житель {(r != null ? r.name : "не найден")})");
            }
            spots.Add(spot);
        }
        Transform last = spots.Count > 0 ? spots[^1] : null;

        // Руины — передом к Краю.
        Location edge = Loc("fuelstop06");
        GameObject ruins = SceneInstance(active, "Ruins", RuinsPosition,
            Quaternion.LookRotation(Vector3.ProjectOnPlane((edge != null ? edge.transform.position : Vector3.zero) - RuinsPosition, Vector3.up), Vector3.up));

        GameObject trickster = SceneInstance(active, "Trickster", last != null ? last.position : Vector3.zero, Quaternion.identity);
        var t = trickster.GetComponent<Trickster>();
        t.spots = spots.ToArray();
        t.ruins = ruins.GetComponent<Location>();
        t.findSlot = ruins.transform.Find("Find slot").GetComponent<ParcelSlot>();
        EditorUtility.SetDirty(t);

        // Дыра — там, где авантюрист принимает находку.
        SceneInstance(active, "BlackHole", last != null ? last.position : Vector3.zero, Quaternion.identity);

        // Улики.
        GameObject clues = SceneRoot(active, "Trickster clues");
        PostTerminal edgePost = edge != null ? Nearest(posts, edge.transform.position) : null;
        if (edgePost != null && clues.transform.Find("TricksterNote") == null)
        {
            Transform p = edgePost.transform;
            var note = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Prefabs + "TricksterNote.prefab"), clues.transform);
            note.transform.SetPositionAndRotation(p.position + p.right * 1.2f - p.forward * 0.3f, p.rotation * Quaternion.Euler(0f, 180f, 0f));
            Write("записка у почты " + edgePost.address + ": " + note.transform.position);
        }

        Location signal = Loc("signal-1");
        if (signal != null) Clue(clues.transform, "signal", signal.transform.position, true, 80f, 5f, new[] { AtLeast("found.signal-1", 1) },
            "В хвосте записи — второй голос, тише: «...не бери тёплое. Если оно тёплое — не бери...» Дальше шум.");

        Location lab = Loc("research");
        Resident labResident = lab != null ? Nearest(residents, lab.transform.position) : null;
        if (labResident != null)
        {
            string name = labResident.profile != null ? labResident.profile.displayName : "Житель";
            Clue(clues.transform, "slip", labResident.transform.position, false, 5f, 1.5f, new[] { AtLeast("trickster.met", 2) },
                $"{name} (вполголоса, не тебе): «Опять этот, в шарфе, про руины расспрашивал. Прошлого, кто с ним летал, тут больше не видно».");
        }

        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);
        Write("Place: готово");
    }

    static T Nearest<T>(T[] all, Vector3 position, float max = 400f) where T : Component
    {
        T best = null;
        float bestDistance = max;
        foreach (T c in all)
        {
            float d = Vector3.Distance(c.transform.position, position);
            if (d < bestDistance) { best = c; bestDistance = d; }
        }
        return best;
    }

    static GameObject SceneRoot(Scene s, string name)
    {
        GameObject go = Array.Find(s.GetRootGameObjects(), g => g.name == name);
        if (go != null) return go;
        go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, s);
        Write("создан " + name);
        return go;
    }

    // Экземпляр префаба корнем сцены: уже стоящий не двигается.
    static GameObject SceneInstance(Scene s, string name, Vector3 position, Quaternion rotation)
    {
        GameObject go = Array.Find(s.GetRootGameObjects(), g => g.name == name);
        if (go != null) return go;
        go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Prefabs + name + ".prefab"), s);
        go.transform.SetPositionAndRotation(position, rotation);
        Write($"создан {name}: {position}");
        return go;
    }

    static void Clue(Transform parent, string id, Vector3 position, bool byCar, float radius, float delay, FactCondition[] conditions, string text)
    {
        string name = "Clue " + id;
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<TricksterClue>();
            Write($"улика {id}: {position}");
        }
        var clue = go.GetComponent<TricksterClue>();
        clue.id = id;
        clue.byCar = byCar;
        clue.radius = radius;
        clue.delay = delay;
        clue.conditions = conditions;
        clue.text = text;
        EditorUtility.SetDirty(clue);
    }
}
