using UnityEngine;

// Косвенная улика линии трикстера: короткий текст, который игрок может найти, а может и пропустить.
// Ничего не меняет, кроме факта trickster.clue.<id>. Срабатывает один раз, когда выполнены условия и кот пешком
// (или машина, если byCar) ближе radius: с needKey — по F («прочитать», только пешком), иначе сам через delay секунд.
public class TricksterClue : MonoBehaviour
{
    public string id;
    public FactCondition[] conditions;
    public float radius = 3f;             // м
    public bool byCar;                    // расстояние от машины, иначе от кота пешком
    public bool needKey;
    public float delay = 4f;              // с, без needKey
    [TextArea] public string text;

    CosmoCarController car;
    float wait;

    string Fact => "trickster.clue." + id;

    void Start() => car = FindFirstObjectByType<CosmoCarController>();

    void Update()
    {
        ParcelCarrier cat = ParcelCarrier.Current;
        Transform who = byCar ? (car != null ? car.transform : null) : cat != null ? cat.transform : null;
        if (StoryFacts.Has(Fact) || who == null || MenuScreen.Opened != null || !FactCondition.All(conditions)
            || Vector3.Distance(who.position, transform.position) > radius)
        {
            wait = 0f;
            return;
        }

        if (!needKey)
        {
            if ((wait += Time.deltaTime) >= delay) Reveal();
            return;
        }
        if (cat == null || cat.HasAction) return;
        Hud.Prompt(Controls.Hint(Controls.Use, "прочитать"), 9);
        if (Controls.Pressed(Controls.Use)) Reveal();
    }

    void Reveal()
    {
        StoryFacts.Set(Fact, 1);
        Hud.Toast(text);
    }
}
