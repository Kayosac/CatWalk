using UnityEngine;

// Планетка: тянет к центру. До radius сила полная, дальше плавно спадает до нуля к outerRadius.
// Радиусы — в метрах мира, от масштаба объекта не зависят.
// Для транспорта эта же зона — атмосфера: машину не тянет, но она летит вдоль планеты (см. CosmoCarController).
public class SphereGravity : GravitySource
{
    public float radius = 25f;            // полная сила до этого расстояния от центра, м
    public float outerRadius = 45f;       // здесь сила спадает до нуля, м

    public override Vector3 GetGravity(Vector3 position)
    {
        Vector3 toCenter = transform.position - position;
        float distance = toCenter.magnitude;
        if (distance >= outerRadius || distance < 1e-3f) return Vector3.zero;

        float fade = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(radius, outerRadius, distance));
        return toCenter / distance * (gravity * fade);
    }

    // Атмосфера в точке: самая сильная планета. weight — 1 у поверхности, 0 на краю зоны. null — открытый космос.
    public static SphereGravity AtmosphereAt(Vector3 position, out Vector3 up, out float weight)
    {
        SphereGravity best = null;
        up = Vector3.up;
        weight = 0f;
        foreach (GravitySource source in sources)
        {
            if (source is not SphereGravity planet || planet.gravity <= 0f) continue;
            Vector3 g = planet.GetGravity(position);
            float w = g.magnitude / planet.gravity;
            if (w > weight)
            {
                best = planet;
                weight = w;
                up = -g / g.magnitude;
            }
        }
        return best;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, outerRadius);
    }
}
