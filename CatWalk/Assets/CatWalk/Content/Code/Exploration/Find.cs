using System.Collections.Generic;
using UnityEngine;

// Находка в пустоте: обломки или сигнал. Точка на карте — Location (kind Find) на том же объекте, её id — ключ сохранения.
// Подлететь на машине ближе range и медленнее maxSpeed — F, исследование идёт scanTime секунд; улетел дальше — прервалось.
// Награда один раз: кредиты, обломки — ещё топливо в бак, сигнал — запись (Toast, потом её можно показать в журнале).
// Исследованная находка гасит маяк.
[RequireComponent(typeof(Location))]
public class Find : MonoBehaviour, ISaveable
{
    public enum Kind { Debris, Signal }

    public static readonly List<Find> All = new();

    public Kind kind;
    public int credits = 170;
    public float fuel = 18f;              // л, до полного бака
    [TextArea] public string record;      // что нашлось в сигнале

    public float range = 30f;             // от машины до находки, м
    public float maxSpeed = 6f;           // м/с
    public float scanTime = 3f;           // с
    public GameObject scanVfx;            // включён, пока идёт исследование
    public GameObject beacon;             // маяк: гаснет, когда исследовано

    public bool Explored { get; private set; }

    CosmoCarController car;
    Rigidbody carBody;
    float progress = -1f;                 // < 0 — не исследуем

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        car = FindFirstObjectByType<CosmoCarController>();
        if (car != null) carBody = car.GetComponent<Rigidbody>();
        scanVfx.SetActive(false);
    }

    void Update()
    {
        if (Explored || car == null) return;
        bool inRange = car.enabled && Vector3.Distance(car.transform.position, transform.position) < range;

        if (progress >= 0f)
        {
            if (!inRange)
            {
                Stop();
                Hud.Toast("Исследование прервано");
                return;
            }
            progress += Time.deltaTime / scanTime;
            Hud.Prompt($"Исследование... {Mathf.Min(progress, 1f) * 100f:0}%", 15);
            if (progress >= 1f) Complete();
            return;
        }

        // F в машине ещё и выравнивает — это не мешает.
        if (!inRange || MenuScreen.Opened != null || carBody.linearVelocity.magnitude > maxSpeed) return;
        Hud.Prompt(Controls.Hint(Controls.Use, "исследовать"), 15);
        if (!Controls.Pressed(Controls.Use)) return;
        progress = 0f;
        scanVfx.SetActive(true);
    }

    void Stop()
    {
        progress = -1f;
        scanVfx.SetActive(false);
    }

    void Complete()
    {
        Stop();
        Wallet.Current.Add(credits);
        string title = GetComponent<Location>().title;
        if (kind == Kind.Debris)
        {
            FuelTank tank = car.GetComponent<FuelTank>();
            float liters = Mathf.Min(fuel, tank.Free);
            tank.Fill(liters);
            Hud.Toast($"{title}: +{Wallet.Format(credits)}, +{liters:0} л топлива");
        }
        else Hud.Toast($"{title}: {record}\n+{Wallet.Format(credits)}");
        SetExplored(true);
        StoryFacts.Set("found." + GetComponent<Location>().id, 1);
    }

    void SetExplored(bool explored)
    {
        Explored = explored;
        beacon.SetActive(!explored);
    }

    public string SaveKey => "find:" + GetComponent<Location>().id;
    public string Save() => Explored ? "1" : "0";
    public void Load(string data) => SetExplored(data == "1");
}
