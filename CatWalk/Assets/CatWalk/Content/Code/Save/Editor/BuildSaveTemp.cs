using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ВРЕМЕННЫЙ: собирает ассет долга и префаб Game — корень систем сцены: сохранение, долг, меню паузы, экран финала,
// кошелёк и общий HUD. Идемпотентен: префаб каждый раз пересобирается заново. Удалить после запуска.
static class BuildSaveTemp
{
    const string DebtFolder = "Assets/CatWalk/Data/Debt";
    const string DebtAsset = DebtFolder + "/Debt.asset";
    const string GamePrefab = "Assets/CatWalk/Prefabs/Game.prefab";
    const string MenuWindowPrefab = "Assets/CatWalk/Prefabs/UI/MenuWindow.prefab";
    const string WalletPrefab = "Assets/CatWalk/Prefabs/Wallet.prefab";
    const string HudPrefab = "Assets/CatWalk/Prefabs/UI/Hud.prefab";

    [MenuItem("CatWalk/Temp/Build Save")]
    static void Build()
    {
        GameObject window = AssetDatabase.LoadAssetAtPath<GameObject>(MenuWindowPrefab);
        if (window == null)
        {
            Debug.LogError("BuildSaveTemp: нет " + MenuWindowPrefab);
            return;
        }

        if (!AssetDatabase.IsValidFolder(DebtFolder)) AssetDatabase.CreateFolder("Assets/CatWalk/Data", "Debt");
        DebtSettings settings = AssetDatabase.LoadAssetAtPath<DebtSettings>(DebtAsset);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<DebtSettings>();
            AssetDatabase.CreateAsset(settings, DebtAsset);
        }

        var root = new GameObject("Game");
        try
        {
            SaveGame save = root.AddComponent<SaveGame>();
            Debt debt = root.AddComponent<Debt>();
            debt.settings = settings;
            Nest(WalletPrefab, root.transform);
            Nest(HudPrefab, root.transform);
            save.pause = Menu<PauseMenu>(window, root.transform, "Pause menu", "Esc — продолжить");
            debt.ending = Menu<DebtEnding>(window, root.transform, "Debt ending", "");

            PrefabUtility.SaveAsPrefabAsset(root, GamePrefab, out bool ok);
            if (ok) Debug.Log("BuildSaveTemp: собран " + GamePrefab);
            else Debug.LogError("BuildSaveTemp: не удалось сохранить " + GamePrefab);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }

    static void Nest(string path, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("BuildSaveTemp: нет " + path + " — пропускаю");
            return;
        }
        PrefabUtility.InstantiatePrefab(prefab, parent);
    }

    // Окно меню из MenuWindow.prefab со своим компонентом; останавливает время, E его не закрывает.
    static T Menu<T>(GameObject window, Transform parent, string name, string hint) where T : MenuScreen
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(window, parent);
        go.name = name;
        T menu = go.AddComponent<T>();
        Transform panel = go.transform.Find("Panel");
        menu.title = panel.Find("Title").GetComponent<TMP_Text>();
        menu.status = panel.Find("Status").GetComponent<TMP_Text>();
        menu.list = panel.Find("List");
        menu.rowTemplate = panel.Find("Row").GetComponent<Button>();
        menu.pauseTime = true;
        menu.closeOnE = false;
        Transform hintText = panel.Find("Hint");
        if (hintText != null) hintText.GetComponent<TMP_Text>().text = hint;
        go.SetActive(false);
        return menu;
    }
}
