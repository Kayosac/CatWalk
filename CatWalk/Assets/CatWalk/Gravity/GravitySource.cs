using System.Collections.Generic;
using UnityEngine;

// Источник гравитации. Все включённые источники сами попадают в общий список.
// В каждой точке действует один источник — самый сильный. Если сила одинаковая
// (например, станция на орбите планеты, обе тянут в полную силу), побеждает больший приоритет.
// Действует на кота и свободные предметы. Транспорт эту гравитацию не чувствует: космокар магнитят только парковки,
// а атмосфера планет лишь ведёт его вдоль поверхности (SphereGravity.AtmosphereAt).
public abstract class GravitySource : MonoBehaviour
{
    public float gravity = 8f;            // полная сила, м/с² (меньше подъёма космокара — 12, иначе не взлететь)
    public int priority;                  // решает при равной силе

    protected static readonly List<GravitySource> sources = new();

    protected virtual void OnEnable() => sources.Add(this);
    protected virtual void OnDisable() => sources.Remove(this);

    // Ускорение от этого источника в точке, м/с². Ноль — точка вне зоны.
    public abstract Vector3 GetGravity(Vector3 position);

    // Гравитация, которая действует в точке. Ноль — открытый космос.
    public static Vector3 At(Vector3 position)
    {
        Vector3 best = Vector3.zero;
        float bestStrength = 0f;
        int bestPriority = int.MinValue;
        foreach (GravitySource source in sources)
        {
            Vector3 g = source.GetGravity(position);
            float strength = g.magnitude;
            if (strength < 1e-3f) continue;
            bool stronger = strength > bestStrength + 1e-3f;
            bool tie = strength > bestStrength - 1e-3f && source.priority > bestPriority;
            if (stronger || tie)
            {
                best = g;
                bestStrength = strength;
                bestPriority = source.priority;
            }
        }
        return best;
    }
}
