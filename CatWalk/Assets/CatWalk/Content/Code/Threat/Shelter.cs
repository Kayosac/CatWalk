using System.Collections.Generic;
using UnityEngine;

// Защищённое место: обитаемая станция или поселение на планете. Охотник кредиторов (Threat) сюда не залетает,
// а если машина внутри radius — отворачивает и уходит. На заправках укрытия нет (там охотник лишь держится поодаль).
public class Shelter : MonoBehaviour
{
    public static readonly List<Shelter> All = new();

    public float radius = 300f;           // от центра объекта, м

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public static bool Covers(Vector3 position) => All.Exists(s => (s.transform.position - position).sqrMagnitude < s.radius * s.radius);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
