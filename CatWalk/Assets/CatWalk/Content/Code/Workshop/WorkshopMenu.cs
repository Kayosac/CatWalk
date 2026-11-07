using UnityEngine;

// Экранное меню мастерской: починить корпус и купить следующий уровень каждого улучшения (у ремонтной платформы — только починить).
public class WorkshopMenu : MenuScreen
{
    WorkshopTerminal workshop;

    public void Open(WorkshopTerminal owner)
    {
        workshop = owner;
        if (Show()) Refresh();
    }

    protected override void Refresh()
    {
        ClearRows();
        title.text = $"{(workshop.repairOnly ? "Ремонт" : "Мастерская")} | на счету {Wallet.Format(Wallet.Current.Balance)}";

        int missing = workshop.Missing;
        int percents = workshop.RepairPercents;
        int cost = percents * workshop.pricePerPercent;
        string now = $"корпус {Mathf.CeilToInt(workshop.Hull.Condition)} %";
        if (missing <= 0) AddRow("Корпус цел | 100 %");
        else if (percents <= 0) AddRow($"Починить — не хватает кредитов ({workshop.pricePerPercent} кр за %) | {now}");
        else AddRow((percents >= missing ? $"Починить полностью | {Wallet.Format(cost)}" : $"Починить на {percents} % | {Wallet.Format(cost)}") + $" | {now}",
            () => $"Корпус починен на {workshop.Repair()} %.");

        if (workshop.repairOnly) return;
        CarUpgrades car = workshop.Upgrades;
        foreach (CarUpgrades.Kind kind in System.Enum.GetValues(typeof(CarUpgrades.Kind)))
        {
            string line = $"{Name(kind)} | ур. {car.Level(kind)}/{car.MaxLevel(kind)}";
            if (car.IsMax(kind))
            {
                AddRow(line + " | максимум");
                continue;
            }
            int price = car.Cost(kind);
            AddRow($"{line} | {NextEffect(car, kind)} | {Wallet.Format(price)}",
                Wallet.Current.Balance >= price ? () => car.TryBuy(kind) ? $"{Name(kind)}: уровень {car.Level(kind)}." : null : null);
        }
    }

    static string Name(CarUpgrades.Kind kind) => kind switch
    {
        CarUpgrades.Kind.Engine => "Двигатель",
        CarUpgrades.Kind.Tank => "Бак",
        CarUpgrades.Kind.Cargo => "Трюм",
        _ => "Экономичность",
    };

    // Что даст следующий уровень.
    static string NextEffect(CarUpgrades car, CarUpgrades.Kind kind)
    {
        int level = car.Level(kind);
        switch (kind)
        {
            case CarUpgrades.Kind.Engine:
                return $"скорость маршевого +{Mathf.RoundToInt(car.cruisePerLevel * level * 100f)} -> +{Mathf.RoundToInt(car.cruisePerLevel * (level + 1) * 100f)} %";
            case CarUpgrades.Kind.Tank:
                float capacity = car.baseCapacity + car.capacityPerLevel * level;
                return $"бак {capacity:0} -> {capacity + car.capacityPerLevel:0} л";
            case CarUpgrades.Kind.Cargo:
                return $"мест на полке +{car.extraSlots[level].GetComponentsInChildren<ParcelSlot>(true).Length}";
            default:
                float saving = 1f - (1f + car.efficiencyPerLevel * level) / (1f + car.efficiencyPerLevel * (level + 1));
                return $"расход -{Mathf.RoundToInt(saving * 100f)} %";
        }
    }
}
