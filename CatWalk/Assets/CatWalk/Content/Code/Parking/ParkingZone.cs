using UnityEngine;

// Парковка у площадки станции — единственное, что магнитит космокар: остальная гравитация на транспорт не действует.
// Зону и силу задаёт PlaneGravity на том же объекте (она же тянет кота и посылки). Парковка притягивает к ней машину,
// мягко центрирует, разворачивает крышей от площадки и следит за статусом.
// Управление не блокируется: подъёмной тяги хватает, чтобы уйти.
// Объект ставится на поверхность площадки; его ось Y — «верх» парковки.
[RequireComponent(typeof(PlaneGravity))]
public class ParkingZone : MonoBehaviour
{
    public CosmoCarController car;        // по умолчанию — космокар в сцене
    public Renderer indicator;            // красится по состоянию (необязательно)

    public float padHalfSize = 6f;        // полуразмер площадки, м
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
    PlaneGravity field;
    MaterialPropertyBlock block;

    void Start()
    {
        if (car == null) car = FindFirstObjectByType<CosmoCarController>();
        if (car != null) body = car.GetComponent<Rigidbody>();
        field = GetComponent<PlaneGravity>();
        block = new MaterialPropertyBlock();
        Paint();
    }

    void FixedUpdate()
    {
        bool wasPulling = IsPulling, wasDocked = IsDocked;
        IsPulling = IsDocked = false;

        if (body != null && field.gravity > 0f)
        {
            // Сила площадки в точке машины: у границы зоны 0, внутри 1.
            float strength = field.GetGravity(body.worldCenterOfMass).magnitude / field.gravity;
            if (strength > 0f)
            {
                IsPulling = true;

                Vector3 up = transform.up;
                Vector3 offset = body.position - transform.position;
                float height = Vector3.Dot(offset, up);
                Vector3 lateral = offset - up * height;
                Vector3 pull = -up * field.gravity + Vector3.ClampMagnitude(-lateral * centering, maxCentering);
                body.AddForce(pull * strength, ForceMode.Acceleration);

                // Крышей от площадки, курс сохраняем.
                Vector3 forward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, up);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(body.rotation * Vector3.up, up);
                car.AlignHold(Quaternion.LookRotation(forward, up), alignSpeed * strength * Time.fixedDeltaTime);

                IsDocked = height < dockHeight
                        && lateral.magnitude < padHalfSize
                        && body.linearVelocity.magnitude < dockSpeed;
            }
        }

        if (IsPulling != wasPulling || IsDocked != wasDocked) Paint();
    }

    void Paint()
    {
        if (indicator == null) return;
        indicator.GetPropertyBlock(block);
        block.SetColor("_BaseColor", IsDocked ? dockedColor : IsPulling ? pullColor : idleColor);
        indicator.SetPropertyBlock(block);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.65f, 0.15f, 0.6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * (dockHeight * 0.5f), new Vector3(padHalfSize * 2f, dockHeight, padHalfSize * 2f));
    }
}
