using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

// Опасный переход: охотник кредиторов, один на сцену. Боя нет — только бегство.
// Раз в checkInterval, если машина на дальнем маршруте (дальше farFrom от станций, заправок и планет), а давление долга
// или дурная слава (факт errand.heat) достаточно высоки, кредиторы могут послать охотника: он появляется позади, вне видимости.
// Охотник летит на перехват и держится позади. Он быстрее форсажа, но медленнее маршевого — уходят на маршевом, топливо решает.
// Уйти можно: оторваться дальше loseDistance; долететь до укрытия (Shelter — станции и поселения), там он сразу отворачивает;
// спрятаться в облаке обломков (находка Debris) на малой скорости; переждать в атмосфере планеты или у заправки —
// туда он не залетает, ждёт поодаль и уходит. Догнал (catchTime секунд ближе catchRadius) — мнёт корпус, сливает топливо,
// иногда бьёт хрупкую посылку на полке, и улетает. При давлении от deadlyPressure поимка — финал «Окончательная гибель».
// После погони — передышка cooldown; она пишется в сохранение, так что после загрузки охотник сразу не появится.
// На экране во время погони — строка: расстояние и сторона, где охотник. Тексты — временные заглушки.
//
// Факты:
//   threat.seen     — сколько раз появлялся охотник
//   threat.escaped  — ушёл от охотника: оторвался, укрылся на станции, переждал в атмосфере или у заправки
//   threat.hidden   — охотник потерял машину в облаке обломков
//   threat.caught   — охотник догнал (пакость или финал)
public class Threat : MonoBehaviour, ISaveable
{
    public ThreatSettings settings;
    public GameObject pursuerPrefab;      // корабль-охотник, только вид: без коллайдеров
    public Canvas hud;                    // метка охотника: видна во время погони за рулём
    public TMP_Text label;

    enum Phase { None, Chase, Leave }

    Phase phase;
    Transform ship;
    Vector3 velocity;
    float cooldown, roll, catchTimer, coverTimer, leaveTimer;
    CosmoCarController car;
    Rigidbody carBody;
    readonly List<(Transform center, float radius)> blocks = new(); // куда охотник не залетает

    void Start()
    {
        car = FindFirstObjectByType<CosmoCarController>();
        if (car != null) carBody = car.GetComponent<Rigidbody>();
        foreach (SphereGravity planet in FindObjectsByType<SphereGravity>(FindObjectsSortMode.None)) blocks.Add((planet.transform, planet.outerRadius + 20f));
        foreach (Shelter shelter in Shelter.All) blocks.Add((shelter.transform, shelter.radius));
        foreach (Location l in Location.All)
            if (l.kind == Location.Kind.FuelStop) blocks.Add((l.transform, settings.fuelStopZone));
        cooldown = settings.firstDelay;
        roll = settings.checkInterval;
        hud.enabled = false;
    }

    void Update()
    {
        if (car == null) return;
        float dt = Time.deltaTime;
        if (phase == Phase.None) Roll(dt);
        else if (phase == Phase.Chase) Chase(dt);
        else if ((leaveTimer -= dt) > 0f) Steer(ship.position + (ship.position - carBody.position).normalized * 1000f, Vector3.zero, 0f, dt);
        else
        {
            phase = Phase.None;
            ship.gameObject.SetActive(false);
        }
        hud.enabled = phase == Phase.Chase && car.enabled && MenuScreen.Opened == null;
    }

    // Решение кредиторов: только на дальнем маршруте, за рулём и при долге или дурной славе.
    void Roll(float dt)
    {
        if ((cooldown -= dt) > 0f || (roll -= dt) > 0f) return;
        roll = settings.checkInterval;
        if (!car.enabled || pursuerPrefab == null) return;
        foreach (Location l in Location.All)
            if (l.kind != Location.Kind.Find && Vector3.Distance(l.transform.position, carBody.position) < settings.farFrom + l.radius) return;

        Debt debt = Debt.Current;
        float pressure = debt != null && debt.Active ? debt.Pressure : 0f;
        int heat = StoryFacts.Get("errand.heat");
        float chance = 0f;
        if (pressure >= settings.minPressure) chance += Mathf.Lerp(settings.chance, settings.chanceAtMax, Mathf.InverseLerp(settings.minPressure, 100f, pressure));
        if (heat >= settings.minHeat) chance += settings.heatChance * heat;
        if (Random.value < chance) Spawn(pressure >= settings.deadlyPressure);
    }

    // Позади камеры, на spawnDistance: игрок его не видит, пока не обернётся.
    void Spawn(bool deadly)
    {
        if (ship == null) ship = Instantiate(pursuerPrefab).transform;
        Camera cam = Camera.main;
        Vector3 back = -(cam != null ? cam.transform.forward : car.transform.forward);
        Vector3 position = carBody.position + (back + Random.insideUnitSphere * 0.3f).normalized * settings.spawnDistance;
        ship.SetPositionAndRotation(position, Quaternion.LookRotation(carBody.position - position));
        ship.gameObject.SetActive(true);
        velocity = ship.forward * (settings.speed * 0.5f);
        Avoid();
        phase = Phase.Chase;
        catchTimer = coverTimer = 0f;
        StoryFacts.Add("threat.seen");
        Hud.Toast(deadly
            ? "Сзади чужой корабль, огни красные. Это уже не сборщик долгов. Не дай ему подойти."
            : "Сзади чужой корабль: охотник кредиторов. Уходи на маршевом, к станции или в обломки.");
    }

    void Chase(float dt)
    {
        Vector3 target = carBody.position;
        float distance = Vector3.Distance(ship.position, target);
        if (Shelter.Covers(target))
        {
            End("Охотник отвернул: у станции слишком много свидетелей.", "threat.escaped");
            return;
        }
        if (distance > settings.loseDistance)
        {
            End("Охотник отстал. Оторвался.", "threat.escaped");
            return;
        }

        // Укрытие: обломки на малой скорости или зона, куда он не залетает (атмосфера, заправка).
        bool debris = carBody.linearVelocity.magnitude < settings.hideSpeed
                      && Find.All.Exists(f => f.kind == Find.Kind.Debris && Vector3.Distance(f.transform.position, target) < f.range);
        bool blocked = blocks.Exists(b => (b.center.position - target).sqrMagnitude < b.radius * b.radius);
        bool cover = debris || blocked;
        coverTimer = cover ? coverTimer + dt : 0f;
        if (debris && coverTimer >= settings.hideTime)
        {
            End("Охотник потерял тебя среди обломков.", "threat.hidden");
            return;
        }
        if (blocked && coverTimer >= settings.waitTime)
        {
            End("Охотник покружил поодаль и ушёл ни с чем.", "threat.escaped");
            return;
        }

        catchTimer = !cover && distance < settings.catchRadius ? catchTimer + dt : Mathf.Max(0f, catchTimer - dt);
        if (catchTimer >= settings.catchTime)
        {
            Caught();
            return;
        }

        // Целится в точку позади машины; пока она укрыта — держится поодаль.
        Vector3 v = carBody.linearVelocity;
        Vector3 behind = v.sqrMagnitude > 1f ? -v.normalized : -car.transform.forward;
        Steer(target + behind * (settings.catchRadius * 0.5f), v, cover ? settings.searchDistance : 0f, dt);

        string state = catchTimer > 0f ? "догоняет! уходи на маршевом (Shift+Space)"
                     : cover ? "ищет тебя... не высовывайся"
                     : "уходи: маршевый, станция или облако обломков";
        label.text = $"ОХОТНИК | {Navigator.FormatDistance(distance)} | {Side()}\n{state}";
    }

    void Caught()
    {
        StoryFacts.Add("threat.caught");
        Debt debt = Debt.Current;
        if (debt != null && debt.Active && debt.Pressure >= settings.deadlyPressure && debt.ending != null)
        {
            phase = Phase.None;
            ship.gameObject.SetActive(false);
            debt.ending.Open(false);
            return;
        }

        if (car.TryGetComponent(out Hull hull)) hull.Damage(settings.hullDamage);
        FuelTank tank = car.GetComponent<FuelTank>();
        tank.TryUse(tank.Fuel * settings.fuelDrain);
        string text = $"Охотник догнал: помял корпус и слил {Mathf.RoundToInt(settings.fuelDrain * 100f)} % топлива.";
        if (Random.value < settings.parcelChance)
        {
            List<Parcel> fragile = Parcel.All.FindAll(p => p.Fragile && p.Slot != null && p.Slot.transform.IsChildOf(car.transform));
            if (fragile.Count > 0)
            {
                Parcel parcel = fragile[Random.Range(0, fragile.Count)];
                parcel.condition = Mathf.Max(0f, parcel.condition - settings.parcelLoss);
                text += $" «{parcel.order.cargo}» слетела с полки: сохранность {Parcel.Percent(parcel.condition)}.";
            }
        }
        End(text + " «Передай: ждём платёж».", null);
    }

    // Погоня кончилась: охотник улетает, потом передышка.
    void End(string text, string fact)
    {
        if (fact != null) StoryFacts.Add(fact);
        Hud.Toast(text);
        phase = Phase.Leave;
        leaveTimer = settings.leaveTime;
        cooldown = settings.cooldown;
        catchTimer = coverTimer = 0f;
    }

    // Скорость машины (follow) плюс сближение с целью, пока до неё дальше hold; не больше speed.
    void Steer(Vector3 goal, Vector3 follow, float hold, float dt)
    {
        Vector3 to = goal - ship.position;
        float d = to.magnitude;
        Vector3 approach = to / Mathf.Max(d, 0.01f) * Mathf.Clamp((d - hold) * 0.5f, -settings.speed, settings.speed);
        Vector3 desired = Vector3.ClampMagnitude(follow + approach, settings.speed);
        velocity = Vector3.MoveTowards(velocity, desired, settings.acceleration * dt);
        ship.position += velocity * dt;
        Avoid();
        if (velocity.sqrMagnitude > 1f) ship.rotation = Quaternion.Slerp(ship.rotation, Quaternion.LookRotation(velocity), 3f * dt);
    }

    // Планеты с атмосферой, укрытия и заправки: охотник скользит по краю их сферы, а не влетает внутрь.
    void Avoid()
    {
        foreach (var (center, radius) in blocks)
        {
            Vector3 offset = ship.position - center.position;
            float d = offset.magnitude, near = radius + settings.speed; // начинает отворачивать за секунду полёта до края
            if (d > near || d < 0.01f) continue;
            Vector3 n = offset / d;
            if (d < radius) ship.position = center.position + n * radius;
            float inward = Vector3.Dot(velocity, n);
            if (inward < 0f) velocity -= n * (inward * Mathf.InverseLerp(near, radius, d));
        }
    }

    // Где охотник относительно камеры: по самой большой оси.
    string Side()
    {
        Transform view = Camera.main != null ? Camera.main.transform : car.transform;
        Vector3 p = view.InverseTransformPoint(ship.position);
        float x = Mathf.Abs(p.x), y = Mathf.Abs(p.y), z = Mathf.Abs(p.z);
        if (z >= x && z >= y) return p.z > 0f ? "впереди" : "сзади";
        if (x >= y) return p.x > 0f ? "справа" : "слева";
        return p.y > 0f ? "сверху" : "снизу";
    }

    // Пишется только передышка. Шла погоня — после загрузки охотник появится не раньше, чем через cooldown.
    public string SaveKey => "threat";
    public string Save() => (phase == Phase.Chase ? Mathf.Max(cooldown, settings.cooldown) : cooldown).ToString(CultureInfo.InvariantCulture);
    public void Load(string data) => cooldown = float.Parse(data, CultureInfo.InvariantCulture);
}
