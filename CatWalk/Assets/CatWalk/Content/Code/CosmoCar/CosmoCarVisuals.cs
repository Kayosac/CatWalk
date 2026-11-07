using UnityEngine;

// Огни и VFX космокара: двигатели по вводу тяги, габариты, фары, багажник со светом в салоне.
// Висит на корне машины (контроллер отключается, когда кот выходит, — этот скрипт работает всегда).
// Всё включается плавно: каждой группе — свой коэффициент 0..N, к нему тянется текущий.
[RequireComponent(typeof(CosmoCarController))]
public class CosmoCarVisuals : MonoBehaviour
{
    // Группа частиц и света. Множитель масштабирует эмиссию и яркость от значений, выставленных в префабе.
    [System.Serializable]
    public class Glow
    {
        public Transform root;
        [HideInInspector] public float level;
        ParticleSystem[] particles;
        float[] rates;
        Light[] lights;
        float[] intensities;
        Quaternion baseRotation;

        public void Init()
        {
            if (root == null) return;
            baseRotation = root.localRotation;
            particles = root.GetComponentsInChildren<ParticleSystem>(true);
            rates = new float[particles.Length];
            for (int i = 0; i < particles.Length; i++) rates[i] = particles[i].emission.rateOverTimeMultiplier;
            lights = root.GetComponentsInChildren<Light>(true);
            intensities = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) intensities[i] = lights[i].intensity;
            Apply();
        }

        public void Fade(float target, float k)
        {
            if (root == null) return;
            level = Mathf.Lerp(level, target, k);
            if (Mathf.Abs(level - target) < 0.005f) level = target;
            Apply();
        }

        // Наклон вокруг локальной X относительно исходного поворота (боковые сопла при подъёме/спуске).
        public void Tilt(float angle) { if (root != null) root.localRotation = baseRotation * Quaternion.Euler(angle, 0f, 0f); }

        void Apply()
        {
            bool on = level > 0.01f;
            for (int i = 0; i < particles.Length; i++)
            {
                var ps = particles[i];
                var e = ps.emission;
                e.enabled = on;
                e.rateOverTimeMultiplier = rates[i] * level;
                if (on && !ps.isPlaying) ps.Play(false);
            }
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].enabled = on;
                lights[i].intensity = intensities[i] * level;
            }
        }
    }

    [Header("Двигатели")]
    public Glow mainEngine;               // обычная тяга вперёд + её свет
    public Glow cruiseEngine;             // маршевый: горит вместе с основным, пока включён круиз
    public Glow thrusterFrontLeft, thrusterFrontRight, thrusterRearLeft, thrusterRearRight;
    public float boostLevel = 1.5f;       // основной двигатель с Shift
    public float cruiseMainLevel = 1.8f;  // основной двигатель на маршевом
    public float liftTilt = 90f;          // на сколько градусов боковые сопла поворачиваются при подъёме/спуске

    [Header("Огни")]
    public Glow headlights;               // фары: горят, пока кот за рулём
    public Glow tailLights;               // габариты: горят за рулём, ярче при торможении
    public float brakeLevel = 4f;

    [Header("Багажник")]
    public Transform trunkDoor;
    public Vector3 trunkOpenEuler = new Vector3(0f, 0f, -85f); // поворот крышки относительно закрытой
    public float trunkDistance = 4f;      // кот ближе — багажник открывается
    public float trunkSpeed = 2f;         // полных открытий в секунду
    public Glow cabinLight;               // свет в салоне, разгорается вместе с крышкой

    public float fadeSpeed = 8f;          // скорость плавного включения/выключения

    CosmoCarController car;
    CosmoCarSeat seat;
    Quaternion trunkClosed;
    float trunkOpen;
    float sideAngle;

    void Awake()
    {
        car = GetComponent<CosmoCarController>();
        seat = GetComponent<CosmoCarSeat>();
        foreach (var g in All()) g.Init();
        if (trunkDoor != null) trunkClosed = trunkDoor.localRotation;
    }

    Glow[] All() => new[] { mainEngine, cruiseEngine, thrusterFrontLeft, thrusterFrontRight, thrusterRearLeft, thrusterRearRight, headlights, tailLights, cabinLight };

    void Update()
    {
        float k = 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime);
        bool driving = car.enabled && (seat == null || seat.Driving);
        Vector3 t = driving ? car.Thrust : Vector3.zero;

        // Основной и маршевый двигатели.
        float main = t.z > 0f ? car.Cruising ? cruiseMainLevel : car.Boosting ? boostLevel : 1f : 0f;
        mainEngine.Fade(main, k);
        cruiseEngine.Fade(car.Cruising && t.z > 0f ? 1f : 0f, k);

        // Боковые: Q/E — сопла с одной стороны (вправо толкают левые), C/Пробел — все четыре, повёрнутые вниз/вверх.
        float lift = Mathf.Abs(t.y);
        float left = Mathf.Max(lift, t.x > 0f ? 1f : 0f);
        float right = Mathf.Max(lift, t.x < 0f ? 1f : 0f);
        if (t.y != 0f) sideAngle = Mathf.Lerp(sideAngle, -Mathf.Sign(t.y) * liftTilt, k);
        else if (t.x != 0f) sideAngle = Mathf.Lerp(sideAngle, 0f, k);
        thrusterFrontLeft.Fade(left, k);
        thrusterRearLeft.Fade(left, k);
        thrusterFrontRight.Fade(right, k);
        thrusterRearRight.Fade(right, k);
        thrusterFrontLeft.Tilt(sideAngle);
        thrusterRearLeft.Tilt(sideAngle);
        thrusterFrontRight.Tilt(sideAngle);
        thrusterRearRight.Tilt(sideAngle);

        // Фары и габариты.
        headlights.Fade(driving ? 1f : 0f, k);
        tailLights.Fade(driving ? t.z < 0f ? brakeLevel : 1f : 0f, k);

        // Багажник: открыт, пока кот пешком рядом с ним.
        var cat = seat != null ? seat.PlayerBody : null;
        bool open = trunkDoor != null && cat != null && Vector3.Distance(cat.position, trunkDoor.position) < trunkDistance;
        trunkOpen = Mathf.MoveTowards(trunkOpen, open ? 1f : 0f, trunkSpeed * Time.deltaTime);
        if (trunkDoor != null)
            trunkDoor.localRotation = trunkClosed * Quaternion.Euler(trunkOpenEuler * Mathf.SmoothStep(0f, 1f, trunkOpen));
        cabinLight.Fade(trunkOpen, k);
    }
}
