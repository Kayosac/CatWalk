using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ВРЕМЕННЫЙ. Build Errands: поручения жителей и их посылки, вехи и их заказы, состояния и реакции в профилях,
// Milestones + Errands + меню поручения в Game.prefab, материалы и модули декораций. Сцены не трогает.
// Place Errands: декорации-по-фактам (FactToggle) у Мэй, Иры и Бруно в открытой сцене.
// Оба идемпотентны, лог — Temp/errands_build.txt. После проверки — удалить.
static class BuildErrandsTemp
{
    const string Data = "Assets/CatWalk/Data/";
    const string ErrandsDir = Data + "Residents/Errands/";
    const string StoryDir = Data + "Story/";
    const string OrdersDir = Data + "Delivery/";
    const string GamePrefab = "Assets/CatWalk/Prefabs/Game.prefab";
    const string MenuWindowPrefab = "Assets/CatWalk/Prefabs/UI/MenuWindow.prefab";
    const string Materials = "Assets/CatWalk/Content/Environment/Materials/";
    const string Modules = "Assets/CatWalk/Content/Environment/Modules/Prototype/";
    const string LogPath = "Temp/errands_build.txt";

    static readonly List<string> log = new();
    static Scene preview;

    // ---------- Build ----------

    [MenuItem("CatWalk/Temp/Build Errands")]
    static void Build()
    {
        log.Clear();
        try
        {
            Folder(Data, "Story");
            Folder(Data + "Residents/", "Errands");
            Folder("Assets/CatWalk/Content/Environment/Modules/", "Prototype");
            BuildErrands();
            Milestone[] milestones = BuildMilestones();
            BuildGame(milestones);
            BuildModules();
            AssetDatabase.SaveAssets();
            Log("Build Errands: готово");
        }
        catch (System.Exception e)
        {
            Log("ОШИБКА: " + e);
            Debug.LogException(e);
        }
        File.WriteAllLines(LogPath, log);
    }

    static void Log(string line)
    {
        log.Add(line);
        Debug.Log("BuildErrandsTemp: " + line);
    }

    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + name)) AssetDatabase.CreateFolder(parent.TrimEnd('/'), name);
    }

    static T Asset<T>(string path) where T : ScriptableObject
    {
        T a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(a, path);
        Log("создан " + path);
        return a;
    }

    static FactCondition At(string fact, int value = 1) => new FactCondition { fact = fact, compare = FactCondition.Compare.AtLeast, value = value };
    static FactCondition Below(string fact, int value) => new FactCondition { fact = fact, compare = FactCondition.Compare.Below, value = value };

    static DeliveryOrder Order(string path, string cargo, string to, int reward, bool fragile, params FactCondition[] requires)
    {
        DeliveryOrder o = Asset<DeliveryOrder>(path);
        o.cargo = cargo;
        o.to = to;
        o.reward = reward;
        o.fragile = fragile;
        o.requires = requires;
        EditorUtility.SetDirty(o);
        return o;
    }

    static ResidentProfile Profile(string name)
    {
        var p = AssetDatabase.LoadAssetAtPath<ResidentProfile>(Data + "Residents/" + name + ".asset");
        if (p == null) throw new System.Exception("нет профиля " + name);
        return p;
    }

    // Поручение и его посылка: посылка видна в меню почты, пока поручение взято и не выполнено.
    static Errand MakeErrand(string asset, string id, Errand.Legality legality, string cargo, string to, int reward, bool fragile,
        int heat, float pressure, FactCondition[] requires, string offer, string accepted, string refused, string done)
    {
        string fact = "errand." + id;
        DeliveryOrder order = Order(ErrandsDir + "ErrandOrder_" + asset + ".asset", cargo, to, reward, fragile, At(fact, 1), Below(fact, 2));
        Errand e = Asset<Errand>(ErrandsDir + "Errand_" + asset + ".asset");
        e.id = id;
        e.legality = legality;
        e.order = order;
        e.requires = requires;
        e.offer = offer;
        e.accepted = accepted;
        e.refused = refused;
        e.done = done;
        e.heat = heat;
        e.pressure = pressure;
        EditorUtility.SetDirty(e);
        return e;
    }

    static ResidentProfile.Mood Mood(FactCondition[] when, string greeting, params string[] topics) =>
        new ResidentProfile.Mood { when = when, greeting = greeting, topics = topics };

    static void React(ResidentProfile p, DeliveryOrder order, string line)
    {
        var list = new List<ResidentProfile.CargoReaction>(p.reactions ?? new ResidentProfile.CargoReaction[0]);
        ResidentProfile.CargoReaction r = list.Find(x => x != null && x.order == order);
        if (r == null) list.Add(r = new ResidentProfile.CargoReaction { order = order });
        r.line = line;
        p.reactions = list.ToArray();
        EditorUtility.SetDirty(p);
    }

    // Тексты временные, в тоне нарративной доски: бытовая сатира и абсурд.
    static void BuildErrands()
    {
        ResidentProfile mei = Profile("Mei"), ira = Profile("Ira"), bruno = Profile("Bruno"),
            nika = Profile("Nika"), tim = Profile("Tim"), saya = Profile("Saya");

        // Мэй: обычная цепочка, меняет её жизнь — огород разрастается (декорация в Place).
        Errand meiDill = MakeErrand("MeiDill", "mei-dill", Errand.Legality.Ordinary, "Укроп на экспертизу", "research", 140, true, 0, 0f,
            new[] { At("delivered_to.mira") },
            "Есть дело... Вырос первый укроп. Отвези Тиму на станцию — пусть научно подтвердит, что это укроп. Я волнуюсь.",
            "Спасибо! Не тряси коробку — он впечатлительный.",
            "Ничего. Укроп подождёт. Он терпеливый.",
            "Мэй: «Тим подтвердил — укроп признан явлением! Теперь бы теплицу...»");
        Errand meiGreenhouse = MakeErrand("MeiGreenhouse", "mei-greenhouse", Errand.Legality.Ordinary, "Рассада к чаю", "konbini", 200, false, 0, 0f,
            new[] { At("errand.mei-dill", 2) },
            "Есть дело... Ника обещала плёнку для теплицы, если пришлю рассаду к чаю. Отвезёшь ящик?",
            "Ура! Ника знает, что делать. Ну, надеюсь.",
            "Жаль. Буду греть грядки лапами. Их у меня четыре, но всё равно мало.",
            "Мэй: «Плёнка пришла! Теплица стоит! Огород пошёл в рост — соседи ходят смотреть»." );
        mei.errands = new[] { meiDill, meiGreenhouse };
        mei.moods = new[]
        {
            Mood(new[] { At("errand.mei-greenhouse", 2) },
                "Теплица! Помидоры растут быстрее, чем я успеваю им представляться.",
                "Огород теперь видно с орбиты. Если прищуриться и очень захотеть.",
                "Соседи за горизонтом пришли смотреть на грядки. Сорок шагов — а как в музей.",
                "Кабачок соседа больше не самый большой. Сосед делает вид, что не обиделся. Кабачок обиделся."),
            Mood(new[] { At("errand.heat", 4) },
                "Ой. По радио про тебя говорили. Я сделала погромче для рассады — ей полезно знать новости."),
        };
        EditorUtility.SetDirty(mei);
        React(tim, meiDill.order, "Укроп? Проверил всеми приборами. Укроп. Поздравьте Мэй, это первое открытие за год.");
        React(nika, meiGreenhouse.order, "Рассада к чаю! Плёнку Мэй я уже отправила. Ну, попросила кого-нибудь. Тебя, наверное.");

        // Ира: сомнительная цепочка с обменом — у Иры появляется новый насос (декорация в Place).
        Errand iraPump = MakeErrand("IraPump", "ira-pump", Errand.Legality.Shady, "Списанный насос (без накладной)", "ochre", 280, false, 1, 0f,
            new[] { At("delivered_to.fuelstop04") },
            "Есть дело... Старый насос надо сбыть Бруно на Охру. Без накладной. Номер на нём стёрт. Сам стёрся, честно.",
            "Если спросят — это скульптура. «Насос, качающий по вторникам».",
            "Правильно. Я бы тоже себе не доверяла.",
            "Ира: «Бруно взял насос и пришлёт новый. Если отвезёшь ему ещё кое-что»." );
        Errand iraNoodles = MakeErrand("IraNoodles", "ira-noodles", Errand.Legality.Ordinary, "Лапша за насос", "ochre", 120, false, 0, 0f,
            new[] { At("errand.ira-pump", 2) },
            "Есть дело... Бруно хочет за новый насос ящик лапши. Курс такой. Отвезёшь?",
            "Лапша самая лучшая. Другой у нас нет.",
            "Ладно. Буду дальше пинать старый. То есть воздух — старый уже у Бруно.",
            "Ира: «Новый насос приехал! Блестит. Я его немного боюсь»." );
        ira.errands = new[] { iraPump, iraNoodles };
        ira.moods = new[]
        {
            Mood(new[] { At("errand.heat", 3) },
                "Слушай, тут патруль заправлялся. Спрашивал про курьера. Я сказала, что курьеры — это миф."),
            Mood(new[] { At("errand.ira-noodles", 2) },
                "Новый насос качает в обе стороны и даже по вторникам. Не знаю, что с этим делать.",
                "Старый насос теперь у Бруно. Говорит, по вторникам качает песок. Обратно.",
                "Каска осталась прежняя. Должно же в жизни быть что-то стабильное."),
        };
        EditorUtility.SetDirty(ira);
        React(bruno, iraPump.order, "Насос! Без номера! Мой любимый вид насосов. Ире скажи — новый за мной.");
        React(bruno, iraNoodles.order, "Лапша! Курс соблюдён. Насос Ире уже летит. Ну, лежит. Скоро полетит.");

        // Бруно: незаконная цепочка — дурная слава, давление кредиторов, изъятие на досмотре; дело Бруно растёт.
        Errand brunoFish = MakeErrand("BrunoFish", "bruno-fish", Errand.Legality.Illegal, "Камни в форме рыбы (неучтённая руда)", "research", 520, false, 2, 5f,
            new[] { At("delivered_to.ochre", 2) },
            "Есть дело... Ящик камней в форме рыбы. Тиму. Если спросят — это рыба. Если спросят, почему тяжёлая, — рыба толстая.",
            "Рыба поехала! Держись дальних маршрутов, там никто не смотрит.",
            "И правильно. Честный курьер — редкий вид. Как рыба из камня.",
            "Бруно: «Тим заплатил! Кстати, кредиторы откуда-то узнали, что у тебя завелись деньги»." );
        Errand brunoFish2 = MakeErrand("BrunoFish2", "bruno-fish2", Errand.Legality.Illegal, "Большая рыба (очень неучтённая)", "ice", 760, false, 3, 8f,
            new[] { At("errand.bruno-fish", 2) },
            "Есть дело... Сае нужен балласт для метеозондов. Большая рыба. Очень большая. Очень неучтённая.",
            "Партнёр! Ящик тяжёлый, совесть лёгкая. У меня, по крайней мере.",
            "Понимаю. Рыба — это ответственность.",
            "Бруно: «Бизнес растёт! Повешу лампу, чтобы клиенты находили»." );
        bruno.errands = new[] { brunoFish, brunoFish2 };
        bruno.moods = new[]
        {
            Mood(new[] { Below("errand.bruno-fish", -1) },
                "Ящик забрали? Ну... рыба уплыла. Бывает. Обычно не со мной."),
            Mood(new[] { Below("errand.bruno-fish2", -1) },
                "Большую рыбу забрали? Инспекция теперь ест хорошо. Хоть кто-то."),
            Mood(new[] { At("errand.bruno-fish2", 2) },
                "Партнёр! Видишь ящики? Не видишь. Вот и молодец.",
                "Бизнес растёт. Руда тоже. Она теперь в форме рыбы официально. Почти.",
                "Лампу повесил, чтобы клиенты находили. Клиентов нет, но лампа красивая."),
        };
        EditorUtility.SetDirty(bruno);
        React(tim, brunoFish.order, "Камни в форме рыбы? Для науки. Исключительно для науки. Никому не говори, что наука платит наличными.");
        React(saya, brunoFish2.order, "Балласт? Запишу: «осадки — рыба». Зонды будут летать солиднее.");

        // Ника: обычное письмо, затем сомнительный чай.
        Errand nikaLetter = MakeErrand("NikaLetter", "nika-letter", Errand.Legality.Ordinary, "Письмо без обратного адреса", "research", 90, false, 0, 0f,
            new[] { At("delivered", 3) },
            "Есть дело... Письмо Тиму. Не читай. Ладно, можешь: оно пустое. Я стесняюсь.",
            "Спасибо. Если он не ответит — я ничего не отправляла.",
            "Ой. Ну и ладно. Сама прочитаю. Пустое, как я и думала.",
            "Ника: «Тим ответил! Тоже пустым письмом. Это ведь значит \"да\"?»" );
        Errand nikaTea = MakeErrand("NikaTea", "nika-tea", Errand.Legality.Shady, "Чай «Бодрый» (без сертификата)", "ice", 260, false, 1, 0f,
            new[] { At("errand.nika-letter", 2) },
            "Есть дело... Сае нужен чай. Он не запрещён, он просто подозрительно бодрит. Сертифицировать не успели — не догнали.",
            "Только не нюхай. Прошлый курьер долетел без машины.",
            "Понимаю. Я бы тоже не взяла. Но я уже взяла, вот в чём беда.",
            "Ника: «Сая говорит, чай работает. Метель стихла от удивления»." );
        nika.errands = new[] { nikaLetter, nikaTea };
        nika.moods = new[]
        {
            Mood(new[] { At("errand.heat", 3) },
                "Тут про тебя спрашивали. В форме. Я сказала, что ты — чайник. Почему-то поверили."),
            Mood(new[] { At("errand.nika-letter", 2) },
                "Тим опять прислал пустое письмо. Мы переписываемся. Очень содержательно.",
                "Чайник ревнует к переписке. Свистит громче. Я делаю вид, что не замечаю."),
        };
        EditorUtility.SetDirty(nika);
        React(tim, nikaLetter.order, "Пустое письмо? Самое содержательное за год. Отвечу таким же.");
        React(saya, nikaTea.order, "Чай «Бодрый»... Записываю: «внутренняя температура — растёт». Спасибо. Наверное.");

        // Тим: одно обычное поручение.
        Errand timProbe = MakeErrand("TimProbe", "tim-probe", Errand.Legality.Ordinary, "Блудный метеозонд", "ice", 160, true, 0, 0f,
            new[] { At("heard.tim-1") },
            "Есть дело... Ко мне прилетел зонд Саи. Сам. Верни его, пожалуйста, с извинениями от зонда. Я их записал.",
            "Спасибо. Он не тяжёлый, просто ему стыдно.",
            "Ладно. Пусть поживёт у меня. Будем молчать в пустоту вдвоём.",
            "Тим: «Сая пишет, зонд дома. Обещал больше не улетать. Врёт, конечно»." );
        tim.errands = new[] { timProbe };
        tim.moods = new[]
        {
            Mood(new[] { At("errand.heat", 3) },
                "Курьер. Приборы фиксируют вокруг тебя повышенный фон. Подозрительности."),
        };
        EditorUtility.SetDirty(tim);
        React(saya, timProbe.order, "Мой зонд! Вернулся! Записываю: «осадки — радость, местами слёзы».");

        // Сая: поручений нет, но слава меняет приём.
        saya.moods = new[]
        {
            Mood(new[] { At("errand.wanted") },
                "Сводка: вас разыскивают, местами усиленно. Чай будет. Через окошко."),
        };
        EditorUtility.SetDirty(saya);
        Log("поручения: 9, профили обновлены");
    }

    static Milestone MakeMilestone(string id, string message, float pressure, FactCondition[] conditions,
        Milestone.FactValue[] facts = null, Milestone.PostOrder[] orders = null)
    {
        Milestone m = Asset<Milestone>(StoryDir + "Milestone_" + id + ".asset");
        m.id = id;
        m.message = message;
        m.pressure = pressure;
        m.conditions = conditions;
        m.facts = facts ?? new Milestone.FactValue[0];
        m.orders = orders ?? new Milestone.PostOrder[0];
        EditorUtility.SetDirty(m);
        return m;
    }

    static Milestone.PostOrder Post(string post, DeliveryOrder order) => new Milestone.PostOrder { post = post, order = order };

    // Вехи-заглушки; суммы и пороги — временные (половина долга — от DebtSettings.total 15000).
    static Milestone[] BuildMilestones()
    {
        DeliveryOrder heater = Order(OrdersDir + "Milestone_KonbiniToIce_Heater.asset", "Обогреватель для метеостанции", "ice", 420, true,
            At("milestone.first-upgrade"));
        DeliveryOrder drill = Order(OrdersDir + "Milestone_ResearchToOchre_Drill.asset", "Бур с паспортом", "ochre", 380, false,
            At("milestone.all-posts"));

        var list = new[]
        {
            MakeMilestone("first-delivery", "Первая доставка. Почтовая компания прислала поздравление: «Вы существуете». Без подписи.", 0f,
                new[] { At("delivered") }),
            MakeMilestone("first-payment", "Первый платёж кредиторам ушёл. В ответ пришёл смайлик. Зловещий.", 0f,
                new[] { At("debt.paid") }),
            MakeMilestone("first-missed", "Первая просрочка. Кредиторы запомнили. Они вообще хорошо запоминают.", 0f,
                new[] { At("debt.missed") }),
            MakeMilestone("first-upgrade", "Космокар стал лучше. На Конбини появился заказ подальше — на Ледышку.", 0f,
                new[] { At("upgrades") }, null, new[] { Post("konbini", heater) }),
            MakeMilestone("all-posts", "Ты побывал на всех почтах. Исследовательская станция прислала заказ на Охру.", 0f,
                new[] { At("visits.konbini"), At("visits.fuelstop04"), At("visits.research"), At("visits.mira"), At("visits.ochre"), At("visits.ice") },
                null, new[] { Post("research", drill) }),
            MakeMilestone("ten-deliveries", "Десять доставок. Компания повысила тебе ничего. Поздравляем.", 0f,
                new[] { At("delivered", 10) }),
            MakeMilestone("three-errands", "Три личных поручения. Тебя узнают не только по посылкам.", 0f,
                new[] { At("errand.done", 3) }),
            MakeMilestone("half-debt", "Половина долга позади. Кредиторы прислали открытку «Не расслабляйся». Давление чуть спало.", -10f,
                new[] { At("debt.paid", 7500) }),
            MakeMilestone("high-pressure", "Кредиторы прислали фото твоего космокара. Сзади. Очень близко.", 0f,
                new[] { At("debt.pressure", 60) }),
            MakeMilestone("bad-fame", "Твой номер висит на доске объявлений станции. Рядом с «Потерялся кот». Не тот кот. Кредиторы тоже видели.", 5f,
                new[] { At("errand.heat", 4) }, new[] { new Milestone.FactValue { fact = "errand.wanted", value = 1 } }),
        };
        Log("вехи: " + list.Length);
        return list;
    }

    static T Ensure<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    // Game.prefab: Milestones, Errands и окно поручения из MenuWindow.prefab.
    static void BuildGame(Milestone[] milestones)
    {
        GameObject window = AssetDatabase.LoadAssetAtPath<GameObject>(MenuWindowPrefab);
        if (window == null) throw new System.Exception("нет " + MenuWindowPrefab);
        using var scope = new PrefabUtility.EditPrefabContentsScope(GamePrefab);
        GameObject root = scope.prefabContentsRoot;
        Ensure<Milestones>(root).milestones = milestones;
        Ensure<Errands>(root);

        Transform menuObject = root.transform.Find("Errand menu");
        if (menuObject == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(window, root.transform);
            go.name = "Errand menu";
            menuObject = go.transform;
            Log("Game.prefab: добавлено Errand menu");
        }
        ErrandMenu menu = Ensure<ErrandMenu>(menuObject.gameObject);
        menu.title = menuObject.Find("Panel/Title").GetComponent<TMP_Text>();
        menu.status = menuObject.Find("Panel/Status").GetComponent<TMP_Text>();
        menu.list = menuObject.Find("Panel/List");
        menu.rowTemplate = menuObject.Find("Panel/Row").GetComponent<Button>();
        menu.pauseTime = true;
        menu.closeOnE = true;
        Transform hint = menuObject.Find("Panel/Hint");
        if (hint != null) hint.GetComponent<TMP_Text>().text = "Esc — закрыть";
        menuObject.gameObject.SetActive(false);
        Log("Game.prefab: Milestones, Errands, ErrandMenu");
    }

    // ---------- Модули декораций: примитивы, URP Lit, без коллайдеров (не мешают ходить) ----------

    static Material Mat(string name, Color color, bool glow = false)
    {
        string path = Materials + "Prototype_" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
            Log("создан " + path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.2f);
        if (glow)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Piece(Transform parent, PrimitiveType type, Material mat, Vector3 position, Vector3 scale, Vector3 euler = default)
    {
        GameObject p = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(p.GetComponent<Collider>());
        SceneManager.MoveGameObjectToScene(p, preview);
        p.transform.SetParent(parent, false);
        p.transform.localPosition = position;
        p.transform.localEulerAngles = euler;
        p.transform.localScale = scale;
        p.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Transform Root(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, preview);
        return go.transform;
    }

    static void SaveModule(Transform root)
    {
        string path = Modules + root.name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
        Log("модуль " + path);
    }

    static void BuildModules()
    {
        Material soil = Mat("ErrandSoil", new Color(0.36f, 0.23f, 0.13f));
        Material leaf = Mat("ErrandLeaf", new Color(0.32f, 0.66f, 0.26f));
        Material flower = Mat("ErrandFlower", new Color(1f, 0.8f, 0.15f));
        Material metal = Mat("ErrandMetal", new Color(0.86f, 0.83f, 0.76f));
        Material paint = Mat("ErrandPaint", new Color(0.95f, 0.5f, 0.15f));
        Material crate = Mat("ErrandCrate", new Color(0.55f, 0.38f, 0.2f));
        Material screen = Mat("ErrandScreen", new Color(0.2f, 1f, 0.8f), true);
        Material lamp = Mat("ErrandLamp", new Color(1f, 0.55f, 0.2f), true);
        Material stripe = Mat("ErrandStripe", new Color(1f, 0.2f, 0.15f), true);

        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            // Огород Мэй: три грядки, кусты, лежачий кабачок, подсолнух. ~3,4 x 1,6 м, пивот у пола.
            Transform garden = Root("ErrandGarden");
            for (int i = -1; i <= 1; i++)
            {
                Piece(garden, PrimitiveType.Cube, soil, new Vector3(i * 0.9f, 0.12f, 0f), new Vector3(0.7f, 0.24f, 1.5f));
                for (int j = -1; j <= 1; j++)
                    if (i != 0 || j != 0) Piece(garden, PrimitiveType.Sphere, leaf, new Vector3(i * 0.9f, 0.4f, j * 0.48f), Vector3.one * (0.3f + 0.05f * ((i + j + 3) % 3)));
            }
            Piece(garden, PrimitiveType.Capsule, leaf, new Vector3(0f, 0.42f, 0f), new Vector3(0.36f, 0.5f, 0.36f), new Vector3(90f, 0f, 0f));
            Piece(garden, PrimitiveType.Cylinder, leaf, new Vector3(1.6f, 0.65f, 0.3f), new Vector3(0.06f, 0.65f, 0.06f));
            Piece(garden, PrimitiveType.Sphere, flower, new Vector3(1.6f, 1.35f, 0.3f), new Vector3(0.4f, 0.4f, 0.12f));
            SaveModule(garden);

            // Новый насос Иры: тумба, козырёк, светящийся экран (смотрит в +Z), шланг. ~1,6 м.
            Transform pump = Root("ErrandPump");
            Piece(pump, PrimitiveType.Cube, metal, new Vector3(0f, 0.04f, 0f), new Vector3(0.8f, 0.08f, 0.7f));
            Piece(pump, PrimitiveType.Cube, paint, new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 1.4f, 0.5f));
            Piece(pump, PrimitiveType.Cube, metal, new Vector3(0f, 1.5f, 0f), new Vector3(0.72f, 0.1f, 0.62f));
            Piece(pump, PrimitiveType.Cube, screen, new Vector3(0f, 1.1f, 0.26f), new Vector3(0.4f, 0.24f, 0.02f));
            Piece(pump, PrimitiveType.Cylinder, metal, new Vector3(0.36f, 0.8f, 0.1f), new Vector3(0.06f, 0.35f, 0.06f));
            Piece(pump, PrimitiveType.Cube, paint, new Vector3(0.36f, 0.4f, 0.1f), new Vector3(0.1f, 0.12f, 0.2f));
            SaveModule(pump);

            // Ящики Бруно: штабель без маркировки, красная полоса, фонарь. ~2,5 м.
            Transform crates = Root("ErrandCrates");
            Piece(crates, PrimitiveType.Cube, crate, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.6f);
            Piece(crates, PrimitiveType.Cube, crate, new Vector3(0.66f, 0.3f, 0.05f), Vector3.one * 0.6f, new Vector3(0f, 8f, 0f));
            Piece(crates, PrimitiveType.Cube, crate, new Vector3(0.3f, 0.9f, 0f), Vector3.one * 0.6f, new Vector3(0f, -12f, 0f));
            Piece(crates, PrimitiveType.Cube, crate, new Vector3(-0.6f, 0.25f, 0.1f), Vector3.one * 0.5f, new Vector3(0f, 20f, 0f));
            Piece(crates, PrimitiveType.Cube, stripe, new Vector3(0f, 0.45f, 0f), new Vector3(0.62f, 0.06f, 0.62f));
            Piece(crates, PrimitiveType.Cylinder, metal, new Vector3(1.4f, 1.1f, 0f), new Vector3(0.05f, 1.1f, 0.05f));
            Piece(crates, PrimitiveType.Sphere, lamp, new Vector3(1.4f, 2.3f, 0f), Vector3.one * 0.28f);
            SaveModule(crates);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    // ---------- Place ----------

    [MenuItem("CatWalk/Temp/Place Errands")]
    static void Place()
    {
        log.Clear();
        try
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            Physics.SyncTransforms();
            int placed = 0;
            foreach (Resident r in Object.FindObjectsByType<Resident>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.profile == null || r.transform.parent == null) continue;
                // Смещения — в осях станции (родителя жителя), в стороне от пути «парковка -> почта».
                switch (r.profile.name)
                {
                    case "Mei": placed += Decor(r, "ErrandGarden", "mei-greenhouse", new Vector3(-3.5f, 0f, -14f), 90f); break;
                    case "Ira": placed += Decor(r, "ErrandPump", "ira-noodles", new Vector3(-6f, 0f, 3.5f), 180f); break;
                    case "Bruno": placed += Decor(r, "ErrandCrates", "bruno-fish2", new Vector3(6.5f, 0f, -17.5f), 180f); break;
                }
            }
            Log($"декораций: {placed}");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log("Place Errands: готово, сцена сохранена");
        }
        catch (System.Exception e)
        {
            Log("ОШИБКА: " + e);
            Debug.LogException(e);
        }
        File.AppendAllLines(LogPath, log);
    }

    // Держатель «Errand decor <модуль>» с FactToggle (всегда активен) и выключенный модуль внутри: включится,
    // когда поручение выполнено (errand.<id> >= 2). Пересоздаётся при каждом запуске. Ставится на пол лучом вниз.
    static int Decor(Resident r, string module, string errand, Vector3 local, float yaw)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Modules + module + ".prefab");
        if (prefab == null)
        {
            Log("нет модуля " + module + " — сначала Build Errands");
            return 0;
        }
        Transform station = r.transform.parent;
        string name = "Errand decor " + module;
        Transform old = station.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Vector3 up = station.up;
        Vector3 position = station.TransformPoint(local);
        if (Physics.Raycast(position + up * 3f, -up, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)) position = hit.point;
        else Log($"{station.name}/{module}: пол под точкой не найден, ставлю на уровень станции");

        var holder = new GameObject(name);
        holder.transform.SetParent(station, false);
        holder.transform.SetPositionAndRotation(position, station.rotation * Quaternion.Euler(0f, yaw, 0f));
        var decor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
        decor.transform.localPosition = Vector3.zero;
        decor.transform.localRotation = Quaternion.identity;
        decor.SetActive(false);

        FactToggle toggle = holder.AddComponent<FactToggle>();
        toggle.when = new[] { At("errand." + errand, 2) };
        toggle.show = new[] { decor };
        toggle.hide = new GameObject[0];
        Log($"{GetPath(holder.transform)} @ {position} (по errand.{errand} >= 2)");
        return 1;
    }

    static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
}
