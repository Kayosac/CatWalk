using System;
using System.Collections.Generic;
using UnityEngine;

// Линия домой, одна на сцену (в Game): какой звонок ждёт, что услышано и что раскрыто о прошлом героя («Записи»).
// Сюжетный звонок — самый приоритетный из доступных и не услышанных; между сюжетными — не меньше catalog.storyGap
// минут игры. Сюжетного нет — следующий бытовой, по кругу. Звонок засчитывается, только если договорили до конца.
// Когда сюжетный звонок стал доступен, один раз показывается его reminder.
//
// Факты:
//   call.<id>  — сколько раз дослушан звонок (сюжетный — 1)
//   calls      — дослушанных звонков всего
public class PhoneLine : MonoBehaviour, ISaveable
{
    public static PhoneLine Current { get; private set; }

    public PhoneCallCatalog catalog;
    public PhoneCallScreen screen;

    [Serializable]
    class State
    {
        public List<string> heard = new();    // дослушанные сюжетные звонки, по порядку
        public List<string> reminded = new(); // о каких уже напомнили
        public int chat;                      // номер следующего бытового разговора
        public int lastStory = -1000;         // минута игры последнего сюжетного звонка
    }

    State s = new();
    float poll;

    void Awake() => Current = this;

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    bool Heard(PhoneCall call) => s.heard.Contains(call.id);
    bool Open(PhoneCall call) => call != null && FactCondition.All(call.conditions) && (call.after == null || Heard(call.after));

    // Сюжетный звонок, который ждёт; null — не о чем.
    public PhoneCall News
    {
        get
        {
            if (StoryFacts.Get("minutes") - s.lastStory < catalog.storyGap) return null;
            PhoneCall best = null;
            foreach (PhoneCall call in catalog.calls)
                if (!call.repeatable && Open(call) && !Heard(call) && (best == null || call.priority > best.priority)) best = call;
            return best;
        }
    }

    // «Позвонить домой»: открывает разговор или возвращает текст статуса, если никто не берёт.
    public string Call(PhoneBooth booth)
    {
        PhoneCall call = News;
        if (call == null)
        {
            PhoneCall[] chats = Array.FindAll(catalog.calls, c => c.repeatable && Open(c));
            if (chats.Length > 0) call = chats[s.chat % chats.Length];
        }
        if (call == null) return "Гудки... никто не берёт.";
        MenuScreen.Opened?.Close();
        screen.Open(call, booth);
        return null;
    }

    // Договорили до конца. Возвращает текст статуса для меню будки.
    public string Done(PhoneCall call)
    {
        if (call.repeatable) s.chat++;
        else
        {
            if (!Heard(call)) s.heard.Add(call.id);
            s.lastStory = StoryFacts.Get("minutes");
        }
        StoryFacts.Add("call." + call.id);
        StoryFacts.Add("calls");
        return call.reveals != null && call.reveals.Length > 0 && !call.repeatable ? "Разговор окончен. Новое в «Записях»." : "Разговор окончен.";
    }

    // Раскрытое — строки услышанных сюжетных звонков, в порядке разговоров.
    public List<string> Records()
    {
        var lines = new List<string>();
        foreach (string id in s.heard)
        {
            PhoneCall call = Array.Find(catalog.calls, c => c.id == id);
            if (call != null && call.reveals != null) lines.AddRange(call.reveals);
        }
        return lines;
    }

    void Update()
    {
        if ((poll -= Time.unscaledDeltaTime) > 0f || MenuScreen.Opened != null) return;
        poll = 1f;
        PhoneCall news = News;
        if (news == null || string.IsNullOrEmpty(news.reminder) || s.reminded.Contains(news.id)) return;
        s.reminded.Add(news.id);
        Hud.Toast(news.reminder);
    }

    public string SaveKey => "phone";
    public string Save() => JsonUtility.ToJson(s);
    public void Load(string data) => s = JsonUtility.FromJson<State>(data) ?? new State();
}
