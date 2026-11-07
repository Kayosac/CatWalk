using System.Collections.Generic;

// Экранное меню почты: баланс, заказы этой почты с наградами, отметка доставки и платежи по долгу (Debt).
// Хрупкие заказы помечены, у сданных хрупких посылок видна сохранность.
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
            string line = $"{order.cargo} → {(to != null ? to.title : order.to)} · {Wallet.Format(order.reward)}{(order.fragile ? " · хрупкое" : "")}";
            if (terminal.IsTaken(order)) AddRow(line + " · в пути");
            else AddRow("Взять: " + line, terminal.HasFreeIssueSlot ? () =>
            {
                terminal.Take(order);
                return $"Посылка «{order.cargo}» ждёт в зоне выдачи.";
            } : null);
        }

        List<Parcel> received = terminal.Received();
        int reward = PostTerminal.Reward(received);
        string fragile = "";
        foreach (Parcel parcel in received)
            if (parcel.Fragile) fragile += $" · «{parcel.order.cargo}» — сохранность {Parcel.Percent(parcel.condition)}";
        AddRow(received.Count > 0 ? $"Отметить доставку ({received.Count}) · +{Wallet.Format(reward)}{fragile}" : "Отметить доставку — в зоне приёма нет посылок сюда",
            received.Count > 0 ? () =>
            {
                terminal.Complete(received);
                return $"Доставлено посылок: {received.Count}. Оплата: +{Wallet.Format(reward)}{fragile}.";
            } : null);

        Debt debt = Debt.Current;
        if (debt == null || !debt.Active) return;
        int balance = Wallet.Current.Balance;
        if (debt.Paid) AddRow($"Кредиторам: платёж внесён · следующий через {Debt.Clock(debt.PaymentTimer)}");
        else AddRow($"Кредиторам: платёж {Wallet.Format(debt.Payment)} (срок через {Debt.Clock(debt.PaymentTimer)})",
            balance >= debt.Payment ? debt.PayScheduled : null);
        if (debt.Demand > 0)
            AddRow($"Требование кредиторов: {Wallet.Format(debt.Demand)} (срок через {Debt.Clock(debt.DemandTimer)})",
                balance >= debt.Demand ? debt.PayDemand : null);
        AddRow($"Внести {Wallet.Format(debt.ExtraPayment)} в счёт долга · осталось {Wallet.Format(debt.Left)}",
            balance >= debt.ExtraPayment ? debt.PayExtra : null);
    }
}
