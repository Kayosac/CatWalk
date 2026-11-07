using Febucci.TextAnimatorForUnity;
using UnityEngine;
using UnityEngine.InputSystem;

// Маяк со старой записью. E у пульта — следующая открытая непрослушанная часть, а если новых нет —
// прослушанные по кругу. Текст печатается над пультом (Text Animator), висит lineTime и не мешает ходить.
// Части открываются условиями (LighthouseRecord: посещения маяка, доставки, другие места).
// Голова фонаря на вершине вращается.
//
// Факты:
//   place.record              — частей записи прослушано
//   place.record.<часть>      — часть прослушана (id из LighthouseRecord)
public class Lighthouse : MonoBehaviour
{
    public LighthouseRecord record;
    public Transform usePoint;            // где встаёт кот
    public float useDistance = 2f;        // м

    [Header("Экран над пультом")]
    public Canvas screen;                 // world-space, всегда смотрит в камеру
    public TypewriterComponent typewriter;
    public float lineTime = 8f;           // сколько висит часть, с; длинная — пока печатается и ещё 3 с

    [Header("Фонарь")]
    public Transform lamp;
    public float lampSpeed = 36f;         // °/с

    int replay;                           // какую прослушанную часть повторить
    float hideAt;

    void Awake() => screen.enabled = false;

    bool CatNear => ParcelCarrier.Current != null && ParcelCarrier.Current.isActiveAndEnabled
                 && Vector3.Distance(ParcelCarrier.Current.transform.position, usePoint.position) < useDistance;

    static string Fact(LighthouseRecord.Part part) => "place.record." + part.id;

    LighthouseRecord.Part Next() => System.Array.Find(record.parts, p => !StoryFacts.Has(Fact(p)) && FactCondition.All(p.conditions));

    void Update()
    {
        if (lamp != null) lamp.Rotate(0f, lampSpeed * Time.deltaTime, 0f, Space.Self);
        bool menu = MenuScreen.Opened != null;
        screen.enabled = !menu && Time.time < hideAt;
        if (menu || !CatNear) return;

        Hud.Prompt(Next() != null ? "E — прослушать запись | новая часть" : "E — прослушать запись", 10);
        Keyboard k = Keyboard.current;
        if (k != null && k.eKey.wasPressedThisFrame) Play();
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (screen.enabled && cam != null) screen.transform.rotation = cam.transform.rotation;
    }

    void Play()
    {
        LighthouseRecord.Part part = Next();
        if (part != null)
        {
            StoryFacts.Set(Fact(part), 1);
            StoryFacts.Add("place.record");
        }
        else
        {
            // Повтор прослушанного по кругу.
            var heard = System.Array.FindAll(record.parts, p => StoryFacts.Has(Fact(p)));
            if (heard.Length > 0) part = heard[replay++ % heard.Length];
        }

        int index = part != null ? System.Array.IndexOf(record.parts, part) + 1 : 0;
        string text = part != null ? $"<b>{record.title} | часть {index} из {record.parts.Length}</b>\n{part.text}" : $"<b>{record.title}</b>\n{record.locked}";
        screen.enabled = true;
        typewriter.ShowText(text);
        hideAt = Time.time + Mathf.Max(lineTime, typewriter.GetApproximateShowDuration() + 3f);
    }

    void OnDrawGizmos()
    {
        if (usePoint == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(usePoint.position, useDistance);
    }
}
