using System;

// Экранное меню телефонной будки: сохранить игру, позвонить домой, «Записи» — что раскрылось в звонках, повесить трубку.
public class PhoneBoothMenu : MenuScreen
{
    PhoneBooth booth;

    public void Open(PhoneBooth owner, string message = null)
    {
        booth = owner;
        if (!Show()) return;
        title.text = $"Телефон | {booth.Place}";
        Refresh();
        if (message != null) status.text = message;
    }

    protected override void Refresh()
    {
        ClearRows();
        PhoneLine line = PhoneLine.Current;
        AddRow("Сохранить игру", () =>
        {
            SaveGame.Current.Save();
            return $"Сохранено. {DateTime.Now:HH:mm}";
        });
        if (line == null) AddRow("Позвонить домой", () => "Гудки... никто не берёт.");
        else
        {
            AddRow(line.News != null ? "Позвонить домой | есть о чём поговорить" : "Позвонить домой", () => line.Call(booth));
            int count = line.Records().Count;
            AddRow($"Записи ({count})", () => count == 0 ? "Записей пока нет: о прошлом узнаётся в звонках домой."
                                                         : "- " + string.Join("\n- ", line.Records()));
        }
        AddRow("Повесить трубку", () => { Close(); return null; });
    }
}
