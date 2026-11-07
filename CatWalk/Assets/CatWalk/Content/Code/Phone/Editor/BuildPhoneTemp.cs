using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Временный сборщик телефонной будки (удалить после проверки). Повторный запуск обновляет, а не дублирует:
// префаб PhoneBooth = модель + PhoneBooth + меню + точка, где встаёт кот, + тёплая лампа сверху. Сцены не трогает.
static class BuildPhoneTemp
{
    const string ModelPath = "Assets/CatWalk/Content/Environment/Modules/Prototype/PhoneBooth.prefab";
    const string MenuWindowPath = "Assets/CatWalk/Prefabs/UI/MenuWindow.prefab";
    const string BoothPath = "Assets/CatWalk/Prefabs/PhoneBooth.prefab";

    [MenuItem("CatWalk/Temp/Build Phone")]
    static void Build()
    {
        // Всё нужное грузим заранее: если чего-то нет, префаб не тронут.
        GameObject model = Load<GameObject>(ModelPath);
        GameObject window = Load<GameObject>(MenuWindowPath);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BoothPath) == null)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            var empty = new GameObject("PhoneBooth");
            SceneManager.MoveGameObjectToScene(empty, preview);
            PrefabUtility.SaveAsPrefabAsset(empty, BoothPath);
            EditorSceneManager.ClosePreviewScene(preview);
        }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(BoothPath))
        {
            GameObject root = scope.prefabContentsRoot;

            if (root.transform.Find("Model") == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "Model";
            }

            // Дверь и трубка смотрят в −Z: кот встаёт перед будкой.
            Transform usePoint = Child(root.transform, "Use point");
            usePoint.SetLocalPositionAndRotation(new Vector3(0f, 0f, -0.9f), Quaternion.identity);

            // Тёплая слабая лампа над будкой — чтобы её было видно издалека.
            Transform lamp = Child(root.transform, "Lamp");
            lamp.SetLocalPositionAndRotation(new Vector3(0f, 2.5f, 0f), Quaternion.identity);
            Light light = Ensure<Light>(lamp.gameObject);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.45f);
            light.intensity = 1.5f;
            light.range = 6f;
            light.shadows = LightShadows.None;

            Transform menuObject = root.transform.Find("Menu");
            if (menuObject == null)
            {
                menuObject = ((GameObject)PrefabUtility.InstantiatePrefab(window, root.transform)).transform;
                menuObject.name = "Menu";
            }
            PhoneBoothMenu menu = Ensure<PhoneBoothMenu>(menuObject.gameObject);
            menu.title = menuObject.Find("Panel/Title").GetComponent<TMP_Text>();
            menu.status = menuObject.Find("Panel/Status").GetComponent<TMP_Text>();
            menu.list = menuObject.Find("Panel/List");
            menu.rowTemplate = menuObject.Find("Panel/Row").GetComponent<Button>();

            TMP_Text hint = menuObject.Find("Panel/Hint").GetComponent<TMP_Text>();
            hint.text = "E или Esc — повесить трубку";
            PrefabUtility.RecordPrefabInstancePropertyModifications(hint);

            PhoneBooth booth = Ensure<PhoneBooth>(root);
            booth.usePoint = usePoint;
            booth.menu = menu;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Build Phone: готово.");
    }

    static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new System.Exception("Build Phone: нет ассета " + path);
        return asset;
    }

    static T Ensure<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    static Transform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }
}
