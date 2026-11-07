using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

// Чёрная дыра и финал «Вечная пустота». Стоит там, где авантюрист принимает находку (Trickster).
// После передачи ждёт, пока машина отлетит дальше blastDistance, — взрыв, и дыра растёт: радиус startRadius + growth·t²,
// машину (и кота пешком) тянет к центру с ускорением pull + pullGrowth·t — со временем сильнее любой тяги.
// Пока тянет, связь есть: приходят сообщения (TricksterProfile.messages). Машина или кот внутри радиуса — пустота:
// мир прячется (все корни сцены, кроме машины, кота, камер, экранного слоя, систем и этого объекта), долг, навигация
// и буксир выключаются, сообщения и баланс не показываются, небо чёрное, топливо не кончается.
// В пустоте можно летать, выйти из машины где угодно (E) и вернуться; Esc — VoidMenu вместо паузы.
// Пустота сохраняется сразу (факт trickster.void), и загрузка возвращает в неё. Время с взрыва — факт trickster.hole.
public class BlackHole : MonoBehaviour
{
    public TricksterProfile profile;
    public Transform visual;              // шар с кольцами: масштаб 1 — радиус 1 м
    public GameObject explosion;          // эффект взрыва, горит explosionTime
    public GameObject voidLight;          // тусклый свет в пустоте
    public VoidMenu menu;

    public float blastDistance = 700f;    // от центра до машины, м
    public float startRadius = 30f;       // м
    public float growth = 0.5f;           // м/с²
    public float pull = 4f;               // м/с²
    public float pullGrowth = 2f;         // прибавка тяги за секунду, м/с²
    public float explosionTime = 12f;     // с
    public Color voidAmbient = new Color(0.12f, 0.12f, 0.15f);

    CosmoCarController car;
    CosmoCarSeat seat;
    FuelTank tank;
    float time;                           // с от взрыва
    int message;                          // следующее сообщение
    bool inVoid, exitQueued, menuWasOpen;

    float Radius => startRadius + growth * time * time;

    void Start()
    {
        car = FindFirstObjectByType<CosmoCarController>();
        seat = car.GetComponent<CosmoCarSeat>();
        tank = car.GetComponent<FuelTank>();
        visual.gameObject.SetActive(false);
        explosion.SetActive(false);
        voidLight.SetActive(false);
    }

    void Update()
    {
        if (StoryFacts.Has("trickster.void"))
        {
            if (!inVoid) EnterVoid(false);
            Wander();
            return;
        }

        int hole = StoryFacts.Get("trickster.hole");
        if (hole == 0)
        {
            if (!StoryFacts.Has("trickster.handed") || Vector3.Distance(car.transform.position, transform.position) < blastDistance) return;
            time = 0f;
            message = 0;
            explosion.SetActive(true);
        }
        else if (Mathf.Abs(hole - 1 - time) > 2f)
        {
            // Загрузили сохранение посреди затягивания: старые сообщения уже были.
            time = hole - 1;
            message = System.Array.FindIndex(profile.messages, m => m.at > time);
            if (message < 0) message = profile.messages.Length;
        }

        time += Time.deltaTime;
        StoryFacts.Set("trickster.hole", 1 + Mathf.FloorToInt(time));
        visual.gameObject.SetActive(true);
        visual.localScale = Vector3.one * Radius;
        if (explosion.activeSelf && time > explosionTime) explosion.SetActive(false);
        if (message < profile.messages.Length && time >= profile.messages[message].at) Hud.Toast(profile.messages[message++].text);

        if (Inside(car.transform.position) || seat.PlayerBody != null && Inside(seat.PlayerBody.position)) EnterVoid(true);
    }

    void FixedUpdate()
    {
        if (inVoid || StoryFacts.Get("trickster.hole") == 0) return;
        float accel = pull + pullGrowth * time;
        Pull(car.GetComponent<Rigidbody>(), accel);
        if (seat.PlayerBody != null) Pull(seat.PlayerBody.GetComponent<Rigidbody>(), accel);
    }

    void LateUpdate() => menuWasOpen = MenuScreen.Opened != null;

    void Pull(Rigidbody body, float accel) =>
        body.AddForce((transform.position - body.worldCenterOfMass).normalized * accel, ForceMode.Acceleration);

    bool Inside(Vector3 position) => (position - transform.position).sqrMagnitude < Radius * Radius;

    // fresh — только что затянуло: остановить машину, кота — к ней, сохранить. Иначе — пустота из сохранения.
    void EnterVoid(bool fresh)
    {
        inVoid = true;
        StoryFacts.Set("trickster.void", 1);
        Transform carT = car.transform;
        if (fresh)
        {
            car.Teleport(carT.position, carT.rotation);
            if (seat.PlayerBody != null) seat.SpawnPlayer(carT.position - carT.right * seat.exitSide, carT.rotation);
        }

        var keep = new HashSet<Transform> { transform.root, carT.root };
        if (seat.PlayerBody != null) keep.Add(seat.PlayerBody.root);
        if (Camera.main != null) keep.Add(Camera.main.transform.root);
        foreach (Component c in new Component[] { FindFirstObjectByType<CosmoCarCamera>(FindObjectsInactive.Include), Hud.Current,
                     SaveGame.Current, FindFirstObjectByType<StoryFacts>(), EventSystem.current })
            if (c != null) keep.Add(c.transform.root);
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            if (!keep.Contains(root.transform) && root.GetComponent("Volume") == null) root.SetActive(false);

        // Связи нет.
        Disable<Debt>();
        Disable<Navigator>();
        Disable<Tow>();
        if (Hud.Current != null) Hud.Current.toast.enabled = false;
        foreach (BalanceHud b in FindObjectsByType<BalanceHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Canvas canvas = b.GetComponentInParent<Canvas>(true);
            if (canvas != null) canvas.enabled = false;
        }
        if (SaveGame.Current != null) SaveGame.Current.pause = null; // Esc — своё меню

        visual.gameObject.SetActive(false);
        explosion.SetActive(false);
        voidLight.SetActive(true);
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = voidAmbient;

        if (fresh && SaveGame.Current != null) SaveGame.Current.Save();
    }

    static void Disable<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)) b.enabled = false;
    }

    void Wander()
    {
        foreach (Camera c in Camera.allCameras)
        {
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
        }
        tank.Fill(tank.Free);

        // Выход — со следующего кадра, чтобы та же E не посадила кота обратно (CosmoCarSeat).
        if (exitQueued && seat.Driving) seat.SpawnPlayer(car.transform.position - car.transform.right * seat.exitSide, car.transform.rotation);
        exitQueued = false;

        if (MenuScreen.Opened != null) return;
        if (seat.Driving)
        {
            Hud.Prompt(Controls.Hint(Controls.Interact, "выйти"), 5);
            if (Controls.Pressed(Controls.Interact)) exitQueued = true;
        }
        if (Controls.Pressed(Controls.Pause) && !menuWasOpen) menu.Open();
    }
}
