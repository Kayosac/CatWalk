using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Карта (M): трёхмерная мини-модель мира и список мест сбоку. Время на паузе. M или Esc — закрыть.
// Модель собирается при открытии по Location.All далеко от мира, на высоте modelHeight (дальше прорисовки игровых камер),
// в масштабе, где весь мир — около modelSize единиц. Планеты — сферы своего размера, станции — ромбы, заправки — кубики,
// находки — точки, машина — стрелка по курсу, кот пешком — точка. От меток — нитки вниз до сетки, по ним видна высота.
// Пунктир: от посылок взятых заказов до почты-адресата и от машины до цели.
// Модель снимает своя камера в текстуру, а текстура лежит на весь экран под панелью меню —
// так карта не зависит от низкого разрешения мира (WorldResolution на главной камере).
// ЛКМ — вращать, колесо — масштаб, ПКМ — сдвиг, клик по метке — цель, F — вид к машине.
// Геймпад: правый стик — вращать, RT/LT — масштаб, строки списка — цель, X — вид к машине, Y — закрыть.
public class MapMenu : MenuScreen
{
    public Camera mapCamera;              // выключена, пока карта закрыта
    public RawImage view;                 // на весь экран под панелью: сюда выводится камера карты
    public Material solidMaterial;        // URP Unlit: метки и сплошные линии, цвет — _BaseColor
    public Material dashMaterial;         // URP Unlit с Alpha Clipping: пунктир, текстура делается в коде
    public int layer = 30;                // слой мини-модели: камера карты видит только его
    public float modelSize = 100f;        // мир вписывается в столько единиц карты
    public float modelHeight = 20000f;    // где строится модель; игровые камеры видят до 8 км
    public float iconSize = 0.012f;       // метка, доля расстояния до камеры
    public float labelSize = 0.016f;      // высота подписи, доля расстояния до камеры
    public float lineWidth = 0.0025f;     // толщина линий, доля расстояния до камеры

    [Header("Цвета")]
    public Color stationColor = new(0.35f, 0.85f, 1f);
    public Color fuelStopColor = new(1f, 0.75f, 0.3f);
    public Color planetColor = new(0.4f, 0.55f, 0.85f);
    public Color findColor = new(0.75f, 0.75f, 0.85f);
    public Color carColor = Color.white;
    public Color catColor = new(0.5f, 1f, 0.5f);
    public Color targetColor = new(1f, 0.9f, 0.2f);
    public Color routeColor = new(1f, 0.5f, 0.3f);
    public Color gridColor = new(0.13f, 0.17f, 0.24f);

    Navigator navigator;
    int openFrame;
    Transform model;                      // корень мини-модели
    Vector3 center;                       // центр мира: он попадает в (0, modelHeight, 0)
    float scale;                          // единиц карты на метр
    RenderTexture image;
    Texture2D dashTexture;
    Mesh sphere, cube, arrow;
    MaterialPropertyBlock block;

    // Вид: камера смотрит на pivot с расстояния distance.
    Vector3 pivot;
    float yaw, pitch = 35f, distance;
    bool grabbed;                         // мышь нажата над картой, а не над панелью
    float travel;                         // сколько пикселей мышь прошла с нажатия: мало — это клик

    readonly List<(Transform icon, float size, float min)> icons = new(); // min — размер в масштабе мира
    readonly List<(TMP_Text text, Transform icon)> labels = new();
    readonly List<(LineRenderer line, bool dashed)> lines = new();
    readonly List<(Location location, Transform icon)> picks = new();

    // Navigator.Current, если owner не указан.
    public void Open(Navigator owner = null)
    {
        navigator = owner != null ? owner : Navigator.Current;
        if (navigator == null || !Show()) return;
        openFrame = Time.frameCount;
        Prepare();
        Refresh();
        ResetView();
        Place();
    }

    public override void Close()
    {
        if (Opened != this) return;
        base.Close();
        if (mapCamera != null) mapCamera.enabled = false;
        if (model != null) model.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (image != null) image.Release();
        if (model != null) Destroy(model.gameObject);
        if (mapCamera != null) Destroy(mapCamera.gameObject);
    }

    protected override void Update()
    {
        base.Update();
        if (Opened != this) return;
        if (Time.frameCount != openFrame && !Controls.Rebinding)
        {
            if (Controls.Map.WasPressedThisFrame())
            {
                Close();
                return;
            }
            if (Controls.Use.WasPressedThisFrame()) ResetView();
        }
        Steer(Mouse.current);
        Pad();
        Place();
    }

    // Камера, текстура на весь экран, меши и корень модели.
    void Prepare()
    {
        // Камера живёт в корне сцены: масштаб канваса не должен её трогать.
        if (mapCamera.transform.parent != null) mapCamera.transform.SetParent(null, false);
        if (image == null || image.width != Screen.width || image.height != Screen.height)
        {
            if (image != null)
            {
                image.Release();
                Destroy(image);
            }
            image = new RenderTexture(Screen.width, Screen.height, 24) { name = "Map view" };
            mapCamera.targetTexture = image;
            view.texture = image;
        }
        mapCamera.cullingMask = 1 << layer;
        mapCamera.nearClipPlane = modelSize * 0.001f;
        mapCamera.farClipPlane = modelSize * 10f;
        mapCamera.enabled = true;

        if (model == null)
        {
            model = new GameObject("Map model").transform;
            block = new MaterialPropertyBlock();
            sphere = PrimitiveMesh(PrimitiveType.Sphere);
            cube = PrimitiveMesh(PrimitiveType.Cube);
            arrow = ArrowMesh();
            // Пунктир: половина периода — штрих, половина — пусто (отсекается Alpha Clipping).
            dashTexture = new Texture2D(2, 1, TextureFormat.RGBA32, false) { name = "Map dash", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            dashTexture.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 0) });
            dashTexture.Apply();
        }
        model.gameObject.SetActive(true);
    }

    protected override void Refresh()
    {
        Build();

        ClearRows();
        FuelTank tank = navigator.GetComponent<FuelTank>();
        string target = navigator.Target != null ? navigator.Target.Label : "не выбрана";
        title.text = tank != null ? $"Карта | цель: {target} | топливо {Mathf.CeilToInt(tank.Fuel)} / {tank.capacity:0} л" : $"Карта | цель: {target}";

        AddRow($"Вид к машине ({Controls.Key(Controls.Use)})", () =>
        {
            ResetView();
            return null;
        });
        foreach (Location location in navigator.Sorted())
        {
            string line = $"{location.Label} | {KindName(location.kind)} | {Navigator.FormatDistance(navigator.DistanceTo(location))}";
            if (location == navigator.Target) line = "[цель] " + line;
            Location picked = location;
            AddRow(line, () => SetTarget(picked));
        }
        if (Location.All.Count == 0) AddRow("На карте нет ни одной точки");
    }

    string SetTarget(Location location)
    {
        navigator.Target = location;
        return $"Цель: {location.Label}";
    }

    // Мини-модель заново: масштаб, сетка, метки, подписи, нитки, маршруты.
    void Build()
    {
        foreach (Transform child in model) Destroy(child.gameObject);
        icons.Clear();
        labels.Clear();
        lines.Clear();
        picks.Clear();

        var bounds = new Bounds(navigator.transform.position, Vector3.zero);
        foreach (Location location in Location.All) bounds.Encapsulate(location.transform.position);
        center = bounds.center;
        scale = modelSize / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 100f);

        // Сетка под всем миром (gridY — уже в координатах карты).
        float gridY = ToMap(bounds.min).y - modelSize * 0.05f, half = modelSize * 0.6f;
        const int cells = 10;
        for (int i = 0; i <= cells; i++)
        {
            float t = Mathf.Lerp(-half, half, i / (float)cells);
            Line(new Vector3(t, gridY, -half), new Vector3(t, gridY, half), gridColor, false);
            Line(new Vector3(-half, gridY, t), new Vector3(half, gridY, t), gridColor, false);
        }

        foreach (Location location in Location.All)
        {
            bool isTarget = location == navigator.Target;
            Color color = isTarget ? targetColor : KindColor(location.kind);
            Vector3 p = ToMap(location.transform.position);
            Transform icon = location.kind switch
            {
                Location.Kind.Planet => Icon(sphere, p, Quaternion.identity, color, iconSize, PlanetRadius(location) * 2f * scale),
                Location.Kind.Station => Icon(cube, p, Quaternion.Euler(45f, 0f, 45f), color, iconSize * 0.8f, 0f),
                Location.Kind.FuelStop => Icon(cube, p, Quaternion.identity, color, iconSize * 0.7f, 0f),
                _ => Icon(sphere, p, Quaternion.identity, color, iconSize * 0.5f, 0f),
            };
            Label(icon, isTarget ? "[цель] " + location.Label : location.Label, color);
            Drop(p, gridY, color);
            picks.Add((location, icon));
        }

        Vector3 car = ToMap(navigator.transform.position);
        Label(Icon(arrow, car, navigator.transform.rotation, carColor, iconSize * 1.5f, 0f), "Машина", carColor);
        Drop(car, gridY, carColor);
        CosmoCarSeat seat = navigator.GetComponent<CosmoCarSeat>();
        if (seat != null && seat.PlayerBody != null)
        {
            Vector3 cat = ToMap(seat.PlayerBody.position);
            Label(Icon(sphere, cat, Quaternion.identity, catColor, iconSize * 0.6f, 0f), "Кот", catColor);
            Drop(cat, gridY, catColor);
        }

        // Маршруты заказов: от посылки (или почты выдачи) до почты-адресата.
        foreach (var (parcel, from, to) in OrderJournal.Active())
        {
            if (to == null || (parcel == null && from == null)) continue;
            Vector3 start = ToMap(parcel != null ? parcel.transform.position : from.transform.position);
            Icon(cube, start, Quaternion.identity, routeColor, iconSize * 0.4f, 0f);
            Line(start, ToMap(Spot(to)), routeColor, true);
        }
        if (navigator.Target != null) Line(car, ToMap(navigator.Target.transform.position), targetColor, true);
    }

    // Метр мира -> точка карты.
    Vector3 ToMap(Vector3 world) => (world - center) * scale + Vector3.up * modelHeight;

    // Где почта на карте: её Location (станция), а если нет — сам терминал.
    static Vector3 Spot(PostTerminal terminal)
    {
        Location location = terminal.GetComponentInParent<Location>();
        return location != null ? location.transform.position : terminal.transform.position;
    }

    // Радиус планеты, м: коллайдер, иначе гравитация, иначе Location.radius.
    static float PlanetRadius(Location location)
    {
        if (location.TryGetComponent(out Collider body) && !body.isTrigger) return Mathf.Max(body.bounds.extents.x, body.bounds.extents.y, body.bounds.extents.z);
        if (location.TryGetComponent(out SphereGravity gravity)) return gravity.radius;
        return location.radius;
    }

    Transform Icon(Mesh mesh, Vector3 position, Quaternion rotation, Color color, float size, float min)
    {
        var go = new GameObject("Icon", typeof(MeshFilter), typeof(MeshRenderer)) { layer = layer };
        go.transform.SetParent(model, false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        Paint(go.GetComponent<MeshRenderer>(), solidMaterial, color, null);
        icons.Add((go.transform, size, min));
        return go.transform;
    }

    // Подпись справа от метки, всегда лицом к камере карты.
    void Label(Transform icon, string text, Color color)
    {
        var go = new GameObject("Label") { layer = layer };
        go.transform.SetParent(model, false);
        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.font = title.font;
        label.fontSize = 10f;             // строка ~1 единица при масштабе 1
        label.color = color;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.pivot = new Vector2(0f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(40f, 1.5f);
        label.text = text;
        labels.Add((label, icon));
    }

    // Нитка от метки вниз до сетки.
    void Drop(Vector3 position, float gridY, Color color) =>
        Line(position, new Vector3(position.x, gridY, position.z), Color.Lerp(gridColor, color, 0.5f), false);

    void Line(Vector3 a, Vector3 b, Color color, bool dashed)
    {
        var go = new GameObject(dashed ? "Dash" : "Line") { layer = layer };
        go.transform.SetParent(model, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.textureMode = LineTextureMode.Tile;
        Paint(line, dashed ? dashMaterial : solidMaterial, color, dashed ? dashTexture : null);
        lines.Add((line, dashed));
    }

    void Paint(Renderer target, Material material, Color color, Texture texture)
    {
        target.sharedMaterial = material;
        target.shadowCastingMode = ShadowCastingMode.Off;
        target.receiveShadows = false;
        block.Clear();
        block.SetColor("_BaseColor", color);
        if (texture != null) block.SetTexture("_BaseMap", texture);
        target.SetPropertyBlock(block);
    }

    void ResetView()
    {
        pivot = ToMap(navigator.transform.position);
        distance = modelSize * 0.6f;
        yaw = navigator.transform.eulerAngles.y;
        pitch = 35f;
    }

    // Мышь: ЛКМ — вращать, ПКМ — сдвиг, колесо — масштаб, короткий клик ЛКМ — выбрать метку. Над панелью мышь не трогает вид.
    void Steer(Mouse mouse)
    {
        if (mouse == null) return;
        Vector2 position = mouse.position.ReadValue(), delta = mouse.delta.ReadValue();
        bool overPanel = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
        {
            grabbed = !overPanel;
            travel = 0f;
        }
        if (grabbed && mouse.leftButton.isPressed)
        {
            yaw += delta.x * 0.3f;
            pitch = Mathf.Clamp(pitch - delta.y * 0.3f, -89f, 89f);
            travel += delta.magnitude;
        }
        if (grabbed && mouse.rightButton.isPressed)
        {
            // Точка под курсором едет вместе с мышью.
            float perPixel = 2f * distance * Mathf.Tan(mapCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            Transform c = mapCamera.transform;
            pivot -= (c.right * delta.x + c.up * delta.y) * perPixel;
        }
        if (grabbed && mouse.leftButton.wasReleasedThisFrame && travel < 6f) Pick(position);

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f && !overPanel) distance = Mathf.Clamp(distance * (scroll > 0f ? 0.85f : 1f / 0.85f), modelSize * 0.03f, modelSize * 4f);
    }

    // Геймпад: правый стик вращает, триггеры приближают и отдаляют. Время стоит — по неигровому времени.
    void Pad()
    {
        if (!Controls.UsingPad) return; // Space и C клавиатуры карту не трогают
        float dt = Time.unscaledDeltaTime;
        Vector2 stick = Controls.Stick.ReadValue<Vector2>();
        yaw += stick.x * 120f * dt;
        pitch = Mathf.Clamp(pitch - stick.y * 90f * dt, -89f, 89f);
        float zoom = Controls.Up.ReadValue<float>() - Controls.Down.ReadValue<float>();
        distance = Mathf.Clamp(distance * Mathf.Pow(0.3f, zoom * dt), modelSize * 0.03f, modelSize * 4f);
    }

    // Ближайшая к курсору метка на экране; планету можно взять за любую точку диска.
    void Pick(Vector2 mouse)
    {
        Location best = null;
        float bestDistance = float.MaxValue;
        float pixelsPerUnit = Screen.height / (2f * Mathf.Tan(mapCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        foreach (var (location, icon) in picks)
        {
            Vector3 v = mapCamera.WorldToViewportPoint(icon.position);
            if (v.z <= 0f) continue;
            float d = Vector2.Distance(mouse, new Vector2(v.x * Screen.width, v.y * Screen.height));
            float reach = Mathf.Max(24f, icon.localScale.x * 0.5f * pixelsPerUnit / v.z);
            if (d < reach && d < bestDistance)
            {
                best = location;
                bestDistance = d;
            }
        }
        if (best == null) return;
        status.text = SetTarget(best);
        Refresh();
    }

    // Камера на орбите вокруг pivot; метки, подписи и линии — постоянного экранного размера.
    void Place()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Transform c = mapCamera.transform;
        c.SetPositionAndRotation(pivot + rotation * new Vector3(0f, 0f, -distance), rotation);
        Vector3 eye = c.position;

        foreach (var (icon, size, min) in icons)
            icon.localScale = Vector3.one * Mathf.Max(min, size * Vector3.Distance(eye, icon.position));
        foreach (var (text, icon) in labels)
        {
            float d = Vector3.Distance(eye, icon.position);
            text.transform.SetPositionAndRotation(icon.position + c.right * (icon.localScale.x * 0.6f + d * 0.005f), c.rotation);
            text.transform.localScale = Vector3.one * (labelSize * d);
        }
        foreach (var (line, dashed) in lines)
        {
            float width = lineWidth * Vector3.Distance(eye, (line.GetPosition(0) + line.GetPosition(1)) * 0.5f);
            line.widthMultiplier = width;
            if (dashed) line.textureScale = new Vector2(1f / (width * 6f), 1f); // период пунктира — 6 толщин
        }
    }

    Color KindColor(Location.Kind kind) => kind switch
    {
        Location.Kind.Station => stationColor,
        Location.Kind.FuelStop => fuelStopColor,
        Location.Kind.Planet => planetColor,
        _ => findColor,
    };

    static string KindName(Location.Kind kind) => kind switch
    {
        Location.Kind.Station => "станция",
        Location.Kind.FuelStop => "заправка",
        Location.Kind.Planet => "планета",
        _ => "находка",
    };

    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
        Destroy(go);
        return mesh;
    }

    // Стрелка машины: четырёхгранная пирамида остриём в +Z, размер ~1.
    static Mesh ArrowMesh()
    {
        var mesh = new Mesh { name = "Map arrow" };
        mesh.vertices = new[]
        {
            new Vector3(0f, 0f, 0.6f),
            new Vector3(-0.4f, -0.2f, -0.4f), new Vector3(0.4f, -0.2f, -0.4f),
            new Vector3(0.4f, 0.2f, -0.4f), new Vector3(-0.4f, 0.2f, -0.4f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1, 1, 3, 2, 1, 4, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
