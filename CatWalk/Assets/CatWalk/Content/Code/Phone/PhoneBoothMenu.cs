using System;

// Экранное меню телефонной будки: сохранить игру, позвонить домой (пока никто не берёт), повесить трубку.
public class PhoneBoothMenu : MenuScreen
{
    public void Open(PhoneBooth booth)
    {
        if (!Show()) return;
        title.text = $"Телефон | {booth.Place}";
        ClearRows();
        AddRow("Сохранить игру", () =>
        {
            SaveGame.Current.Save();
            return $"Сохранено. {DateTime.Now:HH:mm}";
        });
        AddRow("Позвонить домой", () => "Гудки... никто не берёт. (звонки появятся позже)");
        AddRow("Повесить трубку", () => { Close(); return null; });
    }
}
