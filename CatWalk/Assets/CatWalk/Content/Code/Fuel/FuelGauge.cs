using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Полоска топлива внизу экрана: уровень бака, на резерве — красная.
public class FuelGauge : MonoBehaviour
{
    public FuelTank tank;
    public Image fill;                    // растягивается по ширине полоски якорями
    public TMP_Text label;
    public Color normal = new(1f, 0.75f, 0.3f);
    public Color reserve = new(1f, 0.3f, 0.25f);

    int shown = -1;

    void Update()
    {
        fill.rectTransform.anchorMax = new Vector2(tank.Fuel / tank.capacity, 1f);
        fill.color = tank.OnReserve ? reserve : normal;

        int liters = Mathf.CeilToInt(tank.Fuel);
        if (liters == shown) return;
        shown = liters;
        label.text = liters > 0 ? $"Топливо {liters} / {tank.capacity:0} л" : "Топливо — бак пуст";
    }
}
