using UnityEngine;
using UnityEngine.InputSystem;

// Орбитальная камера от третьего лица. Курсор захвачен, мышь вращает камеру вокруг персонажа.
// Горизонт камеры плавно следует за «верхом» кота: на планетке камера обходит её вместе с ним.
// В невесомости горизонт замирает, кот кувыркается сам по себе.
// Курсор отпускают меню (MenuScreen), ЛКМ — захватить снова. Правый стик геймпада вращает так же (Controls).
public class CatCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5f;
    public float height = 1.5f;           // точка вращения над ногами персонажа
    public float sensitivity = 0.1f;
    public float minPitch = -30f;
    public float maxPitch = 70f;
    public float followTime = 0.1f;       // запаздывание за персонажем, с
    public float upSharpness = 6f;        // как быстро горизонт догоняет новый «верх», 1/с

    float yaw, pitch = 15f;
    Vector3 pivot, pivotVelocity;
    Quaternion frame = Quaternion.identity; // горизонт: yaw и pitch считаются от него
    CatController cat;

    void Start()
    {
        if (target != null)
        {
            cat = target.GetComponent<CatController>();
            frame = target.rotation;      // сразу за спиной
            pivot = target.position + target.up * height;
        }
        SetCursorLocked(true);
    }

    void LateUpdate()
    {
        Mouse m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) SetCursorLocked(true);

        Vector2 d = Controls.Look(sensitivity); // множитель и инверсия из настроек
        yaw += d.x;
        pitch = Mathf.Clamp(pitch - d.y, minPitch, maxPitch);

        if (target == null) return;

        // Доворачиваем горизонт к «верху» кота кратчайшим путём — курс при этом не сбивается.
        Vector3 up = cat != null ? cat.Up : target.up;
        Quaternion aligned = Quaternion.FromToRotation(frame * Vector3.up, up) * frame;
        frame = Quaternion.Slerp(frame, aligned, 1f - Mathf.Exp(-upSharpness * Time.deltaTime));

        pivot = Vector3.SmoothDamp(pivot, target.position + frame * Vector3.up * height, ref pivotVelocity, followTime);
        Quaternion rot = frame * Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(pivot - rot * Vector3.forward * distance, rot);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
