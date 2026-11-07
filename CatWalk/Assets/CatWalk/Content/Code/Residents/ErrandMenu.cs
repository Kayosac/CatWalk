// Предложение поручения: житель рассказывает дело, кот берётся или отказывается; «Подумаю» — закрыть без ответа.
public class ErrandMenu : MenuScreen
{
    public void Open(Resident resident, Errand errand)
    {
        if (!Show()) return;
        ClearRows();
        title.text = $"{resident.profile.displayName} | есть дело";
        if (status != null) status.text = errand.offer;
        PostTerminal to = PostTerminal.Find(errand.order.to);
        AddRow($"Взяться: {errand.order.cargo} -> {(to != null ? to.title : errand.order.to)} | {Wallet.Format(errand.order.reward)} | {errand.LegalityLabel}", () =>
        {
            Close();
            resident.Accept(errand);
            return null;
        });
        AddRow("Отказаться", () =>
        {
            Close();
            resident.Refuse(errand);
            return null;
        });
        AddRow("Подумаю", () =>
        {
            Close();
            return null;
        });
    }
}
