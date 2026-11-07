using System;
using UnityEngine;

// Шаг обучения: короткая подсказка, которая появляется по ситуации и пропадает, когда игрок сделал, что в ней сказано.
// Условия — факты истории (FactCondition) и простые проверки состояния игры (State). Шаги показывает Tutorial.
[CreateAssetMenu(menuName = "CatWalk/Tutorial Step")]
public class TutorialStep : ScriptableObject
{
    public enum State
    {
        Driving,        // кот за рулём
        Walking,        // кот пешком
        Docked,         // машина стоит на парковке
        Magnet,         // машину тянет парковка (или она уже стоит)
        NearPost,       // кот у почтового терминала
        ParcelWaiting,  // посылка ждёт в зоне выдачи
        Holding,        // посылка в руках
        Loaded,         // посылка на полке космокара
        InReceiveZone,  // посылка стоит в зоне приёма своей почты
        Cruising,       // маршевый включён
        HasTarget,      // у навигатора есть цель
        LowFuel,        // топлива меньше половины
        CanUpgrade,     // хватает денег на какое-нибудь улучшение
    }

    // Куда смотрит стрелка на плашке, пока кот пешком.
    public enum Target { None, Post, Parcel, Car, Address, Booth, Pump, Workshop }

    [Serializable]
    public class Condition
    {
        public FactCondition[] facts;
        public State[] all;               // все верны
        public State[] none;              // ни одно не верно
        public string menu;               // открыто это меню (имя класса: MapMenu, PostTerminalMenu...); пусто — не важно

        public bool IsEmpty => (facts == null || facts.Length == 0) && (all == null || all.Length == 0)
                            && (none == null || none.Length == 0) && string.IsNullOrEmpty(menu);
    }

    public string id;                     // постоянный: факт tutorial.<id>
    [TextArea] public string text;        // временный текст
    public Condition show;
    public Condition done;                // пусто — шаг проходит только по timeout
    public float timeout;                 // с на экране, после которых шаг пройден и так; 0 — только по done
    public Target target;
    public string visibleIn;              // меню, поверх которого плашка не прячется (имя класса)

    public string Fact => "tutorial." + id;
}
