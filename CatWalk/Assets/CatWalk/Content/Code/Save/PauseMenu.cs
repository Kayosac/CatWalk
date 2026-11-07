using UnityEngine;
using UnityEngine.Events;

// Меню паузы: игра стоит. Открывает SaveGame по Esc, когда не открыто другое меню; Esc же закрывает.
// «Загрузить последнее» перезапускает сцену с файла, «Новая игра» стирает его — со второго нажатия.
// «Настройки» — SettingsMenu (вернёт сюда), «В главное меню» — сцена MainMenu (Menu/Session).
public class PauseMenu : MenuScreen
{
    public UnityEvent towAction;          // вызов буксира (Workshop); пункт виден, только если событие подписано
    public SettingsMenu settings;         // настройки (Menu/); пункт виден, если задано

    // Справка по текущим клавишам: клавиатурным или геймпадным — смотря что трогали последним.
    static string[] Help()
    {
        string boost = Controls.Key(Controls.Boost), up = Controls.Key(Controls.Up), use = Controls.Key(Controls.Use), e = Controls.Key(Controls.Interact);
        string walk = Controls.UsingPad ? Controls.Key(Controls.Forward) : Controls.Key(Controls.Forward) + "/" + Controls.Key(Controls.Turn);
        return new[]
        {
            $"Полёт: {Controls.Key(Controls.Forward)} газ | {Controls.Key(Controls.Turn)} поворот | {Controls.Key(Controls.Strafe)} вбок | {up}/{Controls.Key(Controls.Down)} вверх/вниз | {boost} форсаж",
            $"Полёт: {Controls.Cruise} — маршевый (держится, пока зажат {boost})",
            $"Полёт: {Controls.Key(Controls.Steer)} — рулить за камерой | {use} — выровняться | {e} — выйти на парковке",
            $"Пешком: {walk} ходьба | {boost} бег | {up} прыжок | {e} — сесть в машину",
            $"Посылки: {use} — взять, поставить в слот или положить",
            $"Терминалы: {e} — почта, колонка, мастерская",
            $"{Controls.Key(Controls.Map)} — карта | {Controls.Key(Controls.Target)} — следующая цель | {Controls.Key(Controls.Journal)} — журнал заказов",
            $"{Controls.Key(Controls.Tow)} — буксир | {Controls.Key(Controls.Pause)} — пауза | ЛКМ — снова поймать курсор",
        };
    }

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
            foreach (string line in Help()) AddRow(line);

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
