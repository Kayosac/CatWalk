using System;
using UnityEngine;

// Кто живёт у почты и что говорит: приветствия, темы для разговора, благодарности за доставку
// и короткая история — события, которые открываются по мере доставок сюда.
// Состояния (moods) меняют приветствие и темы по фактам — так поручения и слава меняют жизнь жителя.
// Поручения (errands) житель предлагает сам. Реплики временные — тёплые, короткие, с лёгкой абсурдинкой.
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

    // Событие истории: открывается после minDeliveries доставок сюда и, если задано, после события after.
    [Serializable]
    public class StoryEvent
    {
        public string id;                 // постоянный — по нему житель помнит, что уже рассказал
        [TextArea] public string line;
        public int minDeliveries = 1;
        public string after;              // id события, которое должно быть уже рассказано; пусто — без условия
    }

    // Состояние жителя: пока условия выполнены, свои приветствие и темы вместо обычных (пустое — обычное).
    [Serializable]
    public class Mood
    {
        public FactCondition[] when;
        [TextArea] public string greeting;
        [TextArea] public string[] topics;
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
    public StoryEvent[] story;
    public Mood[] moods;                      // первое подходящее — главное
    public Errand[] errands;                  // по порядку: предлагается первое доступное
}
