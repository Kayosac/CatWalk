using CatWalk.Rendering;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Полётный HUD: скорость, цель и расстояние до неё, маркер цели на экране, состояние маршевого,
// а когда тянет парковка — расстояние до площадки, скорость и «магнит / стоит».
// Виден только за рулём и без открытого меню. Компоновка — в префабе FlightHud, здесь только значения.
[RequireComponent(typeof(Canvas))]
public class FlightHud : MonoBehaviour
{
    public CosmoCarController car;        // по умолчанию — космокар в сцене
    public TMP_Text speed;
    public TMP_Text target;

    [Header("Маршевый (полоска)")]
    public Image cruiseFill;
    public TMP_Text cruiseLabel;
    public Color cruiseReady = new(0.35f, 0.85f, 1f);
    public Color cruiseCharging = new(1f, 0.75f, 0.3f);

    [Header("Маркер цели")]
    public RectTransform marker;          // рамка у цели, когда она на экране
    public TMP_Text markerLabel;
    public RectTransform edgeArrow;       // у края экрана, когда цель за ним; ось +Y смотрит на цель
    public float edgeMargin = 48f;        // отступ стрелки от края экрана, px при эталонном разрешении

    [Header("Парковка (полоска)")]
    public GameObject parking;            // корень полоски, виден, пока тянет площадка
    public Image parkingFill;             // заполняется по мере приближения к площадке
    public TMP_Text parkingLabel;

    Canvas canvas;
    Navigator navigator;
    CruiseDrive cruise;
    Rigidbody body;
    ParkingZone[] zones;

    void Start()
    {
        canvas = GetComponent<Canvas>();
        if (car == null) car = FindFirstObjectByType<CosmoCarController>();
        if (car != null)
        {
            navigator = car.GetComponent<Navigator>();
            cruise = car.GetComponent<CruiseDrive>();
            body = car.GetComponent<Rigidbody>();
        }
        zones = FindObjectsByType<ParkingZone>(FindObjectsSortMode.None);
    }

    void LateUpdate()
    {
        bool visible = car != null && car.enabled && MenuScreen.Opened == null;
        canvas.enabled = visible;
        if (!visible) return;

        float velocity = body.linearVelocity.magnitude;
        speed.text = $"{velocity:0.0} м/с";

        Location goal = navigator.Target;
        target.text = goal != null ? $"Цель: {goal.Label} | {Navigator.FormatDistance(navigator.DistanceTo(goal))}"
                                   : "Цель не выбрана | Tab карта, T следующая";

        string blocker = cruise.Blocker();
        cruiseFill.rectTransform.anchorMax = new Vector2(cruise.SpinningUp ? cruise.Progress : blocker == null ? 1f : 0f, 1f);
        cruiseFill.color = cruise.SpinningUp ? cruiseCharging : cruiseReady;
        cruiseLabel.text = cruise.SpinningUp ? "Маршевый: разгон" : blocker == null ? "Маршевый готов | J" : "Маршевый: " + blocker;

        Marker(goal);
        Parking(velocity);
    }

    void Marker(Location goal)
    {
        Camera cam = Camera.main;
        marker.gameObject.SetActive(false);
        edgeArrow.gameObject.SetActive(false);
        if (goal == null || cam == null) return;

        // Мир может рисоваться в уменьшенном кадре с полями (WorldResolution) — переводим в пиксели экрана сами.
        Rect frame = cam.TryGetComponent(out WorldResolution world) && world.IsActive
            ? world.Settings.OutputRect(Screen.width, Screen.height)
            : new Rect(0f, 0f, Screen.width, Screen.height);
        Vector3 v = cam.WorldToViewportPoint(goal.transform.position);
        Vector2 point = frame.position + Vector2.Scale(v, frame.size);

        if (v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f)
        {
            marker.gameObject.SetActive(true);
            marker.position = point;
            markerLabel.text = $"{goal.Label} | {Navigator.FormatDistance(navigator.DistanceTo(goal))}";
            return;
        }

        // Цель за краем или позади камеры: стрелка на краю кадра в её сторону.
        Vector2 direction = point - frame.center;
        if (v.z < 0f) direction = -direction;
        if (direction.sqrMagnitude < 1e-4f) direction = Vector2.down;
        Vector2 half = frame.size * 0.5f - Vector2.one * (edgeMargin * canvas.scaleFactor);
        float scale = Mathf.Min(half.x / Mathf.Abs(direction.x), half.y / Mathf.Abs(direction.y));
        edgeArrow.gameObject.SetActive(true);
        edgeArrow.position = frame.center + direction * scale;
        edgeArrow.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, direction));
    }

    // Ближайшая площадка из тех, что сейчас тянут машину.
    void Parking(float velocity)
    {
        ParkingZone zone = null;
        float distance = float.MaxValue;
        foreach (ParkingZone z in zones)
        {
            if (z == null || !z.IsPulling) continue;
            float d = Vector3.Distance(body.position, z.transform.position);
            if (d < distance)
            {
                zone = z;
                distance = d;
            }
        }

        parking.SetActive(zone != null);
        if (zone == null) return;
        parkingFill.rectTransform.anchorMax = new Vector2(1f - Mathf.Clamp01(distance / zone.radius), 1f);
        parkingLabel.text = $"Парковка {distance:0.0} м | {velocity:0.0} м/с | {(zone.IsDocked ? "стоит" : "магнит")}";
    }
}
