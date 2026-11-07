using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ВРЕМЕННЫЙ сборщик сведения баланса. Повторный запуск ставит те же числа. После проверки — удалить.
// Build Balance: поля ассетов — долг (Data/Debt/Debt.asset), награды заказов (Data/Delivery), веха «половина долга»,
//                бак и цены улучшений в CosmoCar.prefab, стартовый баланс в Wallet.prefab. Лог — Temp/balance_build.txt.
// Place Balance: стартовый баланс у кошелька в открытой сцене (в Prototype он переопределён на экземпляре Game). Лог — Temp/balance_place.txt.
static class BuildBalanceTemp
{
    const string Data = "Assets/CatWalk/Data/";
    const string Prefabs = "Assets/CatWalk/Prefabs/";
    const int StartBalance = 400;

    // Награда = 120 + 45 кр/км + 60 за хрупкое, до десятков; заказы вех — ещё +80. Потерянная почта и поручения — как были.
    static readonly Dictionary<string, int> Rewards = new()
    {
        { "KonbiniToFuelStop_Letters", 200 }, { "KonbiniToFuelStop_Noodles", 200 }, { "KonbiniToFuelStop_Jars", 260 },
        { "KonbiniToMira_Seedlings", 320 }, { "KonbiniToResearch_Coffee", 270 }, { "KonbiniToOchre_Panamas", 380 },
        { "KonbiniToGalka_Tea", 290 }, { "FuelStopToKonbini_Seals", 260 }, { "FuelStopToMira_Pump", 310 },
        { "FuelStopToResearch_Antenna", 370 }, { "FuelStopToIce_Antifreeze", 350 }, { "ResearchToKonbini_Thermos", 270 },
        { "ResearchToFuelStop_Filters", 310 }, { "ResearchToIce_Probes", 330 }, { "ResearchToGalka_Bulbs", 500 },
        { "MiraToKonbini_Jam", 320 }, { "MiraToResearch_Soil", 400 }, { "MiraToOchre_Herbs", 430 },
        { "OchreToResearch_Ore", 480 }, { "OchreToFuelStop_Lamps", 360 }, { "OchreToKonbini_Sand", 380 },
        { "IceToMira_Snow", 400 }, { "IceToResearch_Logs", 270 }, { "IceToKonbini_Mittens", 280 },
        { "GalkaToMira_Socks", 260 }, { "Milestone_KonbiniToIce_Heater", 420 }, { "Milestone_ResearchToOchre_Drill", 560 },
    };

    static readonly List<string> log = new();

    static void Log(string line)
    {
        log.Add(line);
        Debug.Log("Balance: " + line);
    }

    static void Error(string line)
    {
        log.Add("ОШИБКА: " + line);
        Debug.LogError("Balance: " + line);
    }

    static void Flush(string path)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllLines(path, log);
    }

    [MenuItem("CatWalk/Temp/Build Balance")]
    static void Build()
    {
        log.Clear();
        Debt();
        Orders();
        HalfDebt();
        Car();
        WalletPrefab();
        AssetDatabase.SaveAssets();
        Flush("Temp/balance_build.txt");
    }

    // Поле ассета или компонента по имени: «было -> стало» в лог.
    static void Set(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Error($"{target.name}: нет поля {field}");
            return;
        }
        string was = p.propertyType == SerializedPropertyType.Integer ? p.intValue.ToString() : p.floatValue.ToString("0.###");
        if (p.propertyType == SerializedPropertyType.Integer) p.intValue = Mathf.RoundToInt(value);
        else p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        Log($"{target.name}.{field}: {was} -> {value:0.###}");
    }

    static void Debt()
    {
        var debt = AssetDatabase.LoadAssetAtPath<DebtSettings>(Data + "Debt/Debt.asset");
        if (debt == null)
        {
            Error("нет Data/Debt/Debt.asset");
            return;
        }
        Set(debt, "total", 14000);
        Set(debt, "payment", 350);
        Set(debt, "paymentInterval", 600);
        Set(debt, "firstPayment", 1500);
        Set(debt, "demandEveryMin", 480);
        Set(debt, "demandEveryMax", 900);
        Set(debt, "demandChance", 0.3f);
        Set(debt, "demandMin", 120);
        Set(debt, "demandMax", 250);
        Set(debt, "demandTime", 240);
        Set(debt, "missedPayment", 20);
        Set(debt, "missedDemand", 10);
        EditorUtility.SetDirty(debt);
    }

    static void Orders()
    {
        foreach (var kv in Rewards)
        {
            var order = AssetDatabase.LoadAssetAtPath<DeliveryOrder>(Data + "Delivery/" + kv.Key + ".asset");
            if (order == null)
            {
                Error("нет заказа Data/Delivery/" + kv.Key + ".asset");
                continue;
            }
            Set(order, "reward", kv.Value);
            EditorUtility.SetDirty(order);
        }
    }

    // Веха «половина долга» — от новой суммы.
    static void HalfDebt()
    {
        var m = AssetDatabase.LoadAssetAtPath<Milestone>(Data + "Story/Milestone_half-debt.asset");
        if (m == null || m.conditions == null || m.conditions.Length == 0 || m.conditions[0].fact != "debt.paid")
        {
            Error("Milestone_half-debt: нет условия debt.paid");
            return;
        }
        Log($"Milestone_half-debt: debt.paid >= {m.conditions[0].value} -> 7000");
        m.conditions[0].value = 7000;
        EditorUtility.SetDirty(m);
    }

    static void Car()
    {
        using var scope = new PrefabUtility.EditPrefabContentsScope(Prefabs + "CosmoCar.prefab");
        GameObject car = scope.prefabContentsRoot;
        FuelTank tank = car.GetComponentInChildren<FuelTank>(true);
        CarUpgrades upgrades = car.GetComponentInChildren<CarUpgrades>(true);
        if (tank == null || upgrades == null)
        {
            Error("в CosmoCar.prefab нет FuelTank или CarUpgrades");
            return;
        }
        Set(tank, "capacity", 150);
        Set(upgrades, "baseCapacity", 150);
        Set(upgrades, "capacityPerLevel", 50);
        Set(upgrades, "engineCost", 600);
        Set(upgrades, "tankCost", 300);
        Set(upgrades, "cargoCost", 350);
        Set(upgrades, "economyCost", 450);
    }

    static void WalletPrefab()
    {
        using var scope = new PrefabUtility.EditPrefabContentsScope(Prefabs + "Wallet.prefab");
        Wallet wallet = scope.prefabContentsRoot.GetComponentInChildren<Wallet>(true);
        if (wallet == null) Error("в Wallet.prefab нет Wallet");
        else Set(wallet, "startBalance", StartBalance);
    }

    [MenuItem("CatWalk/Temp/Place Balance")]
    static void Place()
    {
        log.Clear();
        var scene = EditorSceneManager.GetActiveScene();
        Log("сцена " + scene.path);
        Wallet[] wallets = Object.FindObjectsByType<Wallet>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (wallets.Length == 0) Error("в сцене нет Wallet");
        foreach (Wallet w in wallets)
        {
            Set(w, "startBalance", StartBalance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(w);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Flush("Temp/balance_place.txt");
    }
}
