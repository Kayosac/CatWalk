using UnityEngine;

// Неоновая вывеска: каждая светящаяся часть живёт своим ритмом.
// Buzz — горит с лёгкой дрожью и изредка проваливается серией коротких миганий.
// Blink — мигает вкл/выкл с заданной частотой, иногда пропускает такт.
// Shimmer — плавно дышит яркостью и переливается между белым и оттенком tint.
// Меняет только _EmissionColor через MaterialPropertyBlock: материал общий, копий не создаётся.
public class NeonSign : MonoBehaviour
{
    public enum Mode { Buzz, Blink, Shimmer }

    [System.Serializable]
    public class Part
    {
        public Renderer renderer;
        public Mode mode;
        public float speed = 1f;                 // Blink: тактов в секунду; Shimmer: циклов в секунду
        public Color tint = Color.white;         // Shimmer: оттенок, к которому уходит свечение
        [Range(0f, 1f)] public float dim = 0.08f; // яркость в погасшем состоянии
    }

    public Part[] parts;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    MaterialPropertyBlock block;
    Color[] baseEmission;
    float[] burstEnd, nextBurst, seed;

    void Start()
    {
        block = new MaterialPropertyBlock();
        int n = parts.Length;
        baseEmission = new Color[n];
        burstEnd = new float[n];
        nextBurst = new float[n];
        seed = new float[n];
        for (int i = 0; i < n; i++)
        {
            Renderer r = parts[i].renderer;
            baseEmission[i] = r != null ? r.sharedMaterial.GetColor(EmissionId) : Color.black;
            nextBurst[i] = Time.time + Random.Range(1f, 6f);
            seed[i] = Random.value * 100f;
        }
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < parts.Length; i++)
        {
            Part p = parts[i];
            if (p.renderer == null) continue;

            float k = 1f;
            Color tint = Color.white;
            switch (p.mode)
            {
                case Mode.Buzz:
                    k = 0.9f + 0.12f * Mathf.PerlinNoise(seed[i], t * 7f);
                    if (t >= nextBurst[i])
                    {
                        burstEnd[i] = t + Random.Range(0.15f, 0.6f);
                        nextBurst[i] = burstEnd[i] + Random.Range(2f, 8f);
                    }
                    if (t < burstEnd[i] && Mathf.PerlinNoise(seed[i] + 13f, t * 40f) < 0.55f) k = p.dim;
                    break;

                case Mode.Blink:
                    float phase = t * p.speed;
                    bool on = phase - Mathf.Floor(phase) < 0.6f;
                    if (on && Mathf.PerlinNoise(seed[i], Mathf.Floor(phase) * 0.37f) < 0.12f) on = false; // пропуск такта
                    k = on ? 1f : p.dim;
                    break;

                case Mode.Shimmer:
                    float wave = 0.5f + 0.5f * Mathf.Sin((t * p.speed + seed[i]) * Mathf.PI * 2f);
                    k = Mathf.Lerp(0.7f, 1.15f, wave);
                    tint = Color.Lerp(Color.white, p.tint, 0.5f + 0.5f * Mathf.Sin((t * p.speed * 0.37f + seed[i]) * Mathf.PI * 2f));
                    break;
            }

            Color c = baseEmission[i] * tint * k;
            c.a = 1f;
            p.renderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, c);
            p.renderer.SetPropertyBlock(block);
        }
    }
}
