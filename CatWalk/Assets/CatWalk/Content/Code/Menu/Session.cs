using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Переходы между главным меню и игрой. Сцены по имени: «MainMenu» (build index 0) и «Prototype» (1).
// Перед сменой сцены закрываются открытые меню — время и курсор возвращаются.
public static class Session
{
    public const string MainMenuScene = "MainMenu";
    public const string GameScene = "Prototype";

    // Игра; сохранение, если есть, подхватится само (SaveGame).
    public static void Play() => Load(GameScene);

    // Игра с нуля: сохранение стирается.
    public static void NewGame()
    {
        if (SaveGame.Exists) File.Delete(SaveGame.FilePath);
        Load(GameScene);
    }

    public static void ToMainMenu() => Load(MainMenuScene);

    static void Load(string scene)
    {
        // Настройки при закрытии возвращают паузу — закрываем по цепочке.
        for (int i = 0; i < 4 && MenuScreen.Opened != null; i++) MenuScreen.Opened.Close();
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }
}
