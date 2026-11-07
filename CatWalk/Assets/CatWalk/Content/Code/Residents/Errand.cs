using UnityEngine;

// Личное поручение жителя: особая посылка (свой DeliveryOrder, выдаёт почта жителя), награда — в заказе.
// Житель предлагает его сам, когда выполнены requires. Цепочки — через условия на errand.<id> (2 — выполнено).
// Сомнительное и незаконное платят больше, но дают дурную славу (errand.heat) при согласии;
// незаконную посылку могут изъять на досмотре (Errands). Тексты временные.
[CreateAssetMenu(menuName = "CatWalk/Errand")]
public class Errand : ScriptableObject
{
    public enum Legality { Ordinary, Shady, Illegal }

    public string id;                     // постоянный: факт errand.<id>
    public Legality legality;
    public DeliveryOrder order;           // его условия — «взято и не выполнено», их ставит builder
    public FactCondition[] requires;      // когда житель предлагает
    [TextArea] public string offer;       // «Есть дело...»
    [TextArea] public string accepted;
    [TextArea] public string refused;
    [TextArea] public string done;        // сообщение, когда посылку сдали
    public int heat;                      // дурной славы за согласие
    public float pressure;                // сдвиг давления кредиторов, когда сдали

    public string Fact => "errand." + id;
    public int State => StoryFacts.Get(Fact); // 0 — не предлагали, 1 — взято, 2 — выполнено, -1 — отказ, -2 — изъято

    public string LegalityLabel => legality == Legality.Ordinary ? "обычное" : legality == Legality.Shady ? "сомнительное" : "незаконное";
}
