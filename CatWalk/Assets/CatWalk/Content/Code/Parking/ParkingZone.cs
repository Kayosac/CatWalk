using UnityEngine;

// Парковка у площадки на станции или планете — единственное, что магнитит космокар.
// Это не источник гравитации: действует только на машину. Кота и посылки тянет станция или планета, на которой она стоит.
// Рядом с площадкой машину тянет к поверхности, мягко центрирует и разворачивает крышей от площадки.
// Управление не блокируется: подъёмной тяги хватает, чтобы уйти.
// Объект ставится на поверхность площадки; его ось Y — «верх» парковки.
// В центре зоны стоит магнит (вложенный префаб ParkingMagnet): свечение и эффект магнита красятся по состоянию,
// когда машина стоит — эффект выключен.
public class ParkingZone : MonoBehaviour
{
    public CosmoCarController car;        // по умолчанию — космокар в сцене
    public Renderer indicator;            // светящаяся часть магнита, красится по состоянию
    public ParticleSystem vfx;            // эффект магнита: красится по состоянию, на стоянке выключен

    public float radius = 15f;            // с какого расстояния начинает тянуть, м
    public Vector2 padSize = new Vector2(12f, 12f); // размер площадки по X и Z объекта, м
    public float pull = 8f;               // притяжение у самой площадки, м/с² (меньше подъёма машины — 12)
    public float centering = 1.5f;        // тяга к центру на метр смещения, 1/с²
    public float maxCentering = 5f;       // м/с²
    public float alignSpeed = 60f;        // разворот к площадке у поверхности, °/с
    public float dockHeight = 1.2f;       // высота центра машины над площадкой, когда она стоит, м
    public float dockSpeed = 0.5f;        // стоит, если медленнее, м/с

    public Color idleColor = new Color(0.20f, 0.75f, 0.80f);
    public Color pullColor = new Color(1.00f, 0.65f, 0.15f);
    public Color dockedColor = new Color(0.30f, 0.90f, 0.40f);

    public bool IsPulling { get; private set; }
    public bool IsDocked { get; private set; }

    Rigidbody body;
    MaterialPropertyBlock block;
    float glow;                           // интенсивность свечения из материала (линейная)
    ParticleSystem[] particles;

    void Start()
    {
        if (car == null) car = FindFirstObjectByType<CosmoCarController>();
        if (car != null) body = car.GetComponent<Rigidbody>();
        block = new MaterialPropertyBlock();
        if (indicator != null) glow = ((Color)indicator.sharedMaterial.GetVector("_EmissionColor")).maxColorComponent;
        if (vfx != null) particles = vfx.GetComponentsInChildren<ParticleSystem>();
        Paint();
    }

    void FixedUpdate()
    {
        bool wasPulling = IsPulling, wasDocked = IsDocked;
        IsPulling = IsDocked = false;

        if (body != null)
        {
            Vector3 up = transform.up;
            Vector3 offset = body.position - transform.position;
            float height = Vector3.Dot(offset, up);
            Vector3 lateral = offset - up * height;
            float distance = offset.magnitude;

            if (height > -1f && distance < radius)
            {
                IsPulling = true;

                // У границы зоны сила нулевая, у площадки — полная.
                float strength = Mathf.SmoothStep(0f, 1f, 1f - distance / radius);
                Vector3 force = -up * pull + Vector3.ClampMagnitude(-lateral * centering, maxCentering);
                body.AddForce(force * strength, ForceMode.Acceleration);

                // Крышей от площадки, курс сохраняем.
                Vector3 forward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, up);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(body.rotation * Vector3.up, up);
                car.AlignHold(Quaternion.LookRotation(forward, up), alignSpeed * strength * Time.fixedDeltaTime);

                IsDocked = height < dockHeight
                        && Mathf.Abs(Vector3.Dot(lateral, transform.right)) < padSize.x * 0.5f
                        && Mathf.Abs(Vector3.Dot(lateral, transform.forward)) < padSize.y * 0.5f
                        && body.linearVelocity.magnitude < dockSpeed;
            }
        }

        if (IsPulling != wasPulling || IsDocked != wasDocked) Paint();
    }

    void Paint()
    {
        Color color = IsDocked ? dockedColor : IsPulling ? pullColor : idleColor;

        // Меняется только оттенок, интенсивность — как в материале. HDR-цвет пишется вектором, без пересчёта из гаммы.
        if (indicator != null)
        {
            Color emission = color.linear;
            indicator.GetPropertyBlock(block);
            block.SetVector("_EmissionColor", emission * (glow / emission.maxColorComponent));
            indicator.SetPropertyBlock(block);
        }

        if (vfx != null)
        {
            foreach (ParticleSystem system in particles)
            {
                ParticleSystem.MainModule main = system.main;
                main.startColor = color;
            }

            if (IsDocked) vfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            else if (!vfx.isPlaying) vfx.Play(true);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.65f, 0.15f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * (dockHeight * 0.5f), new Vector3(padSize.x, dockHeight, padSize.y));
    }
}
