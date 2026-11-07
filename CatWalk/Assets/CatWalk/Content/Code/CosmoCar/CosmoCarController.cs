using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Физичный космокар на Rigidbody. Тяга и сцепление — силами в осях машины, поворот — моментом.
// Горизонта нет: машина держит ту ориентацию, в которой её оставили.
// ПКМ зажата — доворачивает за прицелом камеры. Иначе A/D поворачивают вокруг собственной оси «вверх».
// В повороте машина кренится внутрь. После сильного удара остаётся там, куда её развернуло.
// Гравитация планет и станций на машину не действует — магнитят её только парковки (ParkingZone).
// В атмосфере планеты машина летит вдоль неё: тяга не уводит от поверхности, а движение загибается
// по её изгибу, так что W ведёт по кругу на той же высоте. Высоту меняют только Space/C.
// Машина сама ложится крышей от планеты: у поверхности — жёстко, к краю атмосферы всё мягче.
// F — медленно выровняться, сохранив курс: на парковке — по ней, в атмосфере — по планете, иначе по мировому верху.
[RequireComponent(typeof(Rigidbody))]
public class CosmoCarController : MonoBehaviour
{
    public CosmoCarCamera aim;            // по умолчанию — камера в сцене

    [Header("Тяга, м/с²")]
    public float thrust = 20f;
    public float reverseThrust = 10f;
    public float strafeThrust = 12f;
    public float liftThrust = 12f;
    public float boostMultiplier = 2.5f;  // Shift

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

    const string Help =
        "W/S газ · A/D поворот · Q/E вбок\n" +
        "Space/C вверх/вниз · Shift форсаж\n" +
        "F выровнять · ПКМ рулить мышью\n" +
        "Esc — курсор · F1 — подсказка";

    Rigidbody rb;
    Quaternion hold;                      // ориентация, которую машина держит (без крена)
    float knockTimer;
    bool leveling;
    bool steering;
    bool showHelp;
    GUIStyle helpStyle;
    SphereGravity planet;                 // в чьей атмосфере были на прошлом шаге
    Vector3 planetUp;
    Quaternion transport = Quaternion.identity; // поворот вокруг планеты, ещё не забранный камерой
    Vector3 parkingUp;                    // «верх» парковки, которая разворачивает машину
    float parkingTime = -1f;

    public Quaternion HoldRotation => hold;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        if (k == null) return;
        float dt = Time.fixedDeltaTime;

        rb.linearDamping = linearDamping;

        // Атмосфера: машина поворачивается на столько, на сколько сместился «верх» планеты, — без отставания,
        // иначе нос задирается над горизонтом и тяга уводит от поверхности.
        SphereGravity air = SphereGravity.AtmosphereAt(rb.worldCenterOfMass, out Vector3 up, out float airWeight);
        if (air != null && air == planet)
        {
            Quaternion delta = Quaternion.FromToRotation(planetUp, up);
            rb.rotation = delta * rb.rotation;
            hold = delta * hold;
            transport = delta * transport;
        }
        planet = air;
        planetUp = up;

        float forwardIn = Axis(k.sKey, k.wKey);
        float strafeIn = Axis(k.qKey, k.eKey);
        float liftIn = Axis(k.cKey, k.spaceKey);
        float forwardAccel = forwardIn > 0f ? thrust * (k.leftShiftKey.isPressed ? boostMultiplier : 1f) : reverseThrust;

        // Скорость в осях машины: по боковой и вертикальной оси без тяги снос гасится — машина едет носом.
        Vector3 localVelocity = Quaternion.Inverse(rb.rotation) * rb.linearVelocity;
        var force = new Vector3(
            strafeIn != 0f ? strafeIn * strafeThrust : -localVelocity.x * sideGrip,
            liftIn != 0f ? liftIn * liftThrust : -localVelocity.y * sideGrip,
            forwardIn * forwardAccel);
        Vector3 worldForce = rb.rotation * force;

        if (air != null)
        {
            // По высоте — только Space/C, иначе вертикальный снос гасится: тяга и удары от планеты не уводят.
            Vector3 velocity = rb.linearVelocity;
            float vUp = Vector3.Dot(velocity, up);
            float vertical = liftIn != 0f ? liftIn * liftThrust : -vUp * sideGrip;
            worldForce += (up * vertical - up * Vector3.Dot(worldForce, up)) * airWeight;

            // Изгиб поверхности: ровно столько ускорения к центру, чтобы лететь по кругу, а не по касательной.
            Vector3 flat = velocity - up * vUp;
            float r = Vector3.Distance(rb.worldCenterOfMass, air.transform.position);
            worldForce -= up * (flat.sqrMagnitude / Mathf.Max(r, 1f));
        }
        rb.AddForce(worldForce, ForceMode.Acceleration);

        steering = aim != null && m != null && m.rightButton.isPressed;
        float yawIn = steering ? 0f : Axis(k.aKey, k.dKey);

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

    // Поворот вокруг планеты, накопленный с прошлого вызова: камера поворачивает на него свой взгляд.
    public Quaternion TakeTransport()
    {
        Quaternion t = transport;
        transport = Quaternion.identity;
        return t;
    }

    // Мягкое касание (посадка, скольжение по стене) толчком не считается.
    void OnCollisionEnter(Collision collision)
    {
        if (collision.impulse.magnitude / rb.mass > knockImpulse) knockTimer = knockTime;
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
        Keyboard k = Keyboard.current;
        if (k == null) return;
        if (k.f1Key.wasPressedThisFrame) showHelp = !showHelp;
        if (k.fKey.wasPressedThisFrame) leveling = true;
    }

    void OnGUI()
    {
        if (!showHelp) return;
        helpStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.UpperRight };
        GUI.Label(new Rect(Screen.width - 266f, 6f, 260f, 60f), Help, helpStyle);
    }

    static float Axis(KeyControl negative, KeyControl positive) =>
        (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
}
