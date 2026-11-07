using System;
using UnityEngine;

// Мета-финал «Обрыв контакта» (доска, раздел 06): когда появляется сбой, что в нём идёт, пасхалка в главном меню.
// ЗАГЛУШКА: условия появления на доске не решены. По умолчанию строгие: все сюжетные звонки из calls услышаны
// (пока там «call.TODO» — финал не случится, пока не впишут настоящие id) и minutes >= 120.
// Реплики и сцены — плейсхолдеры, их пишет автор.
[CreateAssetMenu(menuName = "CatWalk/Meta Ending Settings")]
public class MetaEndingSettings : ScriptableObject
{
    [Serializable]
    public class Line
    {
        [TextArea] public string text;
        public float seconds = 5f;        // сколько висит на экране, с
    }

    [Header("Когда появляется сбой (заглушка)")]
    public string[] calls = { "call.TODO" }; // факты сюжетных звонков, все должны быть > 0
    public FactCondition[] conditions = { new() { fact = "minutes", value = 120 } };
    public float radius = 80f;            // кот или машина ближе — финал начинается, м

    [Header("Распад мира")]
    public float collapseSeconds = 12f;
    public float glitchShift = 2f;        // на сколько дёргаются мигающие объекты, м

    [Header("Сцена (тексты — автора)")]
    public Line[] lines =
    {
        new() { text = "[реплика автора: герой видит, как мир распадается]", seconds = 5f },
        new() { text = "[реплика автора: герой понимает, что мир ненастоящий]", seconds = 6f },
        new() { text = "[реплика автора: обращение к игроку]", seconds = 7f },
        new() { text = "[сцена автора]", seconds = 5f },
    };
    public float freezeSeconds = 3f;      // замерший кадр перед закрытием игры, с

    [Header("Пасхалка в главном меню")]
    public string easterEggMark = "_";
    [TextArea] public string easterEggText = "[текст автора: пасхалка после обрыва контакта]";

    public bool Ready
    {
        get
        {
            foreach (string call in calls)
                if (!string.IsNullOrEmpty(call) && !StoryFacts.Has(call)) return false;
            return FactCondition.All(conditions);
        }
    }
}
