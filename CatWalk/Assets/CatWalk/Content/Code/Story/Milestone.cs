using System;
using UnityEngine;

// Сюжетная веха песочницы: когда выполнены условия — один раз сообщение, свои факты, сдвиг давления кредиторов
// и новые заказы на почтах. Сработавшая веха помнится фактом milestone.<id> и сохраняется вместе с фактами.
// Заказ вехи видят на почте по его условиям (DeliveryOrder.requires) — обычно milestone.<id>.
[CreateAssetMenu(menuName = "CatWalk/Milestone")]
public class Milestone : ScriptableObject
{
    [Serializable]
    public class FactValue
    {
        public string fact;
        public int value = 1;
    }

    // Заказ в список почты с этим адресом (PostTerminal.address).
    [Serializable]
    public class PostOrder
    {
        public string post;
        public DeliveryOrder order;
    }

    public string id;                     // постоянный: факт milestone.<id>
    public FactCondition[] conditions;
    [TextArea] public string message;     // сообщение сверху; пусто — веха тихая
    public FactValue[] facts;             // что веха ставит
    public float pressure;                // сдвиг давления кредиторов; минус — снимает
    public PostOrder[] orders;
}
