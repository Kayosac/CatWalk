using System.Collections.Generic;
using Febucci.TextAnimatorForUnity;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Житель у почты своей станции. Поворачивается к коту, когда тот рядом; при подходе машет и здоровается.
// Без анимаций: в покое модель «дышит» скейлом, а взмах — скейл-баунс на пружине, как приземление кота.
// F рядом — следующая тема разговора. После доставки на свою почту — реакция на груз или благодарность.
// Реплика — облачко над головой: печатается посимвольно (Text Animator), висит lineTime и не мешает ходить.
public class Resident : MonoBehaviour
{
    public ResidentProfile profile;
    public PostTerminal terminal;         // почта, у которой живёт
    public Transform visual;              // модель с пивотом в ногах

    [Header("Облачко")]
    public Canvas bubble;                 // world-space, всегда смотрит в камеру
    public TMP_Text text;
    public TypewriterComponent typewriter;
    public float lineTime = 5.5f;         // сколько висит реплика, с; длинную — не меньше, чем печатается, и ещё 2 с

    [Header("Внимание")]
    public float lookRadius = 6f;         // ближе — поворачивается к коту и здоровается, м
    public float talkDistance = 2.5f;     // F — поговорить, м
    public float turnSpeed = 180f;        // °/с

    [Header("Скейл")]
    public float breathAmount = 0.015f;   // доля высоты
    public float breathFrequency = 0.3f;  // вдохов в секунду
    public float waveKick = 2.5f;         // толчок пружины при взмахе
    public float squashStiffness = 120f;
    public float squashDamping = 7f;

    int deliveries, topic;
    bool known, catNear;
    string pending;                       // ждёт, пока закроют меню
    float hideAt, squash, squashVelocity;
    Quaternion restRotation;
    Vector3 baseScale;

    void Awake()
    {
        restRotation = transform.rotation;
        baseScale = visual.localScale;
        bubble.enabled = false;
    }

    void OnEnable() => PostTerminal.Delivered += OnDelivered;
    void OnDisable() => PostTerminal.Delivered -= OnDelivered;

    Transform Cat => ParcelCarrier.Current != null && ParcelCarrier.Current.isActiveAndEnabled ? ParcelCarrier.Current.transform : null;

    void Update()
    {
        Transform cat = Cat;
        float distance = cat != null ? Vector3.Distance(cat.position, transform.position) : float.MaxValue;

        // Подошёл — машет и здоровается; уходить надо чуть дальше, чтобы не здороваться на каждом шаге у границы.
        if (!catNear && distance < lookRadius)
        {
            catNear = true;
            squashVelocity += waveKick;
            if (!Speaking) Say(Greeting());
        }
        else if (catNear && distance > lookRadius + 2f) catNear = false;

        Turn(catNear ? cat : null);
        Bounce();

        bool menu = MenuScreen.Opened != null;
        if (!menu && distance < talkDistance && !ParcelCarrier.Current.HasAction)
        {
            Hud.Prompt("F — поговорить", 9);
            Keyboard k = Keyboard.current;
            if (k != null && k.fKey.wasPressedThisFrame) Talk();
        }

        if (!menu && pending != null) Show();
        bubble.enabled = !menu && Time.time < hideAt;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (bubble.enabled && cam != null) bubble.transform.rotation = cam.transform.rotation;
    }

    bool Speaking => pending != null || Time.time < hideAt;

    string Greeting() => deliveries >= profile.warmDeliveries ? profile.greetingWarm : known ? profile.greetingKnown : profile.greeting;

    void Talk()
    {
        known = true;
        if (profile.topics.Length == 0) return;
        topic %= profile.topics.Length;
        Say(profile.topics[topic++]);
    }

    void OnDelivered(PostTerminal post, List<Parcel> parcels)
    {
        if (post != terminal) return;
        deliveries += parcels.Count;
        known = true;
        ResidentProfile.CargoReaction reaction = System.Array.Find(profile.reactions, r => parcels.Exists(p => p.order == r.order));
        Say(reaction != null ? reaction.line : profile.thanks);
    }

    // Реплика показывается сразу, а если открыто меню — как только его закроют.
    void Say(string line)
    {
        if (!string.IsNullOrEmpty(line)) pending = line;
    }

    void Show()
    {
        bubble.enabled = true;
        typewriter.ShowText($"<b>{profile.displayName}</b>\n{pending}");
        hideAt = Time.time + Mathf.Max(lineTime, typewriter.GetApproximateShowDuration() + 2f);
        pending = null;
    }

    // Лицом к коту в плоскости пола; без кота — обратно, как поставили.
    void Turn(Transform cat)
    {
        Vector3 up = restRotation * Vector3.up;
        Vector3 toCat = cat != null ? Vector3.ProjectOnPlane(cat.position - transform.position, up) : Vector3.zero;
        Quaternion target = toCat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCat, up) : restRotation;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    // Высота = дыхание + пружина взмаха; объём сохраняется, как у кота.
    void Bounce()
    {
        float dt = Time.deltaTime;
        squashVelocity += (-squashStiffness * squash - squashDamping * squashVelocity) * dt;
        squash += squashVelocity * dt;
        float y = Mathf.Max(0.5f, 1f + breathAmount * Mathf.Sin(Time.time * breathFrequency * 2f * Mathf.PI) + squash);
        float xz = 1f / Mathf.Sqrt(y);
        visual.localScale = Vector3.Scale(baseScale, new Vector3(xz, y, xz));
    }
}
