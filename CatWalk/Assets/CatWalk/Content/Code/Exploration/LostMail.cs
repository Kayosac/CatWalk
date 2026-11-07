using System.Collections.Generic;
using UnityEngine;

// Потерянная почта заброшенного места (сортировочный узел). Посылки лежат спрятанными (выключенная группа mail),
// пока кот или машина впервые не прибудут к месту (факт visits.<id> Location на том же объекте), — тогда появляются
// на настиле, и их можно отвезти адресатам как обычные посылки. Их заказы — особые: ни одна почта их не выдаёт,
// получатель на почте адресата говорит DeliveryOrder.reaction.
// Сохранение: появилась ли почта; сами посылки сохраняет и возвращает SaveGame.
//
// Факты:
//   place.lostmail            — потерянных посылок доставлено
public class LostMail : MonoBehaviour, ISaveable
{
    public GameObject mail;               // выключенная группа с посылками
    public DeliveryOrder[] orders;        // заказы этих посылок — по ним считаются доставки

    bool revealed;
    Location location;

    void Awake() => location = GetComponent<Location>();

    void OnEnable() => PostTerminal.Delivered += OnDelivered;
    void OnDisable() => PostTerminal.Delivered -= OnDelivered;

    void Update()
    {
        if (!revealed && location != null && StoryFacts.Has("visits." + location.id)) Reveal();
    }

    void Reveal()
    {
        revealed = true;
        if (mail == null) return;
        mail.SetActive(true);
        Hud.Toast($"{location.Label}: на настиле лежит чья-то почта");
    }

    void OnDelivered(PostTerminal post, List<Parcel> parcels)
    {
        int count = parcels.FindAll(p => System.Array.IndexOf(orders, p.order) >= 0).Count;
        if (count == 0) return;
        StoryFacts.Add("place.lostmail", count);
        Hud.Toast($"Потерянная почта: доставлено {StoryFacts.Get("place.lostmail")} из {orders.Length}");
    }

    public string SaveKey => "lostmail:" + GetComponent<Location>().id;
    public string Save() => revealed ? "1" : "0";

    // Почта уже появилась до сохранения — спрятанные посылки не нужны: живые вернёт SaveGame.
    public void Load(string data)
    {
        revealed = data == "1";
        if (revealed && mail != null) Destroy(mail);
    }
}
