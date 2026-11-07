// Экран финала долга: «Окончательная гибель» (давление 100) или «Освобождение» (долг выплачен).
// Esc его не закрывает — только кнопки. Перед показом закрывает любое открытое меню.
public class DebtEnding : MenuScreen
{
    public void Open(bool freed)
    {
        if (Opened != null) Opened.Close();
        if (!Show()) return;
        ClearRows();
        title.text = freed ? "Освобождение" : "Окончательная гибель";
        if (status != null) status.text = freed
            ? "Последний кредит выплачен. Космос снова твой."
            : "Терпение кредиторов кончилось. Космокар забрали, почтальона больше никто не видел.";
        if (freed) AddRow("Продолжить свободным", () => { Close(); return null; });
        AddRow("Начать заново", () => { SaveGame.Current.NewGame(); return null; });
    }

    protected override void Update() { }
}
