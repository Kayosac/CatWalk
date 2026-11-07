using System.Collections.Generic;
using UnityEngine;

// Личные поручения жителей, на Game: итог доставки поручения и досмотры.
// Сданная посылка поручения — поручение выполнено: сообщение от жителя, сдвиг давления кредиторов.
// Дурная слава зовёт досмотры: при прибытии на станцию или заправку с шансом heat * chancePerHeat
// незаконные посылки рядом изымаются со штрафом и ростом давления, поручение провалено, слава вдвое меньше.
// Чисто — слава остывает на 1. Планеты и пустота не досматриваются: дальние маршруты слабо контролируются.
//
// Факты:
//   errand.<id>                       — 1 взято, 2 выполнено, -1 отказался, -2 изъято на досмотре
//   errand.done                       — выполнено поручений всего
//   errand.heat                       — дурная слава
//   errand.inspections, errand.seized — досмотров и изъятых посылок всего
//   errand.wanted                     — ставит веха «дурная слава»
public class Errands : MonoBehaviour
{
    [Range(0f, 1f)] public float chancePerHeat = 0.12f;
    [Range(0f, 1f)] public float maxChance = 0.6f;
    public int fine = 150;                // за каждую изъятую посылку, кр; сколько есть на счету
    public float seizurePressure = 10f;   // кредиторам не нравится внимание инспекции

    void OnEnable()
    {
        PostTerminal.Delivered += OnDelivered;
        StoryFacts.Changed += OnFact;
    }

    void OnDisable()
    {
        PostTerminal.Delivered -= OnDelivered;
        StoryFacts.Changed -= OnFact;
    }

    // Поручение, чья это посылка; null — обычный заказ.
    public static Errand Of(DeliveryOrder order)
    {
        if (order == null) return null;
        foreach (Resident r in Resident.All)
            if (r.profile != null && r.profile.errands != null)
                foreach (Errand e in r.profile.errands)
                    if (e != null && e.order == order) return e;
        return null;
    }

    void OnDelivered(PostTerminal post, List<Parcel> parcels)
    {
        foreach (Parcel p in parcels)
        {
            Errand e = Of(p.order);
            if (e == null || e.State != 1) continue;
            StoryFacts.Set(e.Fact, 2);
            StoryFacts.Add("errand.done");
            if (e.pressure != 0f && Debt.Current != null) Debt.Current.AddPressure(e.pressure);
            if (!string.IsNullOrEmpty(e.done)) Hud.Toast(e.done);
        }
    }

    void OnFact(string id)
    {
        if (id == null || !id.StartsWith("visits.")) return;
        Location place = Location.Find(id.Substring("visits.".Length));
        if (place == null || (place.kind != Location.Kind.Station && place.kind != Location.Kind.FuelStop)) return;
        int heat = StoryFacts.Get("errand.heat");
        if (heat > 0 && Random.value < Mathf.Min(maxChance, heat * chancePerHeat)) Inspect(place, heat);
    }

    void Inspect(Location place, int heat)
    {
        StoryFacts.Add("errand.inspections");
        var seized = new List<Parcel>();
        foreach (Parcel p in Parcel.All)
        {
            Errand e = Of(p.order);
            if (e != null && e.legality == Errand.Legality.Illegal
                && (p.transform.position - place.transform.position).sqrMagnitude < place.radius * place.radius * 4f) seized.Add(p);
        }
        if (seized.Count == 0)
        {
            StoryFacts.Set("errand.heat", heat - 1);
            Hud.Toast($"Досмотр на «{place.Label}»: всё чисто. Инспектор разочарован, слава остывает.");
            return;
        }

        string cargo = "";
        foreach (Parcel p in seized)
        {
            StoryFacts.Set(Of(p.order).Fact, -2);
            StoryFacts.Add("errand.seized");
            cargo += (cargo.Length > 0 ? ", " : "") + $"«{p.order.cargo}»";
            if (p.Slot != null) p.Slot.Parcel = null;
            p.gameObject.SetActive(false);
            Destroy(p.gameObject);
        }
        int penalty = Mathf.Min(fine * seized.Count, Wallet.Current.Balance);
        Wallet.Current.TrySpend(penalty);
        if (Debt.Current != null) Debt.Current.AddPressure(seizurePressure);
        StoryFacts.Set("errand.heat", heat / 2);
        Hud.Toast($"Досмотр на «{place.Label}»: изъято {cargo}, штраф {Wallet.Format(penalty)}. Кредиторам не нравится внимание инспекции.");
    }
}
