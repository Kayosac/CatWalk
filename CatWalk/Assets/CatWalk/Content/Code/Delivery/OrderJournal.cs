using System.Collections.Generic;

// ЗАГЛУШКА ветки карты: настоящий журнал заказов делает ветка flight, при слиянии берётся её версия.
// Взятые и ещё не сданные заказы: посылка, почта выдачи, почта-адресат.
public static class OrderJournal
{
    public static IEnumerable<(Parcel parcel, PostTerminal from, PostTerminal to)> Active()
    {
        foreach (Parcel parcel in Parcel.All)
        {
            if (parcel.order == null) continue;
            PostTerminal from = PostTerminal.All.Find(t => t.orders != null && System.Array.IndexOf(t.orders, parcel.order) >= 0);
            yield return (parcel, from, PostTerminal.Find(parcel.order.to));
        }
    }
}
