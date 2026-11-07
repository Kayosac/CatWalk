using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ВРЕМЕННЫЙ: собирает префабы жителя и находок и профили четырёх жителей. Идемпотентен, сцены не трогает.
// После проверки — удалить.
static class BuildResidentsTemp
{
    const string Prefabs = "Assets/CatWalk/Prefabs/";
    const string Modules = "Assets/CatWalk/Content/Environment/Modules/Prototype/";
    const string Fx = "Assets/Plugins/Retro Arsenal/Prefabs/";
    const string Data = "Assets/CatWalk/Data/Residents/";
    const string Orders = "Assets/CatWalk/Data/Delivery/";
    const string Font = "Assets/CatWalk/Fonts/Resources/Fonts & Materials/Monocraft.asset";
    const string TextAnimator = "Assets/Plugins/Febucci/Text Animator for Unity/";

    static Scene scene;

    [MenuItem("CatWalk/Temp/Build Residents")]
    static void Build()
    {
        scene = EditorSceneManager.NewPreviewScene();
        try
        {
            BuildResident();
            BuildFind("Debris", Find.Kind.Debris, "Обломки", 6f, 170, 18f, "",
                Fx + "Environment/Sparkle/Mini/SparkleMiniYellowLoop.prefab", 3f, 4f);
            BuildFind("SignalBeacon", Find.Kind.Signal, "Сигнал", 3f, 240, 0f,
                "Запись на повторе: «...если кто-то слышит — тут очень красивые звёзды. Всё. Просто хотел сказать».",
                Fx + "Interactive/Loot/Obtainables/Lootray/Standard/LootRayBlue.prefab", 2f, 2.5f);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
        BuildProfiles();
        AssetDatabase.SaveAssets();
        Debug.Log("Build Residents: готово");
    }

    static GameObject Load(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new System.Exception("Нет ассета: " + path);
        return asset;
    }

    static GameObject Nested(string path, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Load(path), scene);
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject Root(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    static GameObject UiChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Save(GameObject root, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(root, Prefabs + name + ".prefab");
        Object.DestroyImmediate(root);
    }

    // Житель: модель-модуль, облачко реплики (world-space Canvas с печатью Text Animator) и «!» из Retro Arsenal.
    static void BuildResident()
    {
        GameObject root = Root("Resident");
        GameObject visual = Nested(Modules + "ResidentCat.prefab", root.transform);

        GameObject bubbleGo = UiChild("Bubble", root.transform);
        var bubbleRect = (RectTransform)bubbleGo.transform;
        bubbleRect.localPosition = new Vector3(0f, 1.9f, 0f);
        bubbleRect.localScale = Vector3.one * 0.01f;
        bubbleRect.pivot = new Vector2(0.5f, 0f);
        bubbleRect.sizeDelta = new Vector2(260f, 120f);
        var canvas = bubbleGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        GameObject panel = UiChild("Panel", bubbleGo.transform);
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(260f, 0f);
        var image = panel.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.07f, 0.07f, 0.12f, 0.85f);
        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textGo = UiChild("Text", panel.transform);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Font);
        if (font != null) text.font = font;
        text.fontSize = 20f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.text = "";
        textGo.AddComponent<TextAnimator_TMP>();
        var typewriter = textGo.AddComponent<TypewriterComponent>();
        typewriter.TimingSettings = AssetDatabase.LoadAssetAtPath<TypingsTimingsScriptableBase>(TextAnimator + "Timings/Delays By Character.asset");
        typewriter.sharedSettings = AssetDatabase.LoadAssetAtPath<TypewriterSettingsScriptable>(TextAnimator + "Settings/Shared Typewriter Settings.asset");

        GameObject news = Nested(Fx + "Interactive/Status Icons/Exclamation.prefab", root.transform);
        news.name = "News";
        news.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        news.SetActive(false);

        var resident = root.AddComponent<Resident>();
        resident.visual = visual.transform;
        resident.bubble = canvas;
        resident.text = text;
        resident.typewriter = typewriter;
        resident.newsIcon = news;
        Save(root, "Resident");
    }

    // Находка: модуль, Location kind Find, маяк и эффект сканирования из Retro Arsenal.
    static void BuildFind(string name, Find.Kind kind, string title, float radius, int credits, float fuel, string record,
        string beaconFx, float beaconScale, float scanScale)
    {
        GameObject root = Root(name);
        Nested(Modules + name + ".prefab", root.transform);

        var location = root.AddComponent<Location>();
        location.id = name.ToLowerInvariant();   // в сцене каждой находке — свой id
        location.title = title;
        location.kind = Location.Kind.Find;
        location.radius = radius;

        GameObject beacon = Nested(beaconFx, root.transform);
        beacon.name = "Beacon";
        beacon.transform.localScale = Vector3.one * beaconScale;
        GameObject scan = Nested(Fx + "Interactive/Zone/Hack/HackZoneBlue.prefab", root.transform);
        scan.name = "Scan";
        scan.transform.localScale = Vector3.one * scanScale;
        scan.SetActive(false);

        var find = root.AddComponent<Find>();
        find.kind = kind;
        find.credits = credits;
        find.fuel = fuel;
        find.record = record;
        find.beacon = beacon;
        find.scanVfx = scan;
        Save(root, name);
    }

    static ResidentProfile Profile(string file)
    {
        if (!AssetDatabase.IsValidFolder("Assets/CatWalk/Data/Residents")) AssetDatabase.CreateFolder("Assets/CatWalk/Data", "Residents");
        string path = Data + file + ".asset";
        var profile = AssetDatabase.LoadAssetAtPath<ResidentProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<ResidentProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static ResidentProfile.CargoReaction React(string order, string line)
    {
        var asset = AssetDatabase.LoadAssetAtPath<DeliveryOrder>(Orders + order + ".asset");
        if (asset == null) Debug.LogWarning("Нет заказа: " + order);
        return new ResidentProfile.CargoReaction { order = asset, line = line };
    }

    // Первое событие — после первой доставки сюда, второе — после третьей и только после первого.
    static ResidentProfile.StoryEvent[] Story(string id, string first, string second) => new[]
    {
        new ResidentProfile.StoryEvent { id = id + "-1", minDeliveries = 1, after = "", line = first },
        new ResidentProfile.StoryEvent { id = id + "-2", minDeliveries = 3, after = id + "-1", line = second },
    };

    static void BuildProfiles()
    {
        // Почта станции Конбини (konbini), почтовая форма.
        ResidentProfile nika = Profile("Nika");
        nika.displayName = "Ника";
        nika.greeting = "Ой, новый курьер! Я Ника, тут почта. И чайник. Чайник главнее, но ты ему не говори.";
        nika.greetingKnown = "О, это ты. Чайник как раз вскипел. Совпадение: он у меня кипит постоянно.";
        nika.greetingWarm = "Мой любимый курьер! Только другим курьерам не говори. Их, правда, нет, но всё равно.";
        nika.topics = new[]
        {
            "Чайник третий год свистит в ре миноре. Я уже подпеваю. По-моему, он тоже скучает.",
            "Письма тут пишут заранее: пока долетят, все уже помирятся. Так что пишут сразу «мир».",
            "По ночам смотрю на огни кораблей. Ночь у нас круглосуточная, так что смотрю много.",
            "Форму выдали на вырост. Я расту в ширину, форма — в ответственность. Пока ничья.",
        };
        nika.thanks = "Получено! Ставлю штамп. Штамп в виде лапы — других нет, да и не надо.";
        nika.reactions = new[]
        {
            React("FuelStopToKonbini_Seals", "Уплотнители? Заткну ими чайник. Шучу, отдам на склад. Чайник неприкасаем."),
        };
        nika.damagedRemark = "Коробка помятая... Ничего, я по утрам тоже такая. Главное — внутри цело.";
        nika.story = Story("nika",
            "До тебя почту возил автомат. Он не здоровался. Я с ним всё равно здоровалась. Два года.",
            "Я завела журнал «Сегодня прилетал курьер». Там уже три записи. Лучший журнал на станции.");

        // Заправка 04 (fuelstop04), рабочая каска.
        ResidentProfile ira = Profile("Ira");
        ira.displayName = "Ира";
        ira.greeting = "Стой! Шучу, двигайся. Я Ира, заправка моя. Каску не снимаю — она меня тоже.";
        ira.greetingKnown = "А, курьер. Бак не течёт? Хвост не в мазуте? Ну и славно.";
        ira.greetingWarm = "Мой постоянный клиент! Постоянных у меня ровно один. Каска рада, я тоже.";
        ira.topics = new[]
        {
            "Насос с характером: по вторникам качает только в обратную сторону. Сегодня вроде не вторник.",
            "Проверяй бак до вылета. После вылета тоже можно, но уже нервно.",
            "Ловим одну радиостанцию. Там тридцать лет играет одна песня. Я выучила слова. Слов там нет.",
            "Каска — от падающих гаечных ключей. Моих. Я их роняю с большим профессионализмом.",
        };
        ira.thanks = "Принято. Распишись лапой вот тут. Нет, другой. Ладно, любой.";
        ira.reactions = new[]
        {
            React("KonbiniToFuelStop_Letters", "Письма! Целая пачка! Буду читать по одному в неделю, чтоб надолго хватило."),
            React("KonbiniToFuelStop_Noodles", "Лапша! Заварю на выхлопе насоса. Шучу. Наверное. Спасибо!"),
        };
        ira.damagedRemark = "Коробка битая. Не страшно, я тут всё чиню. Кроме коробок. Коробки — это ты.";
        ira.story = Story("ira",
            "Заправку я выиграла в лотерею. Билет был на пылесос. До сих пор не уверена, что выиграла.",
            "Вчера по радио заиграла другая песня. Я так испугалась, что выключила. Потом весь день скучала.");

        // Посёлок на планете (mira), садовая шляпа.
        ResidentProfile mei = Profile("Mei");
        mei.displayName = "Мэй";
        mei.greeting = "Здравствуй! Я Мэй. Осторожно, рассада. И тут рассада. И вон там тоже.";
        mei.greetingKnown = "Снова ты! Рассада тебя запомнила: шуршит, когда ты рядом. Или это ветер.";
        mei.greetingWarm = "Ты почти местный! Посажу тебе помидор. Будет расти и ждать доставок.";
        mei.topics = new[]
        {
            "Рассада растёт медленно, зато честно. Я с ней разговариваю. Она молчит, но слушает.",
            "Поливаю по утрам. Утро тут когда захочу: планета маленькая, можно дойти до солнца.",
            "Новости посёлка: кабачок вырос размером с соседа. Сосед гордится. Кабачок тоже.",
            "Шляпа — от солнца. Солнце далеко, но пусть знает, что я подготовилась.",
        };
        mei.thanks = "Ой, спасибо! Положу на полку к рассаде. Пусть знает, что нам кто-то пишет.";
        mei.reactions = new ResidentProfile.CargoReaction[0];
        mei.damagedRemark = "Помялась немножко. Ничего, отлежится. У меня так огурцы выпрямляются.";
        mei.story = Story("mei",
            "Когда я прилетела, тут был один камень. Теперь камень и грядка. Прогресс!",
            "До соседей за горизонтом сорок шагов. Иногда хожу туда просто помахать. Они машут в ответ.");

        // Исследовательская станция (research).
        ResidentProfile tim = Profile("Tim");
        tim.displayName = "Тим";
        tim.greeting = "А? Курьер? Я Тим, смотрю в прибор. Прибор смотрит в пустоту. Пустота пока молчит.";
        tim.greetingKnown = "О, курьер. Прибор говорит, что ты пришёл. Я тоже вижу, но приятно, что мы согласны.";
        tim.greetingWarm = "Мой любимый внешний раздражитель! Это комплимент. Учёные так хвалят.";
        tim.topics = new[]
        {
            "Приборы показывают ноль. Ноль — тоже результат. Очень, очень стабильный результат.",
            "Поймал странный сигнал: «пип... пип». Оказалось, микроволновка. Записал как открытие.",
            "Кофе варю на реакторе. Вкус радикальный. Сплю раз в неделю, зато основательно.",
            "Если пустота смотрит в тебя — помаши. Я машу. В отчёты это не пишу.",
        };
        tim.thanks = "Спасибо! Запишу: «Доставок: +1. Пустота: всё ещё пустая». Хороший день.";
        tim.reactions = new ResidentProfile.CargoReaction[0];
        tim.damagedRemark = "Коробка деформирована. Научно говоря — помята. Ненаучно — эх.";
        tim.story = Story("tim",
            "Я тут один уже год. В журнале пишу «мы», чтобы солиднее. Теперь «мы» — это правда.",
            "Тот сигнал из пустоты опять пришёл. Похож на мурчание. Найдёшь источник — передай привет.");
    }
}
