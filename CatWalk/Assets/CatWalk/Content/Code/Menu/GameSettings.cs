using UnityEngine;

// Настройки игрока в PlayerPrefs: мышь и правый стик геймпада (множители чувствительности, инверсия Y), VSync и предел FPS,
// подсказки обучения. Переназначение клавиш хранит Controls (Input/).
// Режим экрана и разрешение Unity запоминает в PlayerPrefs сама. Читаются и применяются при запуске игры.
public static class GameSettings
{
    public static readonly float[] Sensitivities = { 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 2.5f, 3f };
    public static readonly int[] FpsLimits = { 0, 30, 60, 120, 144 }; // 0 — без ограничения

    public const float StickSpeed = 150f; // взгляд правым стиком на полном отклонении при множителе 1, °/с

    public static float Sensitivity { get; private set; } = 1f; // множитель к sensitivity камер
    public static float StickSensitivity { get; private set; } = 1f; // множитель к StickSpeed
    public static bool InvertY { get; private set; }
    public static bool VSync { get; private set; }
    public static int FpsLimit { get; private set; }
    public static bool Tutorial { get; private set; } = true; // подсказки обучения (Onboarding/Tutorial)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Sensitivity = PlayerPrefs.GetFloat("settings.mouse", 1f);
        StickSensitivity = PlayerPrefs.GetFloat("settings.stick", 1f);
        InvertY = PlayerPrefs.GetInt("settings.invertY", 0) == 1;
        VSync = PlayerPrefs.GetInt("settings.vsync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        FpsLimit = PlayerPrefs.GetInt("settings.fps", 0);
        Tutorial = PlayerPrefs.GetInt("settings.tutorial", 1) == 1;
        ApplyFrameRate();
    }

    // Сдвиг мыши для камер: с множителем и инверсией.
    public static Vector2 Look(Vector2 delta) => new(delta.x * Sensitivity, (InvertY ? -delta.y : delta.y) * Sensitivity);

    // Отклонение правого стика -> скорость взгляда, °/с: с множителем и той же инверсией.
    public static Vector2 StickLook(Vector2 stick) =>
        new Vector2(stick.x, InvertY ? -stick.y : stick.y) * (StickSpeed * StickSensitivity);

    public static void SetSensitivity(float value)
    {
        Sensitivity = value;
        PlayerPrefs.SetFloat("settings.mouse", value);
        PlayerPrefs.Save();
    }

    public static void SetStickSensitivity(float value)
    {
        StickSensitivity = value;
        PlayerPrefs.SetFloat("settings.stick", value);
        PlayerPrefs.Save();
    }

    public static void SetInvertY(bool value)
    {
        InvertY = value;
        PlayerPrefs.SetInt("settings.invertY", value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetVSync(bool value)
    {
        VSync = value;
        PlayerPrefs.SetInt("settings.vsync", value ? 1 : 0);
        PlayerPrefs.Save();
        ApplyFrameRate();
    }

    public static void SetFpsLimit(int value)
    {
        FpsLimit = value;
        PlayerPrefs.SetInt("settings.fps", value);
        PlayerPrefs.Save();
        ApplyFrameRate();
    }

    public static void SetTutorial(bool value)
    {
        Tutorial = value;
        PlayerPrefs.SetInt("settings.tutorial", value ? 1 : 0);
        PlayerPrefs.Save();
    }

    static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FpsLimit > 0 ? FpsLimit : -1;
    }
}
