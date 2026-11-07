using System;
using System.Collections.Generic;
using UnityEngine;

// Факты о прохождении — общая память для звонков, вех, поручений и сюжетных цепочек.
// Факт — строковый id и целое число (0 — нет факта). Одни пишут системы (Add/Set), другие
// этот компонент сам снимает с мира раз в полсекунды. Все условия в данных — FactCondition.
//
// Стандартные факты:
//   delivered                 — посылок сдано всего
//   delivered.<заказ>         — сдан заказ (имя ассета DeliveryOrder)
//   delivered_to.<адрес>      — посылок сдано на почту
//   visits.<локация>          — сколько раз прибывал к Location (машиной или пешком)
//   heard.<событие>           — житель рассказал событие истории
//   found.<локация находки>   — находка исследована
//   upgrade.<engine|tank|cargo|economy>, upgrades — уровни улучшений и их сумма
//   debt.paid, debt.left      — выплачено и осталось, кр; debt.pressure — давление 0..100
//   debt.missed               — просрочек всего
//   minutes                   — минут в игре
public class StoryFacts : MonoBehaviour, ISaveable
{
    static readonly Dictionary<string, int> facts = new();

    // id изменившегося факта.
    public static event Action<string> Changed;

    public static int Get(string id) => id != null && facts.TryGetValue(id, out int v) ? v : 0;
    public static bool Has(string id) => Get(id) > 0;

    public static void Add(string id, int amount = 1) => Set(id, Get(id) + amount);

    public static void Set(string id, int value)
    {
        if (string.IsNullOrEmpty(id) || Get(id) == value) return;
        if (value == 0) facts.Remove(id);
        else facts[id] = value;
        Changed?.Invoke(id);
    }

    Debt debt;
    CarUpgrades upgrades;
    Transform car;
    CosmoCarSeat seat;
    float poll, seconds;
    string here;                          // id локации, у которой сейчас кот или машина

    void Awake()
    {
        facts.Clear();                    // статика переживает перезапуск сцены
        Debt.Missed += OnMissed;
        PostTerminal.Delivered += OnDelivered;
    }

    void OnDestroy()
    {
        Debt.Missed -= OnMissed;
        PostTerminal.Delivered -= OnDelivered;
    }

    void Start()
    {
        debt = FindFirstObjectByType<Debt>();
        upgrades = FindFirstObjectByType<CarUpgrades>();
        seat = FindFirstObjectByType<CosmoCarSeat>();
        if (seat != null) car = seat.transform;
    }

    void OnMissed() => Add("debt.missed");

    void OnDelivered(PostTerminal post, List<Parcel> parcels)
    {
        foreach (Parcel p in parcels)
        {
            Add("delivered");
            Add("delivered." + p.order.name);
            Add("delivered_to." + post.address);
        }
    }

    void Update()
    {
        seconds += Time.deltaTime;
        if ((poll -= Time.unscaledDeltaTime) > 0f) return;
        poll = 0.5f;

        Set("minutes", Mathf.FloorToInt(seconds / 60f));
        if (debt != null && debt.settings != null)
        {
            Set("debt.left", debt.Left);
            Set("debt.paid", debt.settings.total - debt.Left);
            Set("debt.pressure", Mathf.RoundToInt(debt.Pressure));
        }
        if (upgrades != null)
        {
            int sum = 0;
            foreach (CarUpgrades.Kind k in Enum.GetValues(typeof(CarUpgrades.Kind)))
            {
                Set("upgrade." + k.ToString().ToLowerInvariant(), upgrades.Level(k));
                sum += upgrades.Level(k);
            }
            Set("upgrades", sum);
        }

        // Прибытие: ближайшая локация, в радиус которой попал кот или машина.
        Vector3 pos = seat != null && seat.PlayerBody != null ? seat.PlayerBody.position : car != null ? car.position : Vector3.zero;
        string now = null;
        foreach (Location l in Location.All)
            if (!string.IsNullOrEmpty(l.id) && (l.transform.position - pos).sqrMagnitude < l.radius * l.radius * 4f) { now = l.id; break; }
        if (now != here && now != null) Add("visits." + now);
        here = now;
    }

    // Сохранение: «id=значение;id=значение» и время в игре.
    [Serializable]
    class State
    {
        public List<string> ids = new();
        public List<int> values = new();
        public float seconds;
    }

    public string SaveKey => "facts";

    public string Save()
    {
        var st = new State { seconds = seconds };
        foreach (var kv in facts) { st.ids.Add(kv.Key); st.values.Add(kv.Value); }
        return JsonUtility.ToJson(st);
    }

    public void Load(string data)
    {
        State st = JsonUtility.FromJson<State>(data);
        if (st == null) return;
        facts.Clear();
        for (int i = 0; i < st.ids.Count && i < st.values.Count; i++) facts[st.ids[i]] = st.values[i];
        seconds = st.seconds;
        Changed?.Invoke(null);
    }
}

// Условие в данных: факт сравнивается с числом. «Не было» — Below 1.
[Serializable]
public class FactCondition
{
    public enum Compare { AtLeast, Below }

    public string fact;
    public Compare compare = Compare.AtLeast;
    public int value = 1;

    public bool Met => compare == Compare.AtLeast ? StoryFacts.Get(fact) >= value : StoryFacts.Get(fact) < value;

    public static bool All(FactCondition[] conditions)
    {
        if (conditions == null) return true;
        foreach (FactCondition c in conditions)
            if (c != null && !string.IsNullOrEmpty(c.fact) && !c.Met) return false;
        return true;
    }

    public override string ToString() => $"{fact} {(compare == Compare.AtLeast ? ">=" : "<")} {value}";
}
