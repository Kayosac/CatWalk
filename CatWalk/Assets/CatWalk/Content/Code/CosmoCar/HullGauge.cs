using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Полоска корпуса внизу экрана, рядом с топливом: исправность, на низкой — красная.
public class HullGauge : MonoBehaviour
{
    public Hull hull;
    public Image fill;                    // растягивается по ширине полоски якорями
    public TMP_Text label;
    public float lowAt = 30f;             // ниже — красная, %
    public Color normal = new(0.55f, 0.85f, 1f);
    public Color low = new(1f, 0.3f, 0.25f);

    int shown = -1;

    void Update()
    {
        fill.rectTransform.anchorMax = new Vector2(hull.Condition / 100f, 1f);
        fill.color = hull.Condition < lowAt ? low : normal;

        int percent = Mathf.CeilToInt(hull.Condition);
        if (percent == shown) return;
        shown = percent;
        label.text = $"Корпус {percent} %";
    }
}
