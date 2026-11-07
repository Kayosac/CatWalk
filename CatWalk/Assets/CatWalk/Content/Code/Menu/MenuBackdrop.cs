using UnityEngine;

// Фон главного меню: медленно вращает объект (модель космокара).
public class MenuBackdrop : MonoBehaviour
{
    public Vector3 speed = new(0f, 6f, 0f); // град/с

    void Update() => transform.Rotate(speed * Time.unscaledDeltaTime, Space.World);
}
