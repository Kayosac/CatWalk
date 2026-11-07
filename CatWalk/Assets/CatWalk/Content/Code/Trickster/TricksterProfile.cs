using System;
using UnityEngine;

// Что говорит авантюрист и когда появляется: цепочка встреч, реплики между встречами, ожидания и передачи находки,
// и сообщения, которые приходят, пока чёрная дыра затягивает машину (BlackHole). Тексты временные: мотив авантюриста открыт.
[CreateAssetMenu(menuName = "CatWalk/Trickster Profile")]
public class TricksterProfile : ScriptableObject
{
    // Встреча: открывается по условиям, когда пройдены все предыдущие; проходит за все реплики по F.
    [Serializable]
    public class Meeting
    {
        public string at;                 // id локации станции (Location.id); точка с таким именем — в Trickster.spots
        public FactCondition[] conditions;
        [TextArea] public string[] lines;
        public int trust = 1;             // сколько доверия прибавляет
        public bool offer;                // предлагает руины: они появляются на карте, в них — находка
    }

    // Сообщение, пока тянет дыра: через at секунд после взрыва.
    [Serializable]
    public class Message
    {
        public float at;
        [TextArea] public string text;
    }

    public string displayName = "Авантюрист";
    public Meeting[] meetings;
    [TextArea] public string[] idle;       // F между встречами — по кругу
    [TextArea] public string waiting;      // предложил руины и ждёт находку
    [TextArea] public string handover;     // принял находку: обещает связаться завтра насчёт денег
    [TextArea] public string afterHandover;
    public Message[] messages;
}
