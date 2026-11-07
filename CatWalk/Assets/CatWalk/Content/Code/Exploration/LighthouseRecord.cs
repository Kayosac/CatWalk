using System;
using UnityEngine;

// Старая запись, которую передаёт маяк (Lighthouse): части по порядку, каждая открывается своими условиями.
// Тексты временные — осколки былой жизни, в тоне нарративной доски.
[CreateAssetMenu(menuName = "CatWalk/Lighthouse Record")]
public class LighthouseRecord : ScriptableObject
{
    [Serializable]
    public class Part
    {
        public string id;                 // постоянный — по нему помнится, что прослушано (факт place.record.<id>)
        [TextArea(3, 8)] public string text;
        public FactCondition[] conditions; // все должны выполниться; пусто — открыта сразу
    }

    public string title = "Запись";
    public Part[] parts;
    [TextArea] public string locked;      // когда новых частей нет и ничего не прослушано
}
