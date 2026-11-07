using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Передвижная заправка: платформа медленно идёт по замкнутому маршруту (route, точки мира) с постоянной скоростью,
// не поворачиваясь. Всё свободное над палубой (машина, кот, посылки — в зоне deck) едет вместе с ней:
// их сдвигает на тот же шаг, так что парковка, магнит, гравитация палубы и ходьба работают как на неподвижной станции.
// Кто влетел в зону быстрее 1 м/с, получает скорость в системе платформы (минус её скорость), кто покинул — обратно;
// кто появился внутри (кот вышел из машины, буксир, загрузка) — едет как есть.
// Location на том же объекте — точка на карте следует за платформой. Сохраняет пройденный путь.
[RequireComponent(typeof(Rigidbody), typeof(Location))]
public class FuelBarge : MonoBehaviour, ISaveable
{
    public Vector3[] route;               // точки маршрута в мире, по кругу
    public float speed = 6f;              // м/с
    public Vector3 deck = new(16f, 20f, 32f); // зона попутчиков: ширина, высота над палубой, длина, м

    public Vector3 Velocity { get; private set; }

    Rigidbody rb;
    float along;                          // пройдено по маршруту, м
    float length;
    readonly HashSet<Rigidbody> aboard = new(), now = new();
    readonly Collider[] hits = new Collider[64];

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        length = 0f;
        for (int i = 0; i < route.Length; i++) length += Vector3.Distance(route[i], route[(i + 1) % route.Length]);
        Snap();
    }

    // Точка маршрута на расстоянии d от первой.
    Vector3 PointAt(float d)
    {
        if (route.Length == 0) return transform.position;
        if (length <= 0f) return route[0];
        d = Mathf.Repeat(d, length);
        for (int i = 0; ; i++)
        {
            Vector3 a = route[i % route.Length], b = route[(i + 1) % route.Length];
            float segment = Vector3.Distance(a, b);
            if (d <= segment || i >= route.Length) return Vector3.Lerp(a, b, segment > 0f ? d / segment : 0f);
            d -= segment;
        }
    }

    void Snap()
    {
        Vector3 p = PointAt(along);
        transform.position = p;
        rb.position = p;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        along = Mathf.Repeat(along + speed * dt, Mathf.Max(length, 1f));
        Vector3 target = PointAt(along);
        Vector3 delta = target - rb.position;
        Velocity = delta / dt;
        rb.MovePosition(target);

        // Попутчики: всё подвижное в зоне над палубой.
        now.Clear();
        Vector3 center = rb.position + transform.up * (deck.y * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(center, deck * 0.5f, hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Rigidbody body = hits[i].attachedRigidbody;
            if (body == null || body.isKinematic || !now.Add(body)) continue;
            if (!aboard.Contains(body) && body.linearVelocity.magnitude > 1f) body.linearVelocity -= Velocity;
            body.position += delta;
        }

        // Сошедшие рядом получают скорость платформы; унесённые далеко (буксир) — нет.
        foreach (Rigidbody body in aboard)
            if (body != null && !now.Contains(body) && (body.position - rb.position).magnitude < deck.magnitude)
                body.linearVelocity += Velocity;
        aboard.Clear();
        aboard.UnionWith(now);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * (deck.y * 0.5f), deck);
        Gizmos.matrix = Matrix4x4.identity;
        if (route == null) return;
        for (int i = 0; i < route.Length; i++) Gizmos.DrawLine(route[i], route[(i + 1) % route.Length]);
    }

    public string SaveKey => "barge:" + GetComponent<Location>().id;
    public string Save() => along.ToString(CultureInfo.InvariantCulture);

    public void Load(string data)
    {
        along = float.Parse(data, CultureInfo.InvariantCulture);
        Snap();
        aboard.Clear();
    }
}
