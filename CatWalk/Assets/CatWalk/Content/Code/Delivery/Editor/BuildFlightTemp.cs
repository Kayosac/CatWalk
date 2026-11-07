using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ВРЕМЕННЫЙ сборщик: префаб журнала заказов; на космокаре — снять остатки маршевого перелёта (CruiseDrive)
// и отдать эффект маршевого контроллеру. Повторный запуск пересобирает то же самое. После проверки — удалить.
static class BuildFlightTemp
{
    const string Prefabs = "Assets/CatWalk/Prefabs/";
    const string Ui = Prefabs + "UI/";

    [MenuItem("CatWalk/Temp/Build Flight")]
    static void Build()
    {
        BuildJournal();
        SetupCar();
        AssetDatabase.SaveAssets();
        Debug.Log("Build Flight: готово");
    }

    // Журнал: вложенный MenuWindow с компонентом OrderJournal.
    static void BuildJournal()
    {
        var window = AssetDatabase.LoadAssetAtPath<GameObject>(Ui + "MenuWindow.prefab");
        if (window == null)
        {
            Debug.LogError("Build Flight: нет " + Ui + "MenuWindow.prefab");
            return;
        }
        var root = (GameObject)PrefabUtility.InstantiatePrefab(window);
        root.name = "OrderJournal";
        OrderJournal menu = root.AddComponent<OrderJournal>();
        menu.title = root.transform.Find("Panel/Title").GetComponent<TMP_Text>();
        menu.status = root.transform.Find("Panel/Status").GetComponent<TMP_Text>();
        menu.list = root.transform.Find("Panel/List");
        menu.rowTemplate = root.transform.Find("Panel/Row").GetComponent<Button>();
        menu.pauseTime = true;
        menu.closeOnE = false;
        Transform hint = root.transform.Find("Panel/Hint");
        if (hint != null) hint.GetComponent<TMP_Text>().text = "Клик — цель на адресата | J или Esc — закрыть";
        root.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, Ui + "OrderJournal.prefab");
        Object.DestroyImmediate(root);
    }

    // CruiseDrive удалён — на машине остался Missing Script; эффект прибытия больше не нужен,
    // эффект разгона горит, пока включён маршевый.
    static void SetupCar()
    {
        using var scope = new PrefabUtility.EditPrefabContentsScope(Prefabs + "CosmoCar.prefab");
        GameObject car = scope.prefabContentsRoot;
        int missing = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(car);

        Transform arrival = car.transform.Find("Cruise arrival VFX");
        bool removed = arrival != null;
        if (removed) Object.DestroyImmediate(arrival.gameObject);

        CosmoCarController controller = car.GetComponent<CosmoCarController>();
        Transform vfx = car.transform.Find("Cruise spin-up VFX");
        if (vfx == null) vfx = car.transform.Find("Cruise VFX");
        if (vfx == null) Debug.LogError("Build Flight: на CosmoCar нет «Cruise spin-up VFX»");
        else
        {
            vfx.name = "Cruise VFX";
            foreach (ParticleSystem system in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
                main.loop = true;
            }
            controller.cruiseVfx = vfx.GetComponentInChildren<ParticleSystem>(true);
        }
        Debug.Log($"Build Flight: CosmoCar — снято пустых скриптов {missing}, эффект прибытия {(removed ? "удалён" : "уже удалён")}, cruiseVfx {(controller.cruiseVfx != null ? controller.cruiseVfx.name : "нет")}");
    }
}
