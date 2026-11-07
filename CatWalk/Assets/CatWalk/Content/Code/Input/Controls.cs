using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Игровой ввод: действия Input System с клавиатурой+мышью и геймпадом, собираются в коде без ассета.
// Переназначения хранятся в PlayerPrefs строками «действие/индекс биндинга=путь» (SaveBindingOverridesAsJson не годится:
// он ищет биндинги по id, а у собранных в коде id новые при каждом запуске) и подхватываются при запуске.
// Переназначает SettingsMenu (Rows, Rebind).
// Pressed(action) — нажатие для игрового мира: молчит, пока открыто меню, и в кадре, когда его закрыли
// (та же E или A, что закрыла меню, не сажает в машину). Key(action) — имя клавиши для подсказок:
// геймпадное, если последним трогали геймпад.
public static class Controls
{
    const string Prefs = "settings.bindings";
    const string Keys = "Keyboard", Pad = "Gamepad"; // группы биндингов

    static readonly InputActionMap map = new("Game");

    // Строки переназначения: название, действие, индекс клавиатурного и геймпадного биндинга.
    public static readonly List<(string label, InputAction action, int key, int pad)> Rows = new();

    // Оси: + вперёд / вправо / вправо.
    public static readonly InputAction Forward = Axis("Forward", "<Gamepad>/leftStick/y", ("Вперёд", "Positive", "<Keyboard>/w", null), ("Назад", "Negative", "<Keyboard>/s", null));
    public static readonly InputAction Turn = Axis("Turn", "<Gamepad>/leftStick/x", ("Влево (поворот)", "Negative", "<Keyboard>/a", null), ("Вправо (поворот)", "Positive", "<Keyboard>/d", null));
    public static readonly InputAction Strafe = Axis("Strafe", null, ("Вбок влево", "Negative", "<Keyboard>/q", "<Gamepad>/dpad/left"), ("Вбок вправо", "Positive", "<Keyboard>/e", "<Gamepad>/dpad/right"));

    public static readonly InputAction Up = Button("Up", "Вверх | прыжок", "<Keyboard>/space", "<Gamepad>/rightTrigger");
    public static readonly InputAction Down = Button("Down", "Вниз", "<Keyboard>/c", "<Gamepad>/leftTrigger");
    public static readonly InputAction Boost = Button("Boost", "Форсаж | бег", "<Keyboard>/leftShift", "<Gamepad>/rightShoulder");
    public static readonly InputAction Steer = Button("Steer", "Рулить за камерой", "<Mouse>/rightButton", "<Gamepad>/leftShoulder");
    public static readonly InputAction Interact = Button("Interact", "Действие: терминалы, посадка", "<Keyboard>/e", "<Gamepad>/buttonSouth");
    public static readonly InputAction Use = Button("Use", "Посылки, выравнивание, разговор", "<Keyboard>/f", "<Gamepad>/buttonWest");
    public static readonly InputAction Map = Button("Map", "Карта", "<Keyboard>/m", "<Gamepad>/buttonNorth");
    public static readonly InputAction Journal = Button("Journal", "Журнал заказов", "<Keyboard>/j", "<Gamepad>/select");
    public static readonly InputAction Target = Button("Target", "Следующая цель", "<Keyboard>/t", "<Gamepad>/dpad/up");
    public static readonly InputAction Tow = Button("Tow", "Буксир", "<Keyboard>/h", "<Gamepad>/dpad/down");
    public static readonly InputAction Pause = Button("Pause", "Пауза", "<Keyboard>/escape", "<Gamepad>/start");
    public static readonly InputAction Back = Button("Back", null, "<Keyboard>/escape", "<Gamepad>/buttonEast", "<Gamepad>/start"); // закрыть меню

    static readonly InputAction lookMouse = map.AddAction("LookMouse", InputActionType.PassThrough, "<Mouse>/delta", groups: Keys, expectedControlLayout: "Vector2");
    public static readonly InputAction Stick = map.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick", groups: Pad, expectedControlLayout: "Vector2");

    public static bool UsingPad { get; private set; } // последним трогали геймпад
    // Ждём новую клавишу — и ещё кадр после: Esc отмены не должен закрыть меню.
    public static bool Rebinding => rebinding || Time.frameCount == rebindFrame;
    static bool rebinding;
    static int rebindFrame = -1;

    // Ходьба: x — вправо, y — вперёд.
    public static Vector2 Move => Vector2.ClampMagnitude(new Vector2(Turn.ReadValue<float>(), Forward.ReadValue<float>()), 1f);

    // Маршевый: «Shift+Space».
    public static string Cruise => $"{Key(Boost)}+{Key(Up)}";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        map.RemoveAllBindingOverrides();
        foreach (string line in PlayerPrefs.GetString(Prefs, "").Split('\n'))
        {
            int slash = line.IndexOf('/'), equals = line.IndexOf('=');
            if (slash < 0 || equals < slash) continue;
            InputAction action = map.FindAction(line.Substring(0, slash));
            if (action != null && int.TryParse(line.Substring(slash + 1, equals - slash - 1), out int index) && index < action.bindings.Count)
                action.ApplyBindingOverride(index, line.Substring(equals + 1));
        }
        map.actionTriggered -= Track;
        map.actionTriggered += Track;
        map.Enable();
    }

    static void Track(InputAction.CallbackContext context)
    {
        if (context.control != null) UsingPad = context.control.device is Gamepad;
    }

    public static bool Pressed(InputAction action) =>
        action.WasPressedThisFrame() && MenuScreen.Opened == null && Time.frameCount != MenuScreen.ClosedFrame;

    // Нажато с клавиатуры (E закрывает меню, а A геймпада там — выбор строки).
    public static bool PressedOnKeys(InputAction action) => action.WasPressedThisFrame() && action.activeControl?.device is not Gamepad;

    // Поворот камеры за кадр, °: мышь (только при захваченном курсоре) с множителем камеры и правый стик.
    public static Vector2 Look(float mouseSensitivity) =>
        (Cursor.lockState == CursorLockMode.Locked ? GameSettings.Look(lookMouse.ReadValue<Vector2>()) * mouseSensitivity : Vector2.zero)
        + GameSettings.StickLook(Stick.ReadValue<Vector2>()) * Time.deltaTime;

    // «E — почта».
    public static string Hint(InputAction action, string text) => $"{Key(action)} — {text}";

    // Клавиши действия текущего устройства: «E», у осей — «W/S».
    public static string Key(InputAction action)
    {
        string group = UsingPad ? Pad : Keys;
        var names = new List<string>();
        foreach (InputBinding b in action.bindings)
            if (!b.isComposite && b.groups == group) names.Add(Name(b.effectivePath));
        return names.Count > 0 ? string.Join("/", names) : "-";
    }

    // Имя биндинга по индексу (для строк переназначения).
    public static string Key(InputAction action, int index) => Name(action.bindings[index].effectivePath);

    static readonly Dictionary<string, string> names = new()
    {
        ["leftShift"] = "Shift", ["rightShift"] = "RShift", ["leftCtrl"] = "Ctrl", ["leftAlt"] = "Alt", ["escape"] = "Esc",
        ["space"] = "Space", ["enter"] = "Enter", ["tab"] = "Tab", ["backspace"] = "Backspace",
        ["leftButton"] = "ЛКМ", ["rightButton"] = "ПКМ", ["middleButton"] = "СКМ",
        ["buttonSouth"] = "A", ["buttonEast"] = "B", ["buttonWest"] = "X", ["buttonNorth"] = "Y",
        ["leftShoulder"] = "LB", ["rightShoulder"] = "RB", ["leftTrigger"] = "LT", ["rightTrigger"] = "RT",
        ["start"] = "Start", ["select"] = "View", ["leftStickPress"] = "L3", ["rightStickPress"] = "R3",
        ["dpad/up"] = "крест вверх", ["dpad/down"] = "крест вниз", ["dpad/left"] = "крест влево", ["dpad/right"] = "крест вправо",
        ["leftStick/x"] = "левый стик", ["leftStick/y"] = "левый стик", ["rightStick"] = "правый стик",
    };

    static string Name(string path)
    {
        if (string.IsNullOrEmpty(path)) return "-";
        string control = path.Substring(path.IndexOf('/') + 1);
        if (names.TryGetValue(control, out string name)) return name;
        if (control.Length == 1) return control.ToUpperInvariant();
        return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
    }

    // Ждать новую клавишу для строки: pad — геймпадный биндинг, иначе клавиатура и кнопки мыши. Esc — отмена.
    // done(true) — переназначено и сохранено. false — эту клавишу не переназначить (ось стика).
    public static bool Rebind(int row, bool pad, Action<bool> done)
    {
        var (_, action, key, padIndex) = Rows[row];
        int index = pad ? padIndex : key;
        if (rebinding || (action.type == InputActionType.Value && !action.bindings[index].isPartOfComposite)) return false;
        rebinding = true;
        map.Disable();
        var rebind = action.PerformInteractiveRebinding(index)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll").WithControlsExcluding("<Mouse>/press")
            .WithControlsExcluding("<Keyboard>/anyKey")
            .WithMatchingEventsBeingSuppressed();
        if (pad) rebind.WithControlsHavingToMatchPath("<Gamepad>");
        else rebind.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
        rebind.OnComplete(r => Finish(r, true, done)).OnCancel(r => Finish(r, false, done)).Start();
        return true;
    }

    static void Finish(InputActionRebindingExtensions.RebindingOperation rebind, bool ok, Action<bool> done)
    {
        rebind.Dispose();
        map.Enable();
        rebinding = false;
        rebindFrame = Time.frameCount;
        if (ok) Save();
        done(ok);
    }

    public static void ResetBindings()
    {
        map.RemoveAllBindingOverrides();
        Save();
    }

    static void Save()
    {
        var lines = new List<string>();
        foreach (InputAction action in map)
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].overridePath != null) lines.Add($"{action.name}/{i}={action.bindings[i].overridePath}");
        PlayerPrefs.SetString(Prefs, string.Join("\n", lines));
        PlayerPrefs.Save();
    }

    // Кнопка: первая клавиша — клавиатура/мышь, остальные — геймпад. label == null — не переназначается.
    static InputAction Button(string name, string label, string key, params string[] pads)
    {
        InputAction action = map.AddAction(name, InputActionType.Button);
        action.AddBinding(key, groups: Keys);
        foreach (string pad in pads) action.AddBinding(pad, groups: Pad);
        if (label != null) Rows.Add((label, action, 0, 1));
        return action;
    }

    // Ось из двух клавиш; на геймпаде — ось стика (padAxis) или две кнопки сторон. Строка переназначения — на каждую сторону.
    static InputAction Axis(string name, string padAxis, params (string label, string part, string key, string pad)[] sides)
    {
        InputAction action = map.AddAction(name, InputActionType.Value, expectedControlLayout: "Axis");
        var keys = action.AddCompositeBinding("1DAxis");
        foreach (var side in sides) keys.With(side.part, side.key, Keys);       // биндинги 1, 2
        if (padAxis != null) action.AddBinding(padAxis, groups: Pad);         // 3 — ось стика, не переназначается (Rebind)
        else
        {
            var pads = action.AddCompositeBinding("1DAxis");                  // 3 — композит, стороны 4, 5
            foreach (var side in sides) pads.With(side.part, side.pad, Pad);
        }
        for (int i = 0; i < sides.Length; i++) Rows.Add((sides[i].label, action, 1 + i, padAxis != null ? 3 : 4 + i));
        return action;
    }
}
