using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Временный сборщик мастерской (удалить после проверки). Повторный запуск обновляет, а не дублирует:
// префаб WorkshopTerminal; на CosmoCar — корпус, улучшения, буксир, доп. слоты трюма, полоска корпуса;
// хрупкие заказы. Сцены не трогает.
static class BuildWorkshopTemp
{
    const string ModelPath = "Assets/CatWalk/Content/Environment/Modules/Prototype/WorkshopTerminal.prefab";
    const string MenuWindowPath = "Assets/CatWalk/Prefabs/UI/MenuWindow.prefab";
    const string BarPath = "Assets/CatWalk/Prefabs/UI/Bar.prefab";
    const string TerminalPath = "Assets/CatWalk/Prefabs/WorkshopTerminal.prefab";
    const string CarPath = "Assets/CatWalk/Prefabs/CosmoCar.prefab";
    const string KonbiniPath = "Assets/CatWalk/Prefabs/SpaceKonbini.prefab";
    const string SealsPath = "Assets/CatWalk/Data/Delivery/FuelStopToKonbini_Seals.asset";
    const string JarsPath = "Assets/CatWalk/Data/Delivery/KonbiniToFuelStop_Jars.asset";

    [MenuItem("CatWalk/Temp/Build Workshop")]
    static void Build()
    {
        // Всё нужное грузим заранее: если чего-то нет, ни один префаб не тронут.
        GameObject model = Load<GameObject>(ModelPath);
        GameObject window = Load<GameObject>(MenuWindowPath);
        GameObject bar = Load<GameObject>(BarPath);
        DeliveryOrder seals = Load<DeliveryOrder>(SealsPath);
        Load<GameObject>(CarPath);
        Load<GameObject>(KonbiniPath);

        BuildTerminal(model, window);
        BuildCar(bar);
        BuildOrders(seals);
        AssetDatabase.SaveAssets();
        Debug.Log("Build Workshop: готово.");
    }

    static void BuildTerminal(GameObject model, GameObject window)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TerminalPath) == null)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            var empty = new GameObject("WorkshopTerminal");
            SceneManager.MoveGameObjectToScene(empty, preview);
            PrefabUtility.SaveAsPrefabAsset(empty, TerminalPath);
            EditorSceneManager.ClosePreviewScene(preview);
        }

        using var scope = new PrefabUtility.EditPrefabContentsScope(TerminalPath);
        GameObject root = scope.prefabContentsRoot;

        if (root.transform.Find("Model") == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.name = "Model";
        }

        // Экран смотрит в −Z: кот встаёт перед ним.
        Transform usePoint = Child(root.transform, "Use point");
        usePoint.SetLocalPositionAndRotation(new Vector3(0f, 0f, -0.9f), Quaternion.identity);

        Transform menuObject = root.transform.Find("Menu");
        if (menuObject == null)
        {
            menuObject = ((GameObject)PrefabUtility.InstantiatePrefab(window, root.transform)).transform;
            menuObject.name = "Menu";
        }
        WorkshopMenu menu = Ensure<WorkshopMenu>(menuObject.gameObject);
        menu.title = menuObject.Find("Panel/Title").GetComponent<TMP_Text>();
        menu.status = menuObject.Find("Panel/Status").GetComponent<TMP_Text>();
        menu.list = menuObject.Find("Panel/List");
        menu.rowTemplate = menuObject.Find("Panel/Row").GetComponent<Button>();

        WorkshopTerminal terminal = Ensure<WorkshopTerminal>(root);
        terminal.usePoint = usePoint;
        terminal.menu = menu;
    }

    static void BuildCar(GameObject barPrefab)
    {
        using var scope = new PrefabUtility.EditPrefabContentsScope(CarPath);
        GameObject car = scope.prefabContentsRoot;

        Hull hull = Ensure<Hull>(car);
        CarUpgrades upgrades = Ensure<CarUpgrades>(car);
        Ensure<Tow>(car);

        // Трюм: 4 группы по 3 слота, шаг как у полки (1 м поперёк, 0,9 м вдоль). Каждая группа — столбец из трёх слотов
        // вдоль машины на своей пластине: уровни 1–2 — боковые стойки слева и справа от полки, 3–4 — второй ярус над ней.
        Transform shelf = car.transform.Find("Cargo shelf");
        GameObject plate = shelf.Find("Plate").gameObject;
        var groups = new GameObject[4];
        for (int i = 0; i < groups.Length; i++)
        {
            bool side = i < 2;
            Transform group = Child(shelf, $"Extra slots {i + 1}");
            group.SetLocalPositionAndRotation(new Vector3((i % 2 == 0 ? -1f : 1f) * (side ? 1.5f : 0.5f), side ? 0f : 0.6f, 0f), Quaternion.identity);

            Transform groupPlate = group.Find("Plate");
            if (groupPlate == null)
            {
                groupPlate = Object.Instantiate(plate, group).transform;
                groupPlate.name = "Plate";
            }
            groupPlate.SetLocalPositionAndRotation(new Vector3(0f, 0.02f, 0f), Quaternion.identity);
            groupPlate.localScale = new Vector3(side ? 0.8f : 1f, 0.04f, 2.7f);

            for (int j = 0; j < 3; j++)
            {
                Transform slot = Child(group, $"Slot {j + 1}");
                slot.SetLocalPositionAndRotation(new Vector3(0f, 0.04f, 0.9f * (1 - j)), Quaternion.identity);
                Ensure<ParcelSlot>(slot.gameObject);
            }
            group.gameObject.SetActive(false);
            groups[i] = group.gameObject;
        }
        upgrades.extraSlots = groups;

        // Полоска корпуса: свой канвас рядом с «Fuel gauge», полоска — над топливной.
        Transform fuelGauge = car.transform.Find("Fuel gauge");
        Transform gauge = car.transform.Find("Hull gauge");
        if (gauge == null)
        {
            gauge = new GameObject("Hull gauge", typeof(RectTransform)).transform;
            gauge.gameObject.layer = fuelGauge.gameObject.layer;
            gauge.SetParent(car.transform, false);
            gauge.SetSiblingIndex(fuelGauge.GetSiblingIndex() + 1);
        }
        Canvas fuelCanvas = fuelGauge.GetComponent<Canvas>();
        Canvas canvas = Ensure<Canvas>(gauge.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = fuelCanvas.sortingOrder;
        CanvasScaler fuelScaler = fuelGauge.GetComponent<CanvasScaler>();
        CanvasScaler scaler = Ensure<CanvasScaler>(gauge.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = fuelScaler.referenceResolution;
        scaler.screenMatchMode = fuelScaler.screenMatchMode;
        scaler.matchWidthOrHeight = fuelScaler.matchWidthOrHeight;

        Transform bar = gauge.Find("Bar");
        if (bar == null)
        {
            bar = ((GameObject)PrefabUtility.InstantiatePrefab(barPrefab, gauge)).transform;
            bar.name = "Bar";
        }
        var fuelBar = fuelGauge.Find("Bar") as RectTransform;
        var rect = (RectTransform)bar;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = fuelBar != null ? fuelBar.sizeDelta : new Vector2(520f, 14f);
        rect.anchoredPosition = (fuelBar != null ? fuelBar.anchoredPosition : new Vector2(0f, 96f)) + new Vector2(0f, 64f);

        HullGauge hullGauge = Ensure<HullGauge>(gauge.gameObject);
        hullGauge.hull = hull;
        hullGauge.fill = bar.Find("Fill").GetComponent<Image>();
        hullGauge.label = bar.GetComponentInChildren<TMP_Text>(true);
    }

    static void BuildOrders(DeliveryOrder seals)
    {
        seals.fragile = true;
        EditorUtility.SetDirty(seals);

        var jars = AssetDatabase.LoadAssetAtPath<DeliveryOrder>(JarsPath);
        if (jars == null)
        {
            jars = ScriptableObject.CreateInstance<DeliveryOrder>();
            AssetDatabase.CreateAsset(jars, JarsPath);
        }
        jars.cargo = "Банки с соленьями";
        jars.to = "fuelstop04";
        jars.reward = 90;
        jars.fragile = true;
        EditorUtility.SetDirty(jars);

        // Новый заказ выдаёт почта комбини — рядом с остальными её заказами.
        using var scope = new PrefabUtility.EditPrefabContentsScope(KonbiniPath);
        PostTerminal post = scope.prefabContentsRoot.GetComponentInChildren<PostTerminal>(true);
        if (System.Array.IndexOf(post.orders, jars) < 0)
        {
            DeliveryOrder[] orders = post.orders;
            ArrayUtility.Add(ref orders, jars);
            post.orders = orders;
        }
    }

    static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new System.Exception("Build Workshop: нет ассета " + path);
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
}
