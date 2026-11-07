using UnityEngine;

// Улучшения космокара, покупаются в мастерской. У каждого направления 0..maxLevel уровней.
// Двигатель — скорость маршевого режима, бак — вместимость, трюм — места на полке, экономичность — расход.
[RequireComponent(typeof(FuelTank), typeof(CosmoCarController))]
public class CarUpgrades : MonoBehaviour, ISaveable
{
    public enum Kind { Engine, Tank, Cargo, Economy }

    public int maxLevel = 4;

    [Header("Двигатель")]
    public float cruisePerLevel = 0.15f;  // доля к скорости маршевого за уровень

    [Header("Бак")]
    public float baseCapacity = 100f;     // л
    public float capacityPerLevel = 55f;  // л

    [Header("Трюм")]
    public GameObject[] extraSlots;       // по одной группе слотов на уровень, включаются по порядку

    [Header("Экономичность")]
    public float efficiencyPerLevel = 0.24f; // расход делится на 1 + это × уровень

    [Header("Цена первого уровня, кр; следующий — цена × (уровень + 1)")]
    public int engineCost = 650;
    public int tankCost = 420;
    public int cargoCost = 380;
    public int economyCost = 480;

    readonly int[] levels = new int[4];

    public int Level(Kind kind) => levels[(int)kind];

    // У трюма уровней не больше, чем групп слотов.
    public int MaxLevel(Kind kind) => kind == Kind.Cargo ? Mathf.Min(maxLevel, extraSlots.Length) : maxLevel;

    public bool IsMax(Kind kind) => Level(kind) >= MaxLevel(kind);

    public int Cost(Kind kind) => BaseCost(kind) * (Level(kind) + 1);

    int BaseCost(Kind kind) => kind switch
    {
        Kind.Engine => engineCost,
        Kind.Tank => tankCost,
        Kind.Cargo => cargoCost,
        _ => economyCost,
    };

    void Awake() => Apply();

    public bool TryBuy(Kind kind)
    {
        if (IsMax(kind) || !Wallet.Current.TrySpend(Cost(kind))) return false;
        levels[(int)kind]++;
        Apply();
        return true;
    }

    void Apply()
    {
        FuelTank tank = GetComponent<FuelTank>();
        tank.capacity = baseCapacity + capacityPerLevel * Level(Kind.Tank);
        tank.efficiency = 1f + efficiencyPerLevel * Level(Kind.Economy);
        GetComponent<CosmoCarController>().cruiseScale = 1f + cruisePerLevel * Level(Kind.Engine);
        for (int i = 0; i < extraSlots.Length; i++) extraSlots[i].SetActive(i < Level(Kind.Cargo));
    }

    public string SaveKey => "upgrades";
    public string Save() => string.Join(",", levels);

    public void Load(string data)
    {
        string[] parts = data.Split(',');
        for (int i = 0; i < levels.Length && i < parts.Length; i++) levels[i] = Mathf.Clamp(int.Parse(parts[i]), 0, maxLevel);
        Apply();
    }
}
