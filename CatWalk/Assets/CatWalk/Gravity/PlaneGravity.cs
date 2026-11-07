using UnityEngine;

// Пол станции: тянет вдоль −Y объекта в зоне-коробке над полом.
// Внутри коробки сила полная, за её стенками и крышей плавно спадает до нуля на расстоянии falloff.
// Под полом не тянет. Размеры — в метрах мира, от масштаба объекта не зависят.
public class PlaneGravity : GravitySource
{
    public Vector3 size = new Vector3(12f, 10f, 12f); // ширина, высота над полом, длина зоны, м
    public float falloff = 3f;                         // м

    public override Vector3 GetGravity(Vector3 position)
    {
        Vector3 local = Quaternion.Inverse(transform.rotation) * (position - transform.position);
        if (local.y < 0f) return Vector3.zero; // под полом не тянет — иначе у ребра станции соседняя грань тянула бы вбок

        // Насколько точка вылезла за коробку (0 — внутри).
        float outside = Mathf.Max(
            Mathf.Abs(local.x) - size.x * 0.5f,
            Mathf.Abs(local.z) - size.z * 0.5f,
            local.y - size.y,
            0f);
        if (outside > 0f && outside >= falloff) return Vector3.zero;

        float fade = outside > 0f ? Mathf.SmoothStep(1f, 0f, outside / falloff) : 1f;
        return -transform.up * (gravity * fade);
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireCube(Vector3.up * (size.y * 0.5f), size);
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.2f);
        Vector3 outer = size + new Vector3(falloff * 2f, falloff, falloff * 2f);
        Gizmos.DrawWireCube(Vector3.up * (outer.y * 0.5f), outer);
    }
}
