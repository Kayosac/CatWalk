using System.Collections.Generic;
using UnityEngine;

// Журнал заказов: все взятые заказы, то есть посылки в мире, — груз, откуда, куда, награда (хрупкое — по сохранности),
// где посылка сейчас и сколько от машины до адресата. Открывается по J (Navigator) в машине и пешком, время на паузе.
// Клик по строке — цель навигатора на почту-адресата. J или Esc — закрыть.
public class OrderJournal : MenuScreen
{
    int openFrame;

    // Взятые заказы: посылка, почта, которая её выдала, и почта-адресат (null, если такой почты нет в сцене).
    public static IEnumerable<(Parcel parcel, PostTerminal from, PostTerminal to)> Active()
    {
        foreach (Parcel parcel in Parcel.All)
            if (parcel.order != null)
                yield return (parcel, PostTerminal.All.Find(t => t.orders != null && System.Array.IndexOf(t.orders, parcel.order) >= 0),
                              PostTerminal.Find(parcel.order.to));
    }

    public void Open()
    {
        if (!Show()) return;
        openFrame = Time.frameCount;
        Refresh();
    }

    protected override void Update()
    {
        base.Update();
        if (Opened == this && Controls.Journal.WasPressedThisFrame() && Time.frameCount != openFrame) Close();
    }

    protected override void Refresh()
    {
        ClearRows();
        Navigator navigator = Navigator.Current;
        int count = 0, sum = 0;
        foreach (var (parcel, from, to) in Active())
        {
            count++;
            sum += parcel.Reward;
            Location target = to != null ? to.GetComponentInParent<Location>() : null;
            string line = $"{parcel.Label} | {(from != null ? from.title : "?")} -> {(to != null ? to.title : parcel.order.to)} | {Wallet.Format(parcel.Reward)} | {Where(parcel)}";
            if (to != null && navigator != null) line += " | " + Navigator.FormatDistance(Vector3.Distance(navigator.transform.position, to.transform.position));
            if (target != null && navigator != null && navigator.Target == target) line = "[цель] " + line;
            AddRow(line, target != null && navigator != null ? () =>
            {
                navigator.Target = target;
                return $"Цель: {target.Label}";
            } : null);
        }
        title.text = $"Журнал заказов | заказов: {count} | награды: {Wallet.Format(sum)}";
        if (count == 0) AddRow("Заказов нет — возьми на любой почте");
    }

    // Где посылка: слот почты, полка машины, руки кота или просто лежит.
    static string Where(Parcel parcel)
    {
        ParcelSlot slot = parcel.Slot;
        if (slot == null) return ParcelCarrier.Current != null && ParcelCarrier.Current.Held == parcel ? "в руках" : "лежит";
        foreach (PostTerminal terminal in PostTerminal.All)
        {
            if (System.Array.IndexOf(terminal.issueSlots, slot) >= 0) return $"в зоне выдачи «{terminal.title}»";
            if (System.Array.IndexOf(terminal.receiveSlots, slot) >= 0) return $"в зоне приёма «{terminal.title}»";
        }
        return slot.GetComponentInParent<CosmoCarController>() != null ? "на полке машины" : "в слоте";
    }
}
