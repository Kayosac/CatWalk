using UnityEngine;

// Декорация по фактам: пока условия выполнены, show включены, а hide выключены; иначе наоборот.
// Сам компонент — на всегда активном объекте (не в своих списках). Огород Мэй, новый насос Иры и т. п.
public class FactToggle : MonoBehaviour
{
    public FactCondition[] when;
    public GameObject[] show;
    public GameObject[] hide;

    void OnEnable()
    {
        StoryFacts.Changed += OnChanged;
        Apply();
    }

    void OnDisable() => StoryFacts.Changed -= OnChanged;

    void OnChanged(string id) => Apply();

    void Apply()
    {
        bool on = FactCondition.All(when);
        foreach (GameObject go in show) if (go != null) go.SetActive(on);
        foreach (GameObject go in hide) if (go != null) go.SetActive(!on);
    }
}
