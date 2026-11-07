using System;
using System.Collections.Generic;
using UnityEngine;

// Настройки: мышь и стик, экран, частота кадров, подсказки обучения (GameSettings), управление (Controls). Открываются из главного меню
// и из паузы; закрытие (Esc или «Назад») возвращает туда, откуда открыли. Клик по строке — следующее значение.
// «Управление» — список действий с клавишами «клавиатура | геймпад»: выбор строки мышью или Enter ждёт новую клавишу,
// кнопкой A — новую кнопку геймпада; Esc — отмена. Esc в списке — назад к настройкам. Звук — позже.
public class SettingsMenu : MenuScreen
{
    Action back;                          // куда вернуться после закрытия
    Vector2Int size;                      // выбранное разрешение: Screen.width обновится только в следующем кадре
    bool fullscreen;
    bool bindings;                        // открыт список управления

    public void Open(Action onClose)
    {
        if (!Show()) return;
        back = onClose;
        size = new Vector2Int(Screen.width, Screen.height);
        fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
        bindings = false;
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

    protected override void Update()
    {
        if (Opened == this && bindings && !Controls.Rebinding && Controls.Back.WasPressedThisFrame())
        {
            bindings = false;
            Refresh();
            return;
        }
        base.Update();
    }

    protected override void Refresh()
    {
        ClearRows();
        if (bindings)
        {
            BindingRows();
            return;
        }
        title.text = "Настройки";
        AddRow("Подсказки обучения: " + OnOff(GameSettings.Tutorial), () =>
        {
            GameSettings.SetTutorial(!GameSettings.Tutorial);
            return null;
        });
        AddRow($"Чувствительность мыши: {GameSettings.Sensitivity:0.##}", () =>
        {
            GameSettings.SetSensitivity(Next(GameSettings.Sensitivities, GameSettings.Sensitivity));
            return null;
        });
        AddRow($"Чувствительность стика: {GameSettings.StickSensitivity:0.##}", () =>
        {
            GameSettings.SetStickSensitivity(Next(GameSettings.Sensitivities, GameSettings.StickSensitivity));
            return null;
        });
        AddRow("Инверсия по вертикали: " + OnOff(GameSettings.InvertY), () =>
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
        AddRow("Управление", () => { bindings = true; return "Мышь или Enter — новая клавиша, A — новая кнопка геймпада. Esc — отмена."; });
        AddRow("Назад", () => { Close(); return null; });
    }

    // Строки «действие: клавиша | кнопка геймпада»; выбор — ждать новую.
    void BindingRows()
    {
        title.text = "Управление | клавиатура | геймпад";
        for (int i = 0; i < Controls.Rows.Count; i++)
        {
            var (label, action, key, pad) = Controls.Rows[i];
            int row = i;
            AddRow($"{label}: {Controls.Key(action, key)} | {Controls.Key(action, pad)}", () =>
            {
                bool onPad = Controls.UsingPad;
                if (!Controls.Rebind(row, onPad, ok =>
                    {
                        if (Opened != this) return;
                        status.text = ok ? $"{label}: {Controls.Key(action, onPad ? pad : key)}" : "Отменено.";
                        Refresh();
                    }))
                    return Controls.Rebinding ? null : "Стик не переназначается.";
                return onPad ? $"Нажми кнопку геймпада для «{label}» | Esc — отмена" : $"Нажми клавишу для «{label}» | Esc — отмена";
            });
        }
        AddRow("Сбросить по умолчанию", () => { Controls.ResetBindings(); return "Управление по умолчанию."; });
        AddRow("Назад", () => { bindings = false; return null; });
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
