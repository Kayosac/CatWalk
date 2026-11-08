using UnityEngine;
using UnityEngine.Rendering.Universal;

// Обход бага URP 17: если в кадре ни у одного источника с тенями нет теней в пределах
// Shadow Distance, URP оставляет им индекс тени без матрицы, и всё, что они освещают,
// рендерится в NaN — bloom заливает экран белым. Поэтому тень источника включена,
// только пока он ближе Shadow Distance к камере; дальше её всё равно не видно.
[RequireComponent(typeof(Light))]
public class ShadowDistanceCull : MonoBehaviour
{
    Light lamp;
    LightShadows shadows;

    void Awake()
    {
        lamp = GetComponent<Light>();
        shadows = lamp.shadows;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (shadows == LightShadows.None || cam == null) return;
        float max = UniversalRenderPipeline.asset.shadowDistance;
        bool near = (cam.transform.position - transform.position).sqrMagnitude < max * max;
        lamp.shadows = near ? shadows : LightShadows.None;
    }
}
