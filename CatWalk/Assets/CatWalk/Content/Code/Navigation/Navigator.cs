using System.Collections.Generic;
using UnityEngine;

// Навигатор космокара: текущая цель на карте. Работает и в машине, и пешком.
// T — следующая цель по кругу, от ближайшей к дальней. M — карта (MapMenu), J — журнал заказов (OrderJournal).
// Расстояния считаются от машины.
public class Navigator : MonoBehaviour, ISaveable
{
    public static Navigator Current { get; private set; }

    public MapMenu map;                   // по умолчанию — карта в сцене
    public OrderJournal journal;          // по умолчанию — журнал в сцене

    public Location Target { get; set; }

    bool menuWasOpen;                     // клавиша, которой только что закрыли меню, его снова не открывает

    void Awake() => Current = this;

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Start()
    {
        if (map == null) map = FindFirstObjectByType<MapMenu>(FindObjectsInactive.Include);
        if (journal == null) journal = FindFirstObjectByType<OrderJournal>(FindObjectsInactive.Include);
    }

    void Update()
    {
        if (menuWasOpen) return;
        if (Controls.Pressed(Controls.Map) && map != null) map.Open(this);
        if (Controls.Pressed(Controls.Journal) && journal != null) journal.Open();
        if (Controls.Pressed(Controls.Target)) Next();
    }

    void LateUpdate() => menuWasOpen = MenuScreen.Opened != null;

    public float DistanceTo(Location location) => Vector3.Distance(transform.position, location.transform.position);

    // Все точки, от ближайшей к дальней.
    public List<Location> Sorted()
    {
        var list = new List<Location>(Location.All);
        list.Sort((a, b) => DistanceTo(a).CompareTo(DistanceTo(b)));
        return list;
    }

    // Следующая по удалённости после текущей; после самой дальней — снова ближайшая.
    void Next()
    {
        List<Location> sorted = Sorted();
        if (sorted.Count == 0)
        {
            Hud.Toast("На карте нет ни одной точки");
            return;
        }
        Target = sorted[(sorted.IndexOf(Target) + 1) % sorted.Count];
        Hud.Toast($"Цель: {Target.Label} | {FormatDistance(DistanceTo(Target))}");
    }

    // «850 м», «2,4 км».
    public static string FormatDistance(float meters) => meters >= 1000f ? $"{meters / 1000f:0.0} км" : $"{meters:0} м";

    public string SaveKey => "navigator";
    public string Save() => Target != null ? Target.id : "";
    public void Load(string data) => Target = Location.Find(data);
}
