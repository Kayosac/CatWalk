using UnityEngine;
using UnityEngine.InputSystem;

// Персонаж от третьего лица без анимаций на Rigidbody. Ходит относительно камеры по любой поверхности:
// «верх» — против гравитации (GravitySource), тело плавно разворачивается за ней.
// WASD — ходьба, Shift — бег, Space — прыжок.
// Вне всех зон гравитации беспомощно дрейфует по инерции и медленно кувыркается.
// Вместо анимации шага модель «баунсит» скейлом: вверх — вытягивается и сужается,
// вниз — сплющивается и расширяется. Объём сохраняется: ширина = 1 / √высоты.
// При приземлении модель сплющивается от удара и пружинит обратно.
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class CatController : MonoBehaviour
{
    public Transform cam;                 // по умолчанию — Camera.main
    public Transform visual;              // модель с пивотом в ногах; если пусто — создаётся капсула

    [Header("Движение")]
    public float moveSpeed = 4f;          // м/с
    public float runSpeed = 7f;           // Shift, м/с
    public float jumpHeight = 1.2f;       // м
    public float acceleration = 30f;      // м/с²
    public float turnSpeed = 720f;        // поворот по ходу и разворот за гравитацией, °/с
    public float gravityScale = 2.5f;     // кот падает быстрее посылок — прыжок резче
    public float groundCheck = 0.15f;     // насколько ниже ног ещё считается земля, м
    public float maxSlope = 50f;          // круче — уже не земля, °

    [Header("Невесомость")]
    public float tumbleSpeed = 40f;       // кувыркание, °/с

    [Header("Баунс")]
    public float bounceFrequency = 3f;    // шагов в секунду при ходьбе, на бегу — чаще
    public float bounceAmount = 0.12f;    // насколько вытягивается/сплющивается, доля высоты
    public float bounceFade = 10f;        // как быстро баунс появляется и затухает, 1/с

    [Header("Приземление")]
    public float landSquash = 0.03f;      // сплющивание на каждый м/с скорости падения
    public float maxLandSquash = 0.35f;   // предел, доля высоты
    public float squashStiffness = 250f;  // жёсткость пружины: больше — быстрее возвращается
    public float squashDamping = 12f;     // меньше — сильнее перепрыгивает в вытягивание

    // Против гравитации; в невесомости — последний известный. По нему держит горизонт камера.
    public Vector3 Up { get; private set; } = Vector3.up;

    Rigidbody rb;
    CapsuleCollider capsule;
    Vector3 baseScale, tumbleAxis;
    float groundSpeed, phase, weight, squash, squashVelocity;
    bool grounded = true, floating, jumpQueued;

    void Reset()
    {
        var c = GetComponent<CapsuleCollider>();
        c.height = 2f;
        c.radius = 0.5f;
        c.center = Vector3.up;            // пивот персонажа — в ногах
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;         // поворачиваем сами
        rb.linearDamping = 0f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Без трения: скорость задаём сами, а в прыжке не липнем к стенам.
        capsule = GetComponent<CapsuleCollider>();
        capsule.material = new PhysicsMaterial("Cat")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
        };

        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (visual == null) visual = CreateCapsule();
        baseScale = visual.localScale;
        Up = transform.up;
    }

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k != null && k.spaceKey.wasPressedThisFrame) jumpQueued = true;
        float dt = Time.deltaTime;

        squashVelocity += (-squashStiffness * squash - squashDamping * squashVelocity) * dt;
        squash += squashVelocity * dt;

        // Баунс: сила зависит от реальной скорости, в воздухе и на месте затухает. На бегу шаги чаще.
        float speed = groundSpeed / moveSpeed; // 1 — ходьба
        weight = Mathf.Lerp(weight, Mathf.Clamp01(speed), 1f - Mathf.Exp(-bounceFade * dt));
        phase = Mathf.Repeat(phase + bounceFrequency * Mathf.Max(speed, 1f) * 2f * Mathf.PI * dt, 2f * Mathf.PI);

        float y = Mathf.Max(0.3f, 1f + bounceAmount * weight * Mathf.Sin(phase) + squash);
        float xz = 1f / Mathf.Sqrt(y);
        visual.localScale = Vector3.Scale(baseScale, new Vector3(xz, y, xz));
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        bool jump = jumpQueued;
        jumpQueued = false;

        Vector3 g = GravitySource.At(rb.worldCenterOfMass);
        if (g.sqrMagnitude < 1e-6f)
        {
            // Невесомость: ввод не работает, летим по инерции и кувыркаемся.
            if (!floating) tumbleAxis = Random.onUnitSphere;
            floating = true;
            grounded = false;
            groundSpeed = 0f;
            rb.MoveRotation(Quaternion.AngleAxis(tumbleSpeed * dt, tumbleAxis) * rb.rotation);
            return;
        }
        floating = false;
        Vector3 up = -g.normalized;
        Up = up;

        Vector3 velocity = rb.linearVelocity;
        float vUp = Vector3.Dot(velocity, up);
        Vector3 lateral = velocity - up * vUp;

        // Земля: сфера чуть меньше капсулы из её нижней полусферы — вниз по гравитации.
        float castRadius = capsule.radius * 0.9f;
        Vector3 foot = rb.position + rb.rotation * Vector3.up * capsule.radius;
        bool wasGrounded = grounded;
        grounded = vUp < 0.1f
            && Physics.SphereCast(foot, castRadius, -up, out RaycastHit hit,
                capsule.radius - castRadius + groundCheck, ~0, QueryTriggerInteraction.Ignore)
            && Vector3.Angle(hit.normal, up) < maxSlope;

        // Ввод — по курсу камеры в плоскости, перпендикулярной «верху».
        Keyboard k = Keyboard.current;
        Vector2 input = k == null ? Vector2.zero : Vector2.ClampMagnitude(new Vector2(
            (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f),
            (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f)), 1f);
        Transform view = cam != null ? cam : transform;
        Vector3 forward = Vector3.ProjectOnPlane(view.forward, up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(view.up, up);
        forward.Normalize();
        Vector3 right = Vector3.Cross(up, forward);
        bool run = k != null && k.leftShiftKey.isPressed;
        Vector3 wish = (right * input.x + forward * input.y) * (run ? runSpeed : moveSpeed);

        lateral = Vector3.MoveTowards(lateral, wish, acceleration * dt);
        float fall = g.magnitude * gravityScale;
        if (grounded)
        {
            if (!wasGrounded)
            {
                squash = -Mathf.Min(-vUp * landSquash, maxLandSquash);
                squashVelocity = 0f;
            }
            vUp = jump ? Mathf.Sqrt(2f * fall * jumpHeight) : -1f; // стоя — слегка прижимаемся к земле
        }
        else
        {
            vUp -= fall * dt;
        }
        rb.linearVelocity = lateral + up * vUp;
        groundSpeed = grounded ? lateral.magnitude : 0f;

        // Крышей против гравитации, носом по ходу.
        Quaternion rotation = rb.rotation;
        Vector3 facing = wish.sqrMagnitude > 0.01f ? wish : Vector3.ProjectOnPlane(rotation * Vector3.forward, up);
        if (facing.sqrMagnitude < 1e-4f) facing = Vector3.ProjectOnPlane(rotation * Vector3.up, up);
        rb.MoveRotation(Quaternion.RotateTowards(rotation, Quaternion.LookRotation(facing, up), turnSpeed * dt));
    }

    // Капсула-заглушка: пивот в ногах, чтобы скейл шёл от земли.
    Transform CreateCapsule()
    {
        var pivot = new GameObject("Visual").transform;
        pivot.SetParent(transform, false);
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(pivot, false);
        body.transform.localPosition = Vector3.up;
        return pivot;
    }
}
