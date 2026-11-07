using System;
using System.Collections.Generic;
using UnityEngine;

// Настройки: мышь, экран, частота кадров (GameSettings). Открываются из главного меню и из паузы;
// закрытие (Esc или «Назад») возвращает туда, откуда открыли. Клик по строке — следующее значение.
// Звук и переназначение клавиш — позже.
public class SettingsMenu : MenuScreen
{
    Action back;                          // куда вернуться после закрытия
    Vector2Int size;                      // выбранное разрешение: Screen.width обновится только в следующем кадре
    bool fullscreen;

    public void Open(Action onClose)
    {
        if (!Show()) return;
        back = onClose;
        size = new Vector2Int(Screen.width, Screen.height);
        fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
        title.text = "Настройки";
        Refresh();
    }

    public override void Close()
    {
        if (Opened != this) return;
        base.Close();
        Action then = back;
        back = null;
        then?.Invoke();
    }

    protected override void Refresh()
    {
        ClearRows();
        AddRow($"Чувствительность мыши: {GameSettings.Sensitivity:0.##}", () =>
        {
            GameSettings.SetSensitivity(Next(GameSettings.Sensitivities, GameSettings.Sensitivity));
            return null;
        });
        AddRow("Инверсия мыши по вертикали: " + OnOff(GameSettings.InvertY), () =>
        {
            GameSettings.SetInvertY(!GameSettings.InvertY);
            return null;
        });
        AddRow("Экран: " + (fullscreen ? "полный экран" : "окно"), () =>
        {
            fullscreen = !fullscreen;
            return ApplyScreen();
        });
        AddRow($"Разрешение: {size.x}x{size.y}", () =>
        {
            size = Next(Sizes(), size);
            return ApplyScreen();
        });
        AddRow("Вертикальная синхронизация: " + OnOff(GameSettings.VSync), () =>
        {
            GameSettings.SetVSync(!GameSettings.VSync);
            return null;
        });
        AddRow("Ограничение FPS: " + (GameSettings.FpsLimit > 0 ? GameSettings.FpsLimit.ToString() : "нет"), () =>
        {
            GameSettings.SetFpsLimit(Next(GameSettings.FpsLimits, GameSettings.FpsLimit));
            return GameSettings.VSync ? "С вертикальной синхронизацией FPS = частоте монитора." : null;
        });
        AddRow("Назад", () => { Close(); return null; });
    }

    string ApplyScreen()
    {
        Screen.SetResolution(size.x, size.y, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        return Application.isEditor ? "В редакторе экран не меняется — проверь в сборке." : null;
    }

    // Разрешения монитора без повторов (частоты обновления не различаем).
    static Vector2Int[] Sizes()
    {
        var sizes = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            var s = new Vector2Int(r.width, r.height);
            if (!sizes.Contains(s)) sizes.Add(s);
        }
        return sizes.ToArray();
    }

    // Следующее значение по кругу; незнакомое — первое.
    static T Next<T>(T[] values, T current) =>
        values.Length == 0 ? current : values[(Array.IndexOf(values, current) + 1) % values.Length];

    static string OnOff(bool on) => on ? "вкл" : "выкл";
}
