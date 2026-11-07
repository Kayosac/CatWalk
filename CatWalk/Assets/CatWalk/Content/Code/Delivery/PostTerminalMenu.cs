using System.Collections.Generic;

// Экранное меню почты: баланс, заказы этой почты с наградами и отметка доставки.
public class PostTerminalMenu : MenuScreen
{
    PostTerminal terminal;

    public void Open(PostTerminal owner)
    {
        terminal = owner;
        if (Show()) Refresh();
    }

    protected override void Refresh()
    {
        ClearRows();
        title.text = $"{terminal.title} · на счету {Wallet.Format(Wallet.Current.Balance)}";

        foreach (DeliveryOrder order in terminal.orders)
        {
            PostTerminal to = PostTerminal.Find(order.to);
            string line = $"{order.cargo} → {(to != null ? to.title : order.to)} · {Wallet.Format(order.reward)}";
            if (terminal.IsTaken(order)) AddRow(line + " · в пути");
            else AddRow("Взять: " + line, terminal.HasFreeIssueSlot ? () =>
            {
                terminal.Take(order);
                return $"Посылка «{order.cargo}» ждёт в зоне выдачи.";
            } : null);
        }

        List<Parcel> received = terminal.Received();
        int reward = PostTerminal.Reward(received);
        AddRow(received.Count > 0 ? $"Отметить доставку ({received.Count}) · +{Wallet.Format(reward)}" : "Отметить доставку — в зоне приёма нет посылок сюда",
            received.Count > 0 ? () =>
            {
                terminal.Complete(received);
                return $"Доставлено посылок: {received.Count}. Оплата: +{Wallet.Format(reward)}.";
            } : null);
    }
}
