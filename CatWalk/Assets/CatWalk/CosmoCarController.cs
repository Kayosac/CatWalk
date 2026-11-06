using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Физичный космокар на Rigidbody. Тяга и сцепление — силами в осях машины, поворот — моментом.
// Горизонта нет: машина держит ту ориентацию, в которой её оставили.
// ПКМ зажата — доворачивает за прицелом камеры. Иначе A/D поворачивают вокруг собственной оси «вверх».
// В повороте машина кренится внутрь. После удара остаётся там, куда её развернуло.
// F — медленно выровняться по горизонту, сохранив курс.
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
    public float levelSpeed = 30f;        // скорость выравнивания по F, °/с

    const string Help =
        "W/S газ · A/D поворот · Q/E вбок\n" +
        "Space/C вверх/вниз · Shift форсаж\n" +
        "F выровнять · ПКМ рулить мышью\n" +
        "Esc — курсор · F1 — подсказка";

    Rigidbody rb;
    Quaternion hold;                      // ориентация, которую машина держит (без крена)
    float knockTimer;
    bool leveling;
    bool showHelp;
    GUIStyle helpStyle;

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
        rb.AddRelativeForce(force, ForceMode.Acceleration);

        bool steering = aim != null && m != null && m.rightButton.isPressed;
        float yawIn = steering ? 0f : Axis(k.aKey, k.dKey);

        // Крен внутрь поворота по фактической скорости рыскания — смещение поверх удерживаемой ориентации.
        float yawSpeed = (Quaternion.Inverse(rb.rotation) * rb.angularVelocity).y;
        float bank = Mathf.Clamp(yawSpeed / Mathf.Max(yawRate, 0.01f), -1f, 1f) * maxBank;
        Quaternion bankOffset = Quaternion.Euler(0f, 0f, -bank);

        if (steering)
        {
            leveling = false;
            hold = aim.AimRotation;
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
                Quaternion level = LevelRotation(hold);
                hold = Quaternion.RotateTowards(hold, level, levelSpeed * dt);
                if (Quaternion.Angle(hold, level) < 0.1f) leveling = false;
            }
        }
        hold = Quaternion.Normalize(hold);

        Vector3 desired = TurnTowards(hold * bankOffset) + hold * Vector3.up * (yawIn * yawRate);
        rb.AddTorque((desired - rb.angularVelocity) * turnDamping, ForceMode.Acceleration);
    }

    // Тот же курс, нос по горизонту, без крена. Если нос смотрит вертикально, курс берётся по крыше.
    static Quaternion LevelRotation(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        Vector3 heading = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (heading.sqrMagnitude < 1e-4f)
            heading = Vector3.ProjectOnPlane(rotation * Vector3.up * -Mathf.Sign(forward.y), Vector3.up);
        return Quaternion.LookRotation(heading, Vector3.up);
    }

    void OnCollisionEnter() => knockTimer = knockTime;
    void OnCollisionStay() => knockTimer = knockTime;

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
