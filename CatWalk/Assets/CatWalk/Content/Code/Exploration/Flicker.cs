using UnityEngine;

// Мигающая лампа заброшенного места: горит неровными промежутками и гаснет короткими провалами.
// Гасит свет и светящийся плафон вместе.
public class Flicker : MonoBehaviour
{
    public Light lamp;
    public Renderer glow;                 // плафон; может быть пустым
    public Vector2 onTime = new(0.05f, 1.6f);  // сколько горит, с (от, до)
    public Vector2 offTime = new(0.03f, 0.35f); // сколько не горит, с

    bool on = true;
    float next;

    void Update()
    {
        if (Time.time < next) return;
        on = !on;
        next = Time.time + (on ? Random.Range(onTime.x, onTime.y) : Random.Range(offTime.x, offTime.y));
        if (lamp != null) lamp.enabled = on;
        if (glow != null) glow.enabled = on;
    }
}
