using UnityEngine;
using UnityEngine.InputSystem;

// Маршевый перелёт к цели навигатора. J за рулём — разгон: тяга игрока работает, на машине горит эффект.
// Через spinUpTime машина мгновенно встаёт у цели носом к ней, топливо списывается разом.
// Если на пути что-то есть — прибывает перед препятствием. Повторное J или пропавшее условие — отмена.
// Стоимость: (baseCost + d × costPerMeter / экономичность) × costScale литров.
[RequireComponent(typeof(Navigator), typeof(CosmoCarController), typeof(CarUpgrades))]
public class CruiseDrive : MonoBehaviour
{
    public float minDistance = 500f;      // ближе маршевый не нужен, м
    public float spinUpTime = 1.5f;       // разгон, с
    public float arrivalDistance = 200f;  // прибытие от края цели (Location.radius), если у неё нет точки arrival, м
    public float pathRadius = 3f;         // толщина проверки пути, м
    public float obstacleMargin = 30f;    // насколько раньше препятствия встаём, м

    [Header("Топливо, л")]
    public float baseCost = 6f;
    public float costPerMeter = 0.018f;
    public float costScale = 0.65f;

    [Header("Эффекты")]
    public ParticleSystem spinUpVfx;      // на машине, пока идёт разгон
    public ParticleSystem arrivalVfx;     // вспышка на машине после прибытия

    public bool SpinningUp => spinUp >= 0f;
    public float Progress => SpinningUp ? spinUp / spinUpTime : 0f;
    public float Range => upgrades.CruiseRange;

    Navigator navigator;
    CosmoCarController car;
    FuelTank tank;
    CarUpgrades upgrades;
    Rigidbody body;
    ParkingZone[] zones;
    float spinUp = -1f;                   // сколько уже разгоняемся, с; меньше нуля — не разгоняемся

    void Awake()
    {
        navigator = GetComponent<Navigator>();
        car = GetComponent<CosmoCarController>();
        tank = GetComponent<FuelTank>();
        upgrades = GetComponent<CarUpgrades>();
        body = GetComponent<Rigidbody>();
    }

    void Start()
    {
        zones = FindObjectsByType<ParkingZone>(FindObjectsSortMode.None);
        if (spinUpVfx != null) spinUpVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (arrivalVfx != null) arrivalVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public float Cost(float distance) => (baseCost + distance * costPerMeter / tank.efficiency) * costScale;

    // Почему сейчас нельзя лететь (для сообщения и HUD); null — можно.
    public string Blocker()
    {
        if (!car.enabled) return "только за рулём";
        Location target = navigator.Target;
        if (target == null) return "нет цели — Tab карта, T следующая";
        foreach (ParkingZone zone in zones)
            if (zone != null && zone.IsDocked) return "машина на парковке — сначала взлети";
        float distance = navigator.DistanceTo(target);
        if (distance <= minDistance) return $"цель ближе {minDistance:0} м — долетишь сам";
        if (distance > Range) return $"до цели {Navigator.FormatDistance(distance)}, двигатель берёт {Navigator.FormatDistance(Range)}";
        float cost = Cost(distance);
        if (cost > tank.Fuel) return $"нужно {cost:0.#} л топлива, в баке {tank.Fuel:0.#} л";
        return null;
    }

    void Update()
    {
        Keyboard k = Keyboard.current;
        bool pressed = k != null && MenuScreen.Opened == null && k.jKey.wasPressedThisFrame;

        if (!SpinningUp)
        {
            if (!pressed) return;
            string blocker = Blocker();
            if (blocker != null)
            {
                Hud.Toast("Маршевый недоступен: " + blocker);
                return;
            }
            spinUp = 0f;
            if (spinUpVfx != null) spinUpVfx.Play(true);
            return;
        }

        if (pressed)
        {
            Cancel("Маршевый отменён");
            return;
        }
        string lost = Blocker();
        if (lost != null)
        {
            Cancel("Маршевый прерван: " + lost);
            return;
        }

        Hud.Prompt("Маршевый: разгон | J — отмена", 15);
        spinUp += Time.deltaTime;
        if (spinUp >= spinUpTime) Jump();
    }

    void Cancel(string message)
    {
        spinUp = -1f;
        if (spinUpVfx != null) spinUpVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Hud.Toast(message);
    }

    void Jump()
    {
        spinUp = -1f;
        if (spinUpVfx != null) spinUpVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Location target = navigator.Target;
        Vector3 center = target.transform.position;
        Vector3 from = body.position;
        Vector3 point = target.arrival != null ? target.arrival.position
                      : center + (from - center).normalized * (target.radius + arrivalDistance);

        // Путь проверяем толстым лучом; что-то на пути (кроме самой машины) — встаём перед ним.
        Vector3 path = point - from;
        float length = Mathf.Max(path.magnitude, 0.01f);
        float blocked = length;
        foreach (RaycastHit hit in Physics.SphereCastAll(from, pathRadius, path / length, length, ~0, QueryTriggerInteraction.Ignore))
            if (hit.rigidbody != body && hit.distance > 0f) blocked = Mathf.Min(blocked, hit.distance - obstacleMargin);

        float distance = navigator.DistanceTo(target);
        float cost = Cost(distance);
        if (blocked < length)
        {
            if (blocked < minDistance)
            {
                Hud.Toast("Маршевый: путь перекрыт — облети препятствие");
                return;
            }
            point = from + path / length * blocked;
            cost = Cost(blocked);
        }
        if (!tank.TryUse(cost)) return;

        // Нос на цель; верх — как у точки прибытия или как был.
        Vector3 up = target.arrival != null ? target.arrival.up : transform.up;
        Vector3 look = center - point;
        if (Vector3.Cross(look, up).sqrMagnitude < 1e-4f) up = transform.forward;
        car.Teleport(point, Quaternion.LookRotation(look, up));
        if (arrivalVfx != null) arrivalVfx.Play(true);

        Hud.Toast(blocked < length
            ? $"Маршевый: препятствие на пути — до цели {Navigator.FormatDistance(navigator.DistanceTo(target))}"
            : $"Маршевый: прибыли к «{target.Label}» | −{cost:0.#} л");
    }
}
