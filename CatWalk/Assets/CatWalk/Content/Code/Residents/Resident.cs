using System.Collections.Generic;
using Febucci.TextAnimatorForUnity;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Житель у почты своей станции. Поворачивается к коту, когда тот рядом; при подходе машет и здоровается.
// Без анимаций: в покое модель «дышит» скейлом, а взмах — скейл-баунс на пружине, как приземление кота.
// F рядом — новое событие истории, если оно открылось (над головой тогда висит «!»), иначе следующая тема.
// После доставки на свою почту — реакция на груз или благодарность.
// Реплика — облачко над головой: печатается посимвольно (Text Animator), висит lineTime и не мешает ходить.
// Помнит доставки сюда, последний груз, знакомство, тему и рассказанное — в сохранении по адресу почты.
public class Resident : MonoBehaviour, ISaveable
{
    public ResidentProfile profile;
    public PostTerminal terminal;         // почта, у которой живёт
    public Transform visual;              // модель с пивотом в ногах

    [Header("Облачко")]
    public Canvas bubble;                 // world-space, всегда смотрит в камеру
    public TMP_Text text;
    public TypewriterComponent typewriter;
    public float lineTime = 5.5f;         // сколько висит реплика, с; длинную — не меньше, чем печатается, и ещё 2 с
    public GameObject newsIcon;           // «!» — есть новое событие истории

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

    [System.Serializable]
    class Memory
    {
        public int deliveries;            // посылок, сданных на эту почту
        public string lastCargo;
        public bool known;
        public int topic;
        public List<string> heard = new(); // рассказанные события истории
    }

    Memory memory = new();
    bool catNear;
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

    // Кот пешком; в машине его нет. Пока открыто меню, кот на месте (его скрипты лишь выключены) — житель его видит.
    Transform Cat => ParcelCarrier.Current != null ? ParcelCarrier.Current.transform : null;

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
        ResidentProfile.StoryEvent news = News();
        if (!menu && distance < talkDistance && !ParcelCarrier.Current.HasAction)
        {
            Hud.Prompt(news != null ? "F — поговорить · есть новости" : "F — поговорить", 9);
            Keyboard k = Keyboard.current;
            if (k != null && k.fKey.wasPressedThisFrame) Talk();
        }

        if (!menu && pending != null) Show();
        bubble.enabled = !menu && Time.time < hideAt;
        newsIcon.SetActive(news != null && !bubble.enabled);
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (bubble.enabled && cam != null) bubble.transform.rotation = cam.transform.rotation;
    }

    bool Speaking => pending != null || Time.time < hideAt;

    string Greeting() => memory.deliveries >= profile.warmDeliveries ? profile.greetingWarm
                       : memory.known ? profile.greetingKnown : profile.greeting;

    // Первое открывшееся и ещё не рассказанное событие истории.
    ResidentProfile.StoryEvent News() => System.Array.Find(profile.story, e => !memory.heard.Contains(e.id)
        && memory.deliveries >= e.minDeliveries && (string.IsNullOrEmpty(e.after) || memory.heard.Contains(e.after)));

    void Talk()
    {
        memory.known = true;
        ResidentProfile.StoryEvent news = News();
        if (news != null)
        {
            memory.heard.Add(news.id);
            Say(news.line);
            return;
        }
        if (profile.topics.Length == 0) return;
        memory.topic %= profile.topics.Length;
        Say(profile.topics[memory.topic++]);
    }

    void OnDelivered(PostTerminal post, List<Parcel> parcels)
    {
        if (post != terminal) return;
        memory.deliveries += parcels.Count;
        memory.known = true;
        memory.lastCargo = parcels[^1].order.cargo;
        ResidentProfile.CargoReaction reaction = System.Array.Find(profile.reactions, r => parcels.Exists(p => p.order == r.order));
        string line = reaction != null ? reaction.line : profile.thanks;
        if (parcels.Exists(p => p.Fragile && p.condition < 0.75f) && !string.IsNullOrEmpty(profile.damagedRemark)) line += " " + profile.damagedRemark;
        Say(line);
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

    public string SaveKey => "resident:" + terminal.address;
    public string Save() => JsonUtility.ToJson(memory);
    public void Load(string data) => memory = JsonUtility.FromJson<Memory>(data) ?? new Memory();
}
