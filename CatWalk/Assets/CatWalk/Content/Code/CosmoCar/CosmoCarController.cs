using UnityEngine;

// Физичный космокар на Rigidbody. Тяга и сцепление — силами в осях машины, поворот — моментом.
// Горизонта нет: машина держит ту ориентацию, в которой её оставили.
// ПКМ зажата — доворачивает за прицелом камеры. Иначе A/D поворачивают вокруг собственной оси «вверх».
// В повороте машина кренится внутрь. После сильного удара остаётся там, куда её развернуло.
// Гравитация планет и станций на машину не действует — магнитят её только парковки (ParkingZone).
// В атмосфере планеты машина летит вдоль неё: тяга не уводит от поверхности, а движение загибается
// по её изгибу, так что W ведёт по кругу на той же высоте. Высоту меняют только Space/C.
// Тяга маршевого атмосферу пересиливает: пока он тянет, машина летит прямо и легко уходит от планеты.
// Машина сама ложится крышей от планеты: у поверхности — жёстко, к краю атмосферы всё мягче.
// F — медленно выровняться, сохранив курс: на парковке — по ней, в атмосфере — по планете, иначе по мировому верху.
// Тяга тратит топливо (FuelTank): на резерве она слабее и без форсажа, в пустом баке двигатели молчат.
// Маршевый — следующая ступень после форсажа: с зажатым Shift нажать Space. Держится, пока зажат Shift;
// повторный Space или удар — выключает. W тянет намного сильнее при малом сопротивлении и жжёт втрое больше форсажа,
// остальная тяга как обычно, Space не поднимает. На резерве и с побитым корпусом (thrustScale < 1) недоступен.
// Клавиши — действия Controls (Input/); на геймпаде стик и триггеры аналоговые. Пока открыто меню, тяги нет.
[RequireComponent(typeof(Rigidbody), typeof(FuelTank))]
public class CosmoCarController : MonoBehaviour
{
    public CosmoCarCamera aim;            // по умолчанию — камера в сцене

    [Header("Тяга, м/с²")]
    public float thrust = 20f;
    public float reverseThrust = 10f;
    public float strafeThrust = 12f;
    public float liftThrust = 12f;
    public float boostMultiplier = 2.5f;  // Shift
    public float thrustScale = 1f;        // доля тяги, которую дают двигатели: её снижает, например, побитый корпус

    [Header("Маршевый (Shift+Space)")]
    public float cruiseThrust = 75f;      // тяга вперёд, м/с²: предел скорости — cruiseThrust / cruiseDamping
    public float cruiseDamping = 0.33f;   // сопротивление: разгон до предела ~3 / cruiseDamping с
    public float cruiseFuel = 3f;         // расход на тягу вперёд относительно форсажа
    public float cruiseScale = 1f;        // множитель тяги маршевого (и его скорости): улучшение двигателя (CarUpgrades)

    [Header("Инерция")]
    public float linearDamping = 1f;      // общее сопротивление: меньше — дольше катится вперёд
    public float sideGrip = 4f;           // гасит снос вбок и вверх-вниз, когда по этой оси нет тяги

    [Header("Поворот")]
    public float yawRate = 1.5f;          // A/D, рад/с
    public float maxBank = 20f;           // крен на полном повороте, °
    public float turnStiffness = 5f;      // сила доворота к цели
    public float turnDamping = 6f;        // гасит раскачку
    public float maxTurnRate = 2.5f;      // рад/с
    public float knockTime = 0.5f;        // сколько после удара машина «отпускает» прежнюю ориентацию, с
    public float knockImpulse = 3f;       // удар сильнее этого (изменение скорости, м/с) считается толчком
    public float levelSpeed = 30f;        // скорость выравнивания по F, °/с

    [Header("Атмосфера планет")]
    public float atmosphereAlignSpeed = 360f; // разворот крышей от планеты у поверхности, °/с; к краю атмосферы — до нуля

    Rigidbody rb;
    FuelTank fuel;
    Quaternion hold;                      // ориентация, которую машина держит (без крена)
    float knockTimer;
    bool leveling;
    bool steering;
    SphereGravity planet;                 // в чьей атмосфере были на прошлом шаге
    Vector3 planetUp;
    Quaternion transport = Quaternion.identity; // поворот вокруг планеты, ещё не забранный камерой
    Vector3 parkingUp;                    // «верх» парковки, которая разворачивает машину
    float parkingTime = -1f;
    bool spaceLatch;                      // Space переключил маршевый: пока его не отпустят, он не поднимает машину

    public Quaternion HoldRotation => hold;
    public bool Cruising { get; private set; }
    public Vector3 Thrust { get; private set; } // ввод тяги на этом шаге: x — вбок, y — вверх, z — вперёд (−1..1); для огней и VFX (CosmoCarVisuals)
    public bool Boosting { get; private set; }
    public bool CanCruise => !fuel.OnReserve && thrustScale >= 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        fuel = GetComponent<FuelTank>();
        rb.useGravity = false;
        rb.mass = 800f;
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        hold = rb.rotation;
        if (aim == null) aim = FindFirstObjectByType<CosmoCarCamera>();
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        rb.linearDamping = Cruising ? cruiseDamping : linearDamping;

        // Без топлива ввода тяги нет — остаётся только гашение сноса.
        // Пока открыто меню (колонка у парковки), стик и клавиши заняты им.
        float power = MenuScreen.Opened == null ? fuel.Power * thrustScale : 0f;
        float forwardIn = power > 0f ? Controls.Forward.ReadValue<float>() : 0f;
        float strafeIn = power > 0f ? Controls.Strafe.ReadValue<float>() : 0f;
        float liftIn = power > 0f ? (!Cruising && !spaceLatch ? Controls.Up.ReadValue<float>() : 0f) - Controls.Down.ReadValue<float>() : 0f;
        bool boost = Controls.Boost.IsPressed() && !fuel.OnReserve;
        bool cruise = Cruising && forwardIn > 0f;
        Thrust = new Vector3(strafeIn, liftIn, forwardIn);
        Boosting = boost && forwardIn > 0f;
        float forwardAccel = (forwardIn > 0f ? cruise ? cruiseThrust * cruiseScale : thrust * (boost ? boostMultiplier : 1f) : reverseThrust) * power;
        float strafeAccel = strafeThrust * power;
        float liftAccel = liftThrust * power;
        float forwardBurn = cruise ? boostMultiplier * cruiseFuel * power : forwardAccel / thrust;
        fuel.Burn(Mathf.Abs(forwardIn) * forwardBurn + (Mathf.Abs(strafeIn) * strafeAccel + Mathf.Abs(liftIn) * liftAccel) / thrust, dt);

        // Атмосфера: машина поворачивается на столько, на сколько сместился «верх» планеты, — без отставания,
        // иначе нос задирается над горизонтом и тяга уводит от поверхности.
        // Маршевый тянет сильнее атмосферы: под его тягой машина летит как в открытом космосе и легко уходит от планеты.
        SphereGravity air = SphereGravity.AtmosphereAt(rb.worldCenterOfMass, out Vector3 up, out float airWeight);
        if (cruise) air = null;
        if (air != null && air == planet)
        {
            Quaternion delta = Quaternion.FromToRotation(planetUp, up);
            rb.rotation = delta * rb.rotation;
            hold = delta * hold;
            transport = delta * transport;
        }
        planet = air;
        planetUp = up;

        // Скорость в осях машины: по боковой и вертикальной оси без тяги снос гасится — машина едет носом.
        Vector3 localVelocity = Quaternion.Inverse(rb.rotation) * rb.linearVelocity;
        var force = new Vector3(
            strafeIn != 0f ? strafeIn * strafeAccel : -localVelocity.x * sideGrip,
            liftIn != 0f ? liftIn * liftAccel : -localVelocity.y * sideGrip,
            forwardIn * forwardAccel);
        Vector3 worldForce = rb.rotation * force;

        if (air != null)
        {
            // По высоте — только Space/C, иначе вертикальный снос гасится: тяга и удары от планеты не уводят.
            Vector3 velocity = rb.linearVelocity;
            float vUp = Vector3.Dot(velocity, up);
            float vertical = liftIn != 0f ? liftIn * liftAccel : -vUp * sideGrip;
            worldForce += (up * vertical - up * Vector3.Dot(worldForce, up)) * airWeight;

            // Изгиб поверхности: ровно столько ускорения к центру, чтобы лететь по кругу, а не по касательной.
            Vector3 flat = velocity - up * vUp;
            float r = Vector3.Distance(rb.worldCenterOfMass, air.transform.position);
            worldForce -= up * (flat.sqrMagnitude / Mathf.Max(r, 1f));
        }
        rb.AddForce(worldForce, ForceMode.Acceleration);

        steering = aim != null && Controls.Steer.IsPressed();
        float yawIn = steering || MenuScreen.Opened != null ? 0f : Controls.Turn.ReadValue<float>();

        // Крен внутрь поворота по фактической скорости рыскания — смещение поверх удерживаемой ориентации.
        float yawSpeed = (Quaternion.Inverse(rb.rotation) * rb.angularVelocity).y;
        float bank = Mathf.Clamp(yawSpeed / Mathf.Max(yawRate, 0.01f), -1f, 1f) * maxBank;
        Quaternion bankOffset = Quaternion.Euler(0f, 0f, -bank);

        if (steering)
        {
            leveling = false;
            hold = aim.AimRotation;
            if (air != null) hold = Quaternion.Slerp(hold, LevelRotation(hold, up), airWeight); // в атмосфере мышь задаёт только курс
        }
        else if (knockTimer > 0f)
        {
            knockTimer -= dt;
            hold = rb.rotation * Quaternion.Inverse(bankOffset);
        }
        else
        {
            hold *= Quaternion.Euler(0f, yawIn * yawRate * Mathf.Rad2Deg * dt, 0f);
            if (leveling)
            {
                bool parked = Time.fixedTime - parkingTime <= dt * 1.5f;
                Quaternion level = LevelRotation(hold, parked ? parkingUp : air != null ? up : Vector3.up);
                hold = Quaternion.RotateTowards(hold, level, levelSpeed * dt);
                if (Quaternion.Angle(hold, level) < 0.1f) leveling = false;
            }
        }
        // Выравнивание в атмосфере — в любом состоянии, в том числе сразу после удара.
        if (air != null)
            hold = Quaternion.RotateTowards(hold, LevelRotation(hold, up), atmosphereAlignSpeed * airWeight * dt);
        hold = Quaternion.Normalize(hold);

        Vector3 desired = TurnTowards(hold * bankOffset) + hold * Vector3.up * (yawIn * yawRate);
        rb.AddTorque((desired - rb.angularVelocity) * turnDamping, ForceMode.Acceleration);
    }

    // Тот же курс, крыша к up, без крена. Если нос смотрит вдоль up, курс берётся по крыше.
    static Quaternion LevelRotation(Quaternion rotation, Vector3 up)
    {
        Vector3 forward = rotation * Vector3.forward;
        Vector3 heading = Vector3.ProjectOnPlane(forward, up);
        if (heading.sqrMagnitude < 1e-4f)
            heading = Vector3.ProjectOnPlane(rotation * Vector3.up * -Mathf.Sign(Vector3.Dot(forward, up)), up);
        return Quaternion.LookRotation(heading, up);
    }

    // Внешний разворот удерживаемой ориентации (например, парковкой). Не действует, пока игрок рулит мышью.
    public void AlignHold(Quaternion target, float maxDegrees)
    {
        parkingUp = target * Vector3.up;
        parkingTime = Time.fixedTime;
        if (!steering) hold = Quaternion.RotateTowards(hold, target, maxDegrees);
    }

    // Мгновенно переставить машину (буксир): без скорости, держит новую ориентацию.
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        SetCruise(false);
        rb.position = position;
        rb.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        hold = rotation;
        knockTimer = 0f;
        leveling = false;
        planet = null;
        transport = Quaternion.identity;
        if (aim != null) aim.Snap();
    }

    // Поворот вокруг планеты, накопленный с прошлого вызова: камера поворачивает на него свой взгляд.
    public Quaternion TakeTransport()
    {
        Quaternion t = transport;
        transport = Quaternion.identity;
        return t;
    }

    // Мягкое касание (посадка, скольжение по стене) толчком не считается.
    // Толчок выбивает и из маршевого.
    void OnCollisionEnter(Collision collision)
    {
        if (collision.impulse.magnitude / rb.mass <= knockImpulse) return;
        knockTimer = knockTime;
        SetCruise(false);
    }

    // Угловая скорость, с которой нужно вращаться, чтобы довернуть к цели.
    Vector3 TurnTowards(Quaternion target)
    {
        Quaternion delta = target * Quaternion.Inverse(rb.rotation);
        if (delta.w < 0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
        delta.ToAngleAxis(out float angle, out Vector3 axis);

        Vector3 desired = angle > 0.01f ? axis * (angle * Mathf.Deg2Rad * turnStiffness) : Vector3.zero;
        return Vector3.ClampMagnitude(desired, maxTurnRate);
    }

    void Update()
    {
        if (Controls.Pressed(Controls.Use)) leveling = true;

        if (!Controls.Up.IsPressed()) spaceLatch = false;
        bool shift = Controls.Boost.IsPressed();
        if (shift && Controls.Pressed(Controls.Up))
        {
            spaceLatch = true;
            if (!Cruising && !CanCruise) Hud.Toast(fuel.OnReserve ? "Маршевый недоступен: топливо на резерве" : "Маршевый недоступен: корпус побит");
            else SetCruise(!Cruising);
        }
        else if (Cruising && (!shift || !CanCruise)) SetCruise(false);
    }

    // Вышли из машины — маршевый гаснет, тяги нет.
    void OnDisable()
    {
        SetCruise(false);
        Thrust = Vector3.zero;
        Boosting = false;
    }

    void SetCruise(bool on) => Cruising = on;
}
