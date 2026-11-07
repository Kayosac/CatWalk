using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Сохранение игры, одно на сцену: JSON-файл в persistentDataPath.
// Хранит строки всех ISaveable в сцене (включая неактивные) и своё ядро: баланс, топливо, позу и скорость машины,
// кота пешком и все посылки. Загружается само через кадр после старта сцены — когда все системы прошли Start.
// «Загрузить» перезапускает сцену, и та подхватывает файл. Автосохранение: после доставки и трат (как только
// закрыто меню), когда машина встала на парковку, при выходе из игры.
public class SaveGame : MonoBehaviour
{
    const int Version = 1;                // формат файла; другой номер — файл не читаем

    public static SaveGame Current { get; private set; }
    public static string FilePath => Path.Combine(Application.persistentDataPath, "catwalk-prototype.json");
    public static bool Exists => File.Exists(FilePath);

    public float dockSaveDelay = 3f;      // после загрузки машина оседает на парковку — это ещё не повод сохраняться, с

    [Serializable]
    class Entry
    {
        public string key, data;
    }

    [Serializable]
    class ParcelData
    {
        public string order;              // имя ассета DeliveryOrder
        public string slot;               // id слота (SlotId); пусто — в руках или свободно
        public bool held;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    class Data
    {
        public int version;
        public int balance;
        public float fuel;
        public Vector3 carPosition, carVelocity;
        public Quaternion carRotation;
        public bool walking;
        public Vector3 catPosition;
        public Quaternion catRotation;
        public List<ParcelData> parcels = new();
        public List<Entry> systems = new();
    }

    CosmoCarController car;
    CosmoCarSeat seat;
    FuelTank tank;
    ParkingZone[] zones;
    bool ready, pending, docked;
    float readyTime;

    void Awake() => Current = this;

    void OnEnable() => PostTerminal.Delivered += OnDelivered;
    void OnDisable() => PostTerminal.Delivered -= OnDelivered;

    void OnDestroy()
    {
        if (Wallet.Current != null) Wallet.Current.Changed -= OnWalletChanged;
        if (Current == this) Current = null;
    }

    IEnumerator Start()
    {
        car = FindFirstObjectByType<CosmoCarController>();
        if (car != null) seat = car.GetComponent<CosmoCarSeat>();
        tank = FindFirstObjectByType<FuelTank>();
        zones = FindObjectsByType<ParkingZone>(FindObjectsSortMode.None);
        if (Wallet.Current != null) Wallet.Current.Changed += OnWalletChanged;

        yield return null;                // все системы прошли Start
        if (Exists) Load();
        pending = false;
        ready = true;
        readyTime = Time.time;
    }

    void OnWalletChanged(int amount)
    {
        if (amount < 0) pending = true;
    }

    void OnDelivered(PostTerminal terminal, List<Parcel> parcels) => pending = true;

    void Update()
    {
        if (!ready) return;
        bool nowDocked = seat != null && seat.Driving && Array.Exists(zones, z => z.IsDocked);
        if (nowDocked && !docked && Time.time > readyTime + dockSaveDelay) pending = true;
        docked = nowDocked;
        if (pending && MenuScreen.Opened == null) Save();
    }

    void OnApplicationQuit()
    {
        if (ready) Save(false);
    }

    public void Save(bool toast = true)
    {
        File.WriteAllText(FilePath, JsonUtility.ToJson(Collect(), true));
        pending = false;
        if (toast) Hud.Toast("Сохранено");
    }

    // Последнее сохранение: сцена с нуля, файл подхватится на старте.
    public void LoadLast() => Reload();

    public void NewGame()
    {
        if (Exists) File.Delete(FilePath);
        Reload();
    }

    void Reload()
    {
        ready = false;
        if (MenuScreen.Opened != null) MenuScreen.Opened.Close(); // вернуть время и курсор
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    static IEnumerable<ISaveable> Saveables()
    {
        foreach (MonoBehaviour b in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b is ISaveable s) yield return s;
    }

    Data Collect()
    {
        var d = new Data { version = Version };
        foreach (ISaveable s in Saveables()) d.systems.Add(new Entry { key = s.SaveKey, data = s.Save() });
        if (Wallet.Current != null) d.balance = Wallet.Current.Balance;
        if (tank != null) d.fuel = tank.Fuel;
        if (car != null)
        {
            Rigidbody rb = car.GetComponent<Rigidbody>();
            d.carPosition = rb.position;
            d.carRotation = rb.rotation;
            d.carVelocity = rb.linearVelocity;
        }
        if (seat != null && !seat.Driving)
        {
            d.walking = true;
            d.catPosition = seat.PlayerBody.position;
            d.catRotation = seat.PlayerBody.rotation;
        }

        ParcelCarrier carrier = ParcelCarrier.Current;
        foreach (Parcel parcel in Parcel.All)
        {
            if (parcel.order == null) continue;
            d.parcels.Add(new ParcelData
            {
                order = parcel.order.name,
                slot = parcel.Slot != null ? SlotId(parcel.Slot) : "",
                held = parcel.Slot == null && carrier != null && carrier.Held == parcel,
                position = parcel.transform.position,
                rotation = parcel.transform.rotation,
            });
        }
        return d;
    }

    void Load()
    {
        Data d = null;
        try { d = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)); }
        catch (Exception e) { Debug.LogWarning("Сохранение не читается: " + e.Message); }
        if (d == null || d.version != Version)
        {
            Debug.LogWarning("Сохранение другой версии или битое — начинаем с нуля.");
            return;
        }

        // Сначала системы: улучшения меняют бак и включают полки, на которые потом встанут посылки.
        var systems = new Dictionary<string, string>();
        foreach (Entry e in d.systems) systems[e.key] = e.data;
        foreach (ISaveable s in Saveables())
            if (systems.TryGetValue(s.SaveKey, out string data)) s.Load(data);

        if (Wallet.Current != null) Wallet.Current.Set(d.balance);
        if (tank != null) tank.Set(d.fuel);
        if (car != null)
        {
            car.Teleport(d.carPosition, d.carRotation);
            car.GetComponent<Rigidbody>().linearVelocity = d.carVelocity;
        }
        if (d.walking && seat != null) seat.SpawnPlayer(d.catPosition, d.catRotation);
        LoadParcels(d.parcels);
    }

    void LoadParcels(List<ParcelData> saved)
    {
        // Посылки, расставленные в сцене заранее, заменяются сохранёнными.
        foreach (Parcel parcel in Parcel.All.ToArray())
        {
            if (parcel.Slot != null) parcel.Slot.Parcel = null;
            parcel.gameObject.SetActive(false);
            Destroy(parcel.gameObject);
        }

        var slots = new Dictionary<string, ParcelSlot>();
        foreach (ParcelSlot slot in FindObjectsByType<ParcelSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            slots[SlotId(slot)] = slot;

        // Заказ ищется по заказам всех почт; посылку делает префаб той почты, что его выдаёт.
        var orders = new Dictionary<string, (DeliveryOrder order, PostTerminal from)>();
        foreach (PostTerminal terminal in PostTerminal.All)
            foreach (DeliveryOrder order in terminal.orders)
                if (order != null) orders[order.name] = (order, terminal);

        foreach (ParcelData p in saved)
        {
            if (!orders.TryGetValue(p.order, out var found) || found.from.parcelPrefab == null) continue;
            Parcel parcel = Instantiate(found.from.parcelPrefab);
            parcel.order = found.order;
            if (!string.IsNullOrEmpty(p.slot) && slots.TryGetValue(p.slot, out ParcelSlot slot)) parcel.Attach(slot.transform, slot);
            else if (p.held && ParcelCarrier.Current != null) ParcelCarrier.Current.Hold(parcel);
            else parcel.Drop(p.position, p.rotation, Vector3.zero);
        }
    }

    // Постоянный id слота: почта + зона + индекс, полка машины + индекс, иначе путь в иерархии.
    string SlotId(ParcelSlot slot)
    {
        foreach (PostTerminal terminal in PostTerminal.All)
        {
            int i = Array.IndexOf(terminal.issueSlots, slot);
            if (i >= 0) return $"{terminal.address}/issue/{i}";
            i = Array.IndexOf(terminal.receiveSlots, slot);
            if (i >= 0) return $"{terminal.address}/receive/{i}";
        }
        if (car != null && slot.transform.IsChildOf(car.transform))
            return "car/" + Array.IndexOf(car.GetComponentsInChildren<ParcelSlot>(true), slot);
        string path = slot.name;
        for (Transform t = slot.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
        return path;
    }
}
