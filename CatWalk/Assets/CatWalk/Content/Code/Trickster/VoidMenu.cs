using UnityEngine;
using UnityEngine.SceneManagement;

// Меню «Вечной пустоты» вместо паузы (открывает BlackHole по Esc): продолжить скитаться, начать заново
// (сохранение стирается, со второго нажатия) или выйти в главное меню — сцену MainMenu, а если её нет в сборке, закрыть игру.
public class VoidMenu : MenuScreen
{
    const string MainMenu = "MainMenu";

    bool confirmNew;

    public void Open()
    {
        confirmNew = false;
        if (!Show()) return;
        if (status != null) status.text = "Связи нет.";
        Refresh();
    }

    protected override void Refresh()
    {
        ClearRows();
        title.text = "Вечная пустота";
        AddRow("Продолжить скитаться", () => { Close(); return null; });
        AddRow(confirmNew ? "Начать заново — точно? Сохранение сотрётся" : "Начать заново", () =>
        {
            if (confirmNew) SaveGame.Current.NewGame();
            confirmNew = true;
            return null;
        });
        AddRow("Выйти в главное меню", () =>
        {
            Close();
            if (Application.CanStreamedLevelBeLoaded(MainMenu)) SceneManager.LoadScene(MainMenu);
            else Application.Quit();
            return null;
        });
    }
}
