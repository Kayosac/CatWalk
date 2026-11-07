using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ВРЕМЕННЫЙ сборщик навигации: префабы карты и полётного HUD, навигатор и маршевый на космокаре,
// точки Location на готовых станциях. Повторный запуск пересобирает то же самое. После проверки — удалить.
static class BuildNavigationTemp
{
    const string Prefabs = "Assets/CatWalk/Prefabs/";
    const string Ui = Prefabs + "UI/";
    const string FontPath = "Assets/CatWalk/Fonts/Resources/Fonts & Materials/Monocraft.asset";
    const string SpinUpVfx = "Assets/Plugins/Retro Arsenal/Prefabs/Combat/Fighting/ChargeWaves.prefab";
    const string ArrivalVfx = "Assets/Plugins/Retro Arsenal/Prefabs/Combat/Nova/Basic/NovaBlue.prefab";
    static readonly Color Accent = new(0.35f, 0.85f, 1f);

    [MenuItem("CatWalk/Temp/Build Navigation")]
    static void Build()
    {
        BuildMap();
        BuildHud();
        SetupCar();
        SetupLocation("SpaceKonbini", "konbini", "Конбини", Location.Kind.Station);
        SetupLocation("FuelStop04", "fuelstop04", "Заправка 04", Location.Kind.FuelStop);
        SetupLocation("ResearchStation", "research", "Исследовательская станция", Location.Kind.Station);
        SetupLocation("PlanetParking", "", "", Location.Kind.Planet);
        AssetDatabase.SaveAssets();
        Debug.Log("Build Navigation: готово");
    }

    // Карта: вариант MenuWindow с компонентом MapMenu.
    static void BuildMap()
    {
        var window = AssetDatabase.LoadAssetAtPath<GameObject>(Ui + "MenuWindow.prefab");
        if (window == null)
        {
            Debug.LogError("Build Navigation: нет " + Ui + "MenuWindow.prefab");
            return;
        }
        var root = (GameObject)PrefabUtility.InstantiatePrefab(window);
        root.name = "MapMenu";
        MapMenu menu = root.AddComponent<MapMenu>();
        menu.title = root.transform.Find("Panel/Title").GetComponent<TMP_Text>();
        menu.status = root.transform.Find("Panel/Status").GetComponent<TMP_Text>();
        menu.list = root.transform.Find("Panel/List");
        menu.rowTemplate = root.transform.Find("Panel/Row").GetComponent<Button>();
        menu.pauseTime = true;
        menu.closeOnE = false;
        Transform hint = root.transform.Find("Panel/Hint");
        if (hint != null) hint.GetComponent<TMP_Text>().text = "Клик — сделать целью · Tab или Esc — закрыть";
        root.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, Ui + "MapMenu.prefab");
        Object.DestroyImmediate(root);
    }

    // Полётный HUD: свой канвас. Слева снизу — скорость, цель, маршевый; справа снизу — парковка; маркер цели.
    static void BuildHud()
    {
        var bar = AssetDatabase.LoadAssetAtPath<GameObject>(Ui + "Bar.prefab");
        if (bar == null)
        {
            Debug.LogError("Build Navigation: нет " + Ui + "Bar.prefab");
            return;
        }

        var root = new GameObject("FlightHud", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        FlightHud hud = root.AddComponent<FlightHud>();

        Vector2 bottomLeft = Vector2.zero, bottomRight = new(1f, 0f), center = new(0.5f, 0.5f);
        hud.speed = Text(root.transform, "Speed", bottomLeft, new Vector2(40f, 164f), new Vector2(480f, 36f), 28f, TextAlignmentOptions.BottomLeft);
        hud.target = Text(root.transform, "Target", bottomLeft, new Vector2(40f, 128f), new Vector2(760f, 30f), 20f, TextAlignmentOptions.BottomLeft);

        RectTransform cruise = Bar(bar, root.transform, "Cruise", bottomLeft, new Vector2(40f, 96f), new Vector2(420f, 14f));
        hud.cruiseFill = cruise.Find("Fill").GetComponent<Image>();
        hud.cruiseLabel = cruise.Find("Label").GetComponent<TMP_Text>();

        RectTransform parking = Bar(bar, root.transform, "Parking", bottomRight, new Vector2(-40f, 96f), new Vector2(420f, 14f));
        hud.parking = parking.gameObject;
        hud.parkingFill = parking.Find("Fill").GetComponent<Image>();
        hud.parkingLabel = parking.Find("Label").GetComponent<TMP_Text>();

        // Маркер: рамка из уголков и подпись под ней.
        RectTransform marker = Rect(root.transform, "Marker", center, Vector2.zero, new Vector2(56f, 56f));
        foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
        {
            Line(marker, "Corner H", corner, Vector2.zero, new Vector2(16f, 3f), 0f);
            Line(marker, "Corner V", corner, Vector2.zero, new Vector2(3f, 16f), 0f);
        }
        hud.marker = marker;
        hud.markerLabel = Text(marker, "Label", new Vector2(0.5f, 0f), new Vector2(0f, -6f), new Vector2(480f, 26f), 18f, TextAlignmentOptions.Top);
        hud.markerLabel.rectTransform.pivot = new Vector2(0.5f, 1f);

        // Стрелка у края: «^», вершиной к цели.
        RectTransform arrow = Rect(root.transform, "Edge arrow", center, Vector2.zero, new Vector2(32f, 32f));
        Line(arrow, "Left", center, new Vector2(-7f, 1f), new Vector2(4f, 20f), -45f);
        Line(arrow, "Right", center, new Vector2(7f, 1f), new Vector2(4f, 20f), 45f);
        hud.edgeArrow = arrow;

        PrefabUtility.SaveAsPrefabAsset(root, Ui + "FlightHud.prefab");
        Object.DestroyImmediate(root);
    }

    // Навигатор, маршевый (CarUpgrades добавится сам) и эффекты маршевого детьми машины.
    static void SetupCar()
    {
        using var scope = new PrefabUtility.EditPrefabContentsScope(Prefabs + "CosmoCar.prefab");
        GameObject car = scope.prefabContentsRoot;
        if (!car.TryGetComponent(out Navigator _)) car.AddComponent<Navigator>();
        if (!car.TryGetComponent(out CruiseDrive cruise)) cruise = car.AddComponent<CruiseDrive>();
        cruise.spinUpVfx = Vfx(car.transform, SpinUpVfx, "Cruise spin-up VFX", 2f, true);
        cruise.arrivalVfx = Vfx(car.transform, ArrivalVfx, "Cruise arrival VFX", 3f, false);
    }

    // Эффект Retro Arsenal ребёнком машины, не играет сам. local — частицы летят вместе с машиной.
    static ParticleSystem Vfx(Transform car, string path, string name, float scale, bool local)
    {
        Transform child = car.Find(name);
        if (child == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("Build Navigation: нет " + path);
                return null;
            }
            child = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, car)).transform;
            child.name = name;
        }
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one * scale;
        foreach (ParticleSystem system in child.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            if (local) main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }
        return child.GetComponentInChildren<ParticleSystem>(true);
    }

    // Точка на карте в корне готовой сборки; radius — по габаритам рендереров, arrival — дочерний «Cruise arrival», если есть.
    static void SetupLocation(string prefab, string id, string title, Location.Kind kind)
    {
        using var scope = new PrefabUtility.EditPrefabContentsScope(Prefabs + prefab + ".prefab");
        GameObject root = scope.prefabContentsRoot;
        if (!root.TryGetComponent(out Location location)) location = root.AddComponent<Location>();
        location.id = id;
        location.title = title;
        location.kind = kind;

        bool any = false;
        var bounds = new Bounds();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!any) bounds = r.bounds;
            else bounds.Encapsulate(r.bounds);
            any = true;
        }
        if (any)
        {
            // Самый дальний от пивота угол габаритов, с округлением вверх до 5 м.
            float radius = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one);
                radius = Mathf.Max(radius, Vector3.Distance(root.transform.position, corner));
            }
            location.radius = Mathf.Ceil(radius / 5f) * 5f;
        }

        Transform arrival = FindDeep(root.transform, "Cruise arrival");
        if (arrival != null) location.arrival = arrival;
        Debug.Log($"Build Navigation: {prefab} — radius {location.radius} м, arrival {(location.arrival != null ? location.arrival.name : "нет")}");
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static RectTransform Bar(GameObject prefab, Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)((GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)).transform;
        rect.name = name;
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static void Line(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, float angle)
    {
        RectTransform rect = Rect(parent, name, anchor, position, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = Accent;
        image.raycastTarget = false;
    }

    static TMP_Text Text(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = Rect(parent, name, anchor, position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}
