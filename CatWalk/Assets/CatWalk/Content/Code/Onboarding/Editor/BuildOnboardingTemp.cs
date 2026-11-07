using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using S = TutorialStep.State;
using T = TutorialStep.Target;

// ВРЕМЕННЫЙ сборщик раздела Onboarding. Повторный запуск пересобирает то же самое (тексты и условия шагов перезаписываются).
// Build Onboarding: ассеты шагов Data/Onboarding/Step_NN_<id>.asset, префаб Prefabs/UI/Tutorial.prefab (свой канвас-плашка),
//                   вложенный Tutorial в Prefabs/Game.prefab (если его там нет). Сцену не трогает. Лог — Temp/onboarding_build.txt.
static class BuildOnboardingTemp
{
    const string DataDir = "Assets/CatWalk/Data/Onboarding";
    const string Ui = "Assets/CatWalk/Prefabs/UI/";
    const string PrefabPath = Ui + "Tutorial.prefab";
    const string GamePath = "Assets/CatWalk/Prefabs/Game.prefab";
    static readonly Color Teal = new(0.35f, 0.85f, 1f);

    static readonly List<string> log = new();

    static void Log(string line)
    {
        log.Add(line);
        Debug.Log("Onboarding: " + line);
    }

    static void Error(string line)
    {
        log.Add("ОШИБКА: " + line);
        Debug.LogError("Onboarding: " + line);
    }

    [MenuItem("CatWalk/Temp/Build Onboarding")]
    static void Build()
    {
        log.Clear();
        TutorialStep[] steps = BuildSteps();
        GameObject prefab = BuildPrefab(steps);
        if (prefab != null) NestInGame(prefab);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Temp");
        File.WriteAllLines("Temp/onboarding_build.txt", log);
    }

    // ---------- Шаги ----------

    static FactCondition F(string fact, int value = 1) => new() { fact = fact, compare = FactCondition.Compare.AtLeast, value = value };
    static FactCondition No(string fact, int value = 1) => new() { fact = fact, compare = FactCondition.Compare.Below, value = value };

    static TutorialStep.Condition C(FactCondition[] facts = null, S[] all = null, S[] none = null, string menu = "") =>
        new() { facts = facts ?? new FactCondition[0], all = all ?? new S[0], none = none ?? new S[0], menu = menu };

    static TutorialStep[] BuildSteps()
    {
        if (!AssetDatabase.IsValidFolder(DataDir)) AssetDatabase.CreateFolder("Assets/CatWalk/Data", "Onboarding");
        FactCondition first = No("delivered");          // до первой доставки
        var steps = new List<TutorialStep>();
        void Step(string id, string text, TutorialStep.Condition show, TutorialStep.Condition done,
                  float timeout = 0f, T target = T.None, string visibleIn = "")
        {
            string path = $"{DataDir}/Step_{steps.Count + 1:00}_{id}.asset";
            var step = AssetDatabase.LoadAssetAtPath<TutorialStep>(path);
            if (step == null)
            {
                step = ScriptableObject.CreateInstance<TutorialStep>();
                AssetDatabase.CreateAsset(step, path);
                Log("создан " + path);
            }
            else Log("обновлён " + path);
            step.id = id;
            step.text = text;
            step.show = show;
            step.done = done;
            step.timeout = timeout;
            step.target = target;
            step.visibleIn = visibleIn;
            EditorUtility.SetDirty(step);
            steps.Add(step);
        }

        // Тексты временные.
        Step("exit", "Космокар стоит на парковке станции. E — выйти из машины.",
            C(new[] { first }, new[] { S.Driving, S.Docked }), C(all: new[] { S.Walking }));
        Step("post", "Почта — терминал под вывеской «ПОЧТА». Подойди к нему: стрелка покажет, куда идти.",
            C(new[] { first }, new[] { S.Walking }, new[] { S.NearPost, S.ParcelWaiting, S.Holding, S.Loaded }),
            C(all: new[] { S.NearPost }), target: T.Post);
        Step("order", "E — меню почты. «Взять» — принять заказ: посылка появится в зоне выдачи рядом с терминалом. Награда — в строке заказа.",
            C(new[] { first }, new[] { S.NearPost }, new[] { S.ParcelWaiting, S.Holding, S.Loaded }),
            C(all: new[] { S.ParcelWaiting }), target: T.Post, visibleIn: "PostTerminalMenu");
        Step("pickup", "F — взять посылку из зоны выдачи.",
            C(new[] { first }, new[] { S.Walking, S.ParcelWaiting }, new[] { S.Holding }),
            C(all: new[] { S.Holding }), target: T.Parcel);
        Step("shelf", "Неси коробку к задней полке космокара. F у полки — поставить. С коробкой в руках в машину не сесть.",
            C(new[] { first }, new[] { S.Holding }), C(all: new[] { S.Loaded }), target: T.Car);
        Step("board", "Груз на полке. E у машины — сесть за руль.",
            C(new[] { first }, new[] { S.Walking, S.Loaded }, new[] { S.Holding, S.ParcelWaiting }),
            C(all: new[] { S.Driving }), target: T.Car);
        Step("takeoff", "Space — вверх: тяга сильнее магнита парковки. W/S — газ, A/D — поворот, ПКМ — рулить мышью.",
            C(new[] { first }, new[] { S.Driving, S.Loaded }), C(none: new[] { S.Magnet }));
        Step("map", "M — карта. Пунктир ведёт от посылки к почте-адресату.",
            C(new[] { F("tutorial.takeoff") }, new[] { S.Driving, S.Loaded }, new[] { S.Magnet }),
            C(menu: "MapMenu"), timeout: 25f);
        Step("target", "Выбери цель: клик по адресу посылки в списке карты. Без карты — T, следующая точка по кругу.",
            C(new[] { F("tutorial.takeoff") }, new[] { S.Driving, S.Loaded }, new[] { S.HasTarget }),
            C(all: new[] { S.HasTarget }), timeout: 40f, visibleIn: "MapMenu");
        Step("cruise", "Далеко лететь? Маршевый: держи Shift и нажми Space, газ — W. Отпустишь Shift — маршевый выключится.",
            C(new[] { first, F("tutorial.takeoff") }, new[] { S.Driving, S.HasTarget }, new[] { S.Magnet }),
            C(all: new[] { S.Cruising }), timeout: 45f);
        Step("arrive", "Рамка на экране — цель. Перед местом отпусти Shift и сбавь ход: парковку видно по светящемуся магниту.",
            C(new[] { first, F("tutorial.takeoff") }, new[] { S.Driving, S.Loaded }, new[] { S.Magnet }),
            C(all: new[] { S.Magnet }));
        Step("park", "Магнит тянет к площадке. Зависни над разметкой почти без скорости — машина встанет сама, свет станет зелёным.",
            C(new[] { first, F("tutorial.arrive") }, new[] { S.Driving, S.Magnet }, new[] { S.Docked }),
            C(all: new[] { S.Docked }));
        Step("unload", "На месте? E — выйти, F — снять коробку с полки и поставить в зону приёма у почты адресата.",
            C(new[] { first, F("tutorial.park") }, none: new[] { S.InReceiveZone }),
            C(all: new[] { S.InReceiveZone }), target: T.Address);
        Step("deliver", "E — почта -> «Отметить доставку»: посылки из зоны приёма закрываются, оплата — сразу на счёт.",
            C(new[] { first }, new[] { S.InReceiveZone }), C(new[] { F("delivered") }), target: T.Post, visibleIn: "PostTerminalMenu");
        Step("paid", "Первая оплата! Баланс — справа вверху. Дальние адреса платят больше, но и топлива уходит больше.",
            C(new[] { F("delivered") }), C(), timeout: 8f);
        Step("debt", "Кредиторы ждут платежей: их вносят в меню почты (строка «Кредиторам»), срок — там же. Просрочка злит кредиторов.",
            C(new[] { F("delivered"), No("debt.paid") }, new[] { S.NearPost }), C(new[] { F("debt.paid") }),
            timeout: 30f, visibleIn: "PostTerminalMenu");
        Step("save", "Сохраниться можно только в телефонной будке — она есть почти на каждой остановке. E у будки -> «Сохранить игру».",
            C(new[] { F("delivered") }, new[] { S.Walking }), C(new[] { F("save.count") }), timeout: 40f, target: T.Booth);
        Step("journal", "J — журнал взятых заказов: что везёшь, куда и сколько заплатят. Клик по заказу — цель.",
            C(new[] { F("delivered") }, new[] { S.Loaded }), C(menu: "OrderJournal"), timeout: 20f);
        Step("fuel", "Топлива меньше половины. Колонки — на заправках (на карте — кубики) и у почт на планетах. E у колонки.",
            C(all: new[] { S.LowFuel }), C(menu: "FuelPumpMenu"), timeout: 30f, target: T.Pump);
        Step("upgrade", "Хватает на улучшение космокара: мастерские — на Конбини и Исследовательской станции. Бак, трюм, двигатель, экономичность.",
            C(new[] { F("delivered"), No("upgrades") }, new[] { S.CanUpgrade }), C(menu: "WorkshopMenu"), timeout: 25f, target: T.Workshop);
        return steps.ToArray();
    }

    // ---------- Плашка ----------

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static TextMeshProUGUI Text(string name, Transform parent, TMP_Text style, float size, TextAlignmentOptions align)
    {
        RectTransform rect = Rect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = style.font;
        text.fontSharedMaterial = style.fontSharedMaterial;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    static GameObject BuildPrefab(TutorialStep[] steps)
    {
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>(Ui + "Hud.prefab");
        var window = AssetDatabase.LoadAssetAtPath<GameObject>(Ui + "MenuWindow.prefab");
        Transform promptT = hud != null ? hud.transform.Find("Prompt") : null;
        TMP_Text style = promptT != null ? promptT.GetComponentInChildren<TMP_Text>(true) : null;
        if (style == null)
        {
            Error("нет шрифта: не нашёл TMP_Text в " + Ui + "Hud.prefab/Prompt");
            return null;
        }
        Image windowPanel = window != null && window.transform.Find("Panel") != null ? window.transform.Find("Panel").GetComponent<Image>() : null;
        Color back = windowPanel != null ? windowPanel.color : new Color(0.05f, 0.07f, 0.1f, 0.85f);
        if (windowPanel == null) Log("у MenuWindow/Panel нет Image — фон плашки по умолчанию");

        var root = new GameObject("Tutorial", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;          // над HUD, под окнами меню нет смысла — плашка сбоку
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Tutorial tutorial = root.AddComponent<Tutorial>();
        tutorial.steps = steps;

        // Слева по центру высоты: не перекрывает подсказки внизу, сообщения сверху и баланс справа.
        RectTransform panel = Rect("Panel", root.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(32f, 120f);
        panel.sizeDelta = new Vector2(560f, 0f);
        Image image = panel.gameObject.AddComponent<Image>();
        image.color = back;
        image.raycastTarget = false;
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 8f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        tutorial.text = Text("Text", panel, style, 22f, TextAlignmentOptions.TopLeft);

        // Строка стрелки: шеврон «^» из двух полосок (как стрелка FlightHud) и расстояние.
        RectTransform pointer = Rect("Pointer", panel);
        pointer.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
        RectTransform arrow = Rect("Arrow", pointer);
        arrow.anchorMin = arrow.anchorMax = new Vector2(0f, 0.5f);
        arrow.pivot = new Vector2(0.5f, 0.5f);
        arrow.anchoredPosition = new Vector2(18f, 0f);
        arrow.sizeDelta = new Vector2(32f, 32f);
        foreach ((string name, float x, float angle) in new[] { ("Left", -6f, -35f), ("Right", 6f, 35f) })
        {
            RectTransform bar = Rect(name, arrow);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = new Vector2(x, 0f);
            bar.sizeDelta = new Vector2(5f, 22f);
            bar.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image img = bar.gameObject.AddComponent<Image>();
            img.color = Teal;
            img.raycastTarget = false;
        }
        tutorial.arrow = arrow;
        TextMeshProUGUI distance = Text("Distance", pointer, style, 20f, TextAlignmentOptions.MidlineLeft);
        distance.color = Teal;
        RectTransform dr = distance.rectTransform;
        dr.anchorMin = new Vector2(0f, 0f);
        dr.anchorMax = new Vector2(1f, 1f);
        dr.offsetMin = new Vector2(48f, 0f);
        dr.offsetMax = Vector2.zero;
        tutorial.distance = distance;
        tutorial.panel = panel.gameObject;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        if (saved == null) Error("не сохранился " + PrefabPath);
        else Log($"префаб {PrefabPath}: шагов {steps.Length}");
        return saved;
    }

    static void NestInGame(GameObject prefab)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(GamePath) == null)
        {
            Error("нет " + GamePath);
            return;
        }
        using var scope = new PrefabUtility.EditPrefabContentsScope(GamePath);
        GameObject game = scope.prefabContentsRoot;
        if (game.GetComponentInChildren<Tutorial>(true) != null)
        {
            Log("Tutorial уже вложен в Game.prefab");
            return;
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, game.transform);
        instance.name = "Tutorial";
        Log("Tutorial вложен в Game.prefab");
    }
}
