using System;
using UnityEngine;

// Кто живёт у почты и что говорит: приветствия, темы для разговора, благодарности за доставку.
// Реплики временные — тёплые, короткие, с лёгкой абсурдинкой.
[CreateAssetMenu(menuName = "CatWalk/Resident Profile")]
public class ResidentProfile : ScriptableObject
{
    // Отдельная реплика на конкретный груз.
    [Serializable]
    public class CargoReaction
    {
        public DeliveryOrder order;
        [TextArea] public string line;
    }

    public string displayName = "Житель";
    [TextArea] public string greeting;        // при первой встрече
    [TextArea] public string greetingKnown;   // знакомому — после разговора или доставки
    [TextArea] public string greetingWarm;    // после warmDeliveries доставок сюда
    public int warmDeliveries = 3;
    [TextArea] public string[] topics;        // F — по очереди, по кругу
    [TextArea] public string thanks;          // за доставку, если на груз нет своей реакции
    public CargoReaction[] reactions;
    [TextArea] public string damagedRemark;   // о помятой хрупкой коробке
}
