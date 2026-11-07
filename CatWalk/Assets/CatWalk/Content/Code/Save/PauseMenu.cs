using UnityEngine;
using UnityEngine.Events;

// Меню паузы: игра стоит. Открывает SaveGame по Esc, когда не открыто другое меню; Esc же закрывает.
// «Загрузить последнее» перезапускает сцену с файла, «Новая игра» стирает его — со второго нажатия.
// «Настройки» — SettingsMenu (вернёт сюда), «В главное меню» — сцена MainMenu (Menu/Session).
public class PauseMenu : MenuScreen
{
    public UnityEvent towAction;          // вызов буксира (Workshop); пункт виден, только если событие подписано
    public SettingsMenu settings;         // настройки (Menu/); пункт виден, если задано

    static readonly string[] Controls =
    {
        "Полёт: W/S газ | A/D поворот | Q/E вбок | Space/C вверх/вниз | Shift форсаж",
        "Полёт: Shift+Space — маршевый (держится, пока зажат Shift)",
        "Полёт: ПКМ — рулить мышью | F — выровняться | E — выйти на парковке",
        "Пешком: WASD ходьба | Shift бег | Space прыжок | E — сесть в машину",
        "Посылки: F — взять, поставить в слот или положить",
        "Терминалы: E — почта, колонка, мастерская",
        "M — карта | T — следующая цель | J — журнал заказов",
        "H — буксир | Esc — пауза | ЛКМ — снова поймать курсор",
    };

    bool showControls, confirmNew;

    public void Open()
    {
        showControls = confirmNew = false;
        if (!Show()) return;
        if (status != null) status.text = "Сохраниться можно в телефонной будке на любой станции.";
        Refresh();
    }

    protected override void Refresh()
    {
        ClearRows();
        title.text = Debt.Current != null ? "Пауза | " + Debt.Current.Summary : "Пауза";

        AddRow("Продолжить", () => { Close(); return null; });
        AddRow(SaveGame.Exists ? "Загрузить последнее" : "Загрузить последнее — сохранений нет",
            SaveGame.Exists ? () => { SaveGame.Current.LoadLast(); return null; } : null);
        if (towAction.GetPersistentEventCount() > 0) AddRow("Вызвать буксир", () => { Close(); towAction.Invoke(); return null; });

        AddRow(showControls ? "Скрыть управление" : "Управление", () => { showControls = !showControls; return null; });
        if (showControls)
            foreach (string line in Controls) AddRow(line);

        if (settings != null) AddRow("Настройки", () => { Close(); settings.Open(Open); return null; });

        AddRow(confirmNew ? "Новая игра — точно? Сохранение сотрётся" : "Новая игра", () =>
        {
            if (confirmNew) SaveGame.Current.NewGame();
            confirmNew = true;
            return null;
        });
        AddRow("В главное меню — несохранённое пропадёт", () => { Session.ToMainMenu(); return null; });
        AddRow("Выйти — несохранённое пропадёт", () =>
        {
            Application.Quit();
            return null;
        });
    }
}
