using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Главное меню (сцена MainMenu): продолжить с сохранения, новая игра, настройки, выход. Esc его не закрывает.
// После мета-финала (MetaEndingScreen.ContactLost) в углу изредка вспыхивает метка — пасхалка;
// клик по ней показывает текст автора из MetaEndingSettings.
public class MainMenu : MenuScreen
{
    public SettingsMenu settings;
    public MetaEndingSettings meta;       // метка и текст пасхалки
    public Button egg;                    // метка в углу; видна только после мета-финала

    bool confirmNew;
    TMP_Text eggLabel;

    void Start()
    {
        Time.timeScale = 1f;
        if (egg != null)
        {
            bool shown = meta != null && MetaEndingScreen.ContactLost;
            egg.gameObject.SetActive(shown);
            eggLabel = egg.GetComponentInChildren<TMP_Text>(true);
            if (shown)
            {
                eggLabel.text = meta.easterEggMark;
                egg.onClick.AddListener(() => status.text = meta.easterEggText);
            }
        }
        Open();
    }

    public void Open()
    {
        confirmNew = false;
        if (!Show()) return;
        title.text = "Дальняя почта";
        Refresh();
    }

    // Esc и E не закрывают; метка почти невидима и вспыхивает на долю секунды раз в несколько секунд.
    protected override void Update()
    {
        if (eggLabel != null && egg.gameObject.activeInHierarchy)
            eggLabel.alpha = Mathf.Repeat(Time.unscaledTime, 6.3f) < 0.12f ? 0.9f : 0.05f;
    }

    protected override void Refresh()
    {
        ClearRows();
        if (SaveGame.Exists) AddRow("Продолжить", () => { Session.Play(); return null; });
        AddRow(confirmNew ? "Новая игра — точно? Сохранение сотрётся" : "Новая игра", () =>
        {
            if (!SaveGame.Exists || confirmNew) Session.NewGame();
            confirmNew = true;
            return null;
        });
        if (settings != null) AddRow("Настройки", () => { Close(); settings.Open(Open); return null; });
        AddRow("Выход", () => { Application.Quit(); return null; });
    }
}
