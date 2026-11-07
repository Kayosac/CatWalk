using UnityEngine;
using UnityEngine.InputSystem;

// Камера космокара с инерцией. Курсор захвачен, мышь всегда осматривает.
// Горизонта нет: без ПКМ камера осматривается относительно машины и вращается вместе с ней,
// на ходу без движения мыши сама возвращается за спину.
// С ПКМ мышь вращает прицел без ограничений, машина доворачивает за ним.
// Курсор отпускают меню (MenuScreen), ЛКМ — захватить снова.
public class CosmoCarCamera : MonoBehaviour
{
    public Transform target;              // по умолчанию — космокар в сцене
    public float distance = 12f;
    public float height = 2.5f;
    public float sensitivity = 0.1f;
    public float restPitch = 8f;          // наклон камеры, при котором машина целится по носу
    public float minPitch = -70f;         // пределы осмотра без ПКМ, относительно машины
    public float maxPitch = 80f;

    [Header("Инерция")]
    public float followTime = 0.15f;      // запаздывание за машиной, с
    public float rotationSharpness = 8f;  // как быстро камера догоняет взгляд, 1/с

    [Header("Возврат за спину")]
    public float recenterDelay = 1.5f;    // секунд без мыши
    public float recenterSpeed = 2f;      // 1/с
    public float recenterMinSpeed = 2f;   // только на ходу, м/с

    Quaternion look = Quaternion.identity;  // взгляд без сглаживания — по нему целится машина
    Quaternion shown = Quaternion.identity; // сглаженный поворот камеры
    Vector3 pivot, pivotVelocity;
    float yaw, pitch, idleTime;             // осмотр относительно машины
    bool steering;
    CosmoCarController car;
    Rigidbody targetBody;

    // Куда доворачивает машина при зажатой ПКМ.
    public Quaternion AimRotation => look * Quaternion.Euler(-restPitch, 0f, 0f);

    void Start()
    {
        pitch = restPitch;
        if (target == null)
        {
            var found = FindFirstObjectByType<CosmoCarController>();
            if (found != null) target = found.transform;
        }
        if (target != null)
        {
            car = target.GetComponent<CosmoCarController>();
            targetBody = target.GetComponent<Rigidbody>();
            pivot = target.position;
            look = shown = Frame() * Quaternion.Euler(pitch, yaw, 0f);
        }
        SetCursorLocked(true);
    }

    void LateUpdate()
    {
        Mouse m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) SetCursorLocked(true);

        Vector2 d = m != null && Cursor.lockState == CursorLockMode.Locked
            ? m.delta.ReadValue() * sensitivity
            : Vector2.zero;
        idleTime = d != Vector2.zero ? 0f : idleTime + Time.deltaTime;

        if (target == null) return;

        // Над планетой машина облетает её — взгляд поворачивается вместе с ней.
        if (car != null)
        {
            Quaternion around = car.TakeTransport();
            look = around * look;
            shown = around * shown;
        }

        bool steerNow = m != null && m.rightButton.isPressed;
        if (steering && !steerNow) SyncAnglesFromLook();
        steering = steerNow;

        if (steering)
        {
            // Свободное вращение в осях взгляда: без пределов.
            look *= Quaternion.Euler(-d.y, d.x, 0f);
        }
        else
        {
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, minPitch, maxPitch);

            bool moving = targetBody != null && targetBody.linearVelocity.magnitude > recenterMinSpeed;
            if (moving && idleTime > recenterDelay)
            {
                float r = 1f - Mathf.Exp(-recenterSpeed * Time.deltaTime);
                yaw = Mathf.LerpAngle(yaw, 0f, r);
                pitch = Mathf.Lerp(pitch, restPitch, r);
            }
            look = Frame() * Quaternion.Euler(pitch, yaw, 0f);
        }

        float t = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
        shown = Quaternion.Slerp(shown, look, t);
        pivot = Vector3.SmoothDamp(pivot, target.position, ref pivotVelocity, followTime);
        transform.SetPositionAndRotation(pivot + shown * new Vector3(0f, height, -distance), shown);
    }

    // Сразу встать за машиной — после телепорта (CosmoCarController.Teleport), без пролёта через всё пространство.
    public void Snap()
    {
        if (target == null) return;
        pivot = target.position;
        pivotVelocity = Vector3.zero;
        yaw = 0f;
        pitch = restPitch;
        look = shown = Frame() * Quaternion.Euler(pitch, yaw, 0f);
    }

    // Система отсчёта осмотра — ориентация машины без крена.
    Quaternion Frame() => car != null ? car.HoldRotation : target.rotation;

    // После ПКМ продолжаем осмотр с того же взгляда, только уже относительно машины.
    void SyncAnglesFromLook()
    {
        Vector3 f = Quaternion.Inverse(Frame()) * look * Vector3.forward;
        yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
