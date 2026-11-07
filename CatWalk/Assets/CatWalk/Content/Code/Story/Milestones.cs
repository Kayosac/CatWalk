using UnityEngine;

// Проверяльщик вех, на Game. Раз в полсекунды срабатывает первая несработавшая веха с выполненными условиями.
// За раз — одна веха; после сообщения следующая ждёт gap, чтобы сообщения не перебивали друг друга.
// Первая проверка — через firstCheck: сохранение уже загружено и факты сняты с мира.
// Заказы вех кладутся в списки почт на старте (видны по своим условиям) — так сохранение находит их посылки.
//
// Факты: milestone.<id> — веха сработала (1).
public class Milestones : MonoBehaviour
{
    public Milestone[] milestones;
    public float firstCheck = 2f;         // с после старта
    public float gap = 4f;                // с после вехи с сообщением

    float wait;

    void Start()
    {
        wait = firstCheck;
        foreach (Milestone m in milestones)
            if (m != null && m.orders != null)
                foreach (Milestone.PostOrder o in m.orders)
                    foreach (PostTerminal t in PostTerminal.All)
                        if (t.address == o.post) t.AddOrder(o.order);
    }

    void Update()
    {
        if ((wait -= Time.unscaledDeltaTime) > 0f) return;
        wait = 0.5f;

        Milestone m = System.Array.Find(milestones, x => x != null && !StoryFacts.Has("milestone." + x.id) && FactCondition.All(x.conditions));
        if (m == null) return;
        StoryFacts.Set("milestone." + m.id, 1);
        if (m.facts != null)
            foreach (Milestone.FactValue f in m.facts) StoryFacts.Set(f.fact, f.value);
        if (m.pressure != 0f && Debt.Current != null) Debt.Current.AddPressure(m.pressure);
        if (!string.IsNullOrEmpty(m.message))
        {
            Hud.Toast(m.message);
            wait = gap;
        }
    }
}
