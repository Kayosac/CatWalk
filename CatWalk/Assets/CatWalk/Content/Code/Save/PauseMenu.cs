using UnityEngine;
using UnityEngine.Events;

// Меню паузы: игра стоит. Открывает SaveGame по Esc, когда не открыто другое меню; Esc же закрывает.
// «Загрузить последнее» перезапускает сцену с файла, «Новая игра» стирает его — со второго нажатия.
public class PauseMenu : MenuScreen
{
    public UnityEvent towAction;          // вызов буксира (Workshop); пункт виден, только если событие подписано

    static readonly string[] Controls =
    {
        "Полёт: W/S газ · A/D поворот · Q/E вбок · Space/C вверх/вниз · Shift форсаж",
        "Полёт: ПКМ — рулить мышью · F — выровняться · E — выйти на парковке",
        "Пешком: WASD ходьба · Shift бег · Space прыжок · E — сесть в машину",
        "Посылки: F — взять, поставить в слот или положить",
        "Терминалы: E — почта, колонка, мастерская",
        "Tab — карта · T — следующая цель · J — маршевый",
        "H — буксир · Esc — пауза · ЛКМ — снова поймать курсор",
    };

    bool showControls, confirmNew;

    public void Open()
    {
        showControls = confirmNew = false;
        if (Show()) Refresh();
    }

    protected override void Refresh()
    {
        ClearRows();
        title.text = "Пауза";

        AddRow("Продолжить", () => { Close(); return null; });
        AddRow("Сохранить", () => { SaveGame.Current.Save(false); return "Сохранено."; });
        AddRow(SaveGame.Exists ? "Загрузить последнее" : "Загрузить последнее — сохранений нет",
            SaveGame.Exists ? () => { SaveGame.Current.LoadLast(); return null; } : null);
        if (towAction.GetPersistentEventCount() > 0) AddRow("Вызвать буксир", () => { Close(); towAction.Invoke(); return null; });

        AddRow(showControls ? "Скрыть управление" : "Управление", () => { showControls = !showControls; return null; });
        if (showControls)
            foreach (string line in Controls) AddRow(line);

        AddRow(confirmNew ? "Новая игра — точно? Сохранение сотрётся" : "Новая игра", () =>
        {
            if (confirmNew) SaveGame.Current.NewGame();
            confirmNew = true;
            return null;
        });
        AddRow("Выйти", () =>
        {
            SaveGame.Current.Save(false);
            Application.Quit();
            return null;
        });
    }
}
