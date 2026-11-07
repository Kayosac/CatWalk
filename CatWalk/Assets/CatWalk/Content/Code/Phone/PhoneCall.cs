using System;
using UnityEngine;

// Звонок домой: когда он открывается, кто что говорит и что игрок узнаёт о прошлом героя.
// Сюжетный звонок слышен один раз; повторяемый — «бытовой» разговор, идёт по кругу, когда сюжетных нет.
[CreateAssetMenu(menuName = "CatWalk/Phone Call")]
public class PhoneCall : ScriptableObject
{
    public enum Speaker { Hero, Mom, Dad, Machine } // имена — в PhoneCallCatalog.names

    [Serializable]
    public class Line
    {
        public Speaker who;
        [TextArea] public string text;
    }

    public string id;                     // постоянный — по нему помнится, что звонок услышан (факт call.<id>)
    public FactCondition[] conditions;    // все должны выполняться
    public PhoneCall after;               // открывается только после этого звонка; пусто — без условия
    public int priority;                  // из доступных сюжетных звучит самый приоритетный
    public bool repeatable;               // бытовой разговор
    public string reminder;               // Hud.Toast один раз, когда звонок стал доступен; пусто — молча
    public Line[] lines;
    [TextArea] public string[] reveals;   // строки «Записей» — что стало известно
}
