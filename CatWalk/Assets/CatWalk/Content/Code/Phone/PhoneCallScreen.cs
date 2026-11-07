using Febucci.TextAnimatorForUnity;
using UnityEngine;
using UnityEngine.InputSystem;

// Экран звонка домой, время на паузе. Реплики по одной, печатаются посимвольно (Text Animator по неигровому времени).
// E, пробел или клик — допечатать реплику, потом следующая; после последней звонок засчитан.
// Esc — повесить трубку: недослушанный звонок не засчитывается и подождёт следующего раза.
// В обоих случаях снова открывается меню будки.
public class PhoneCallScreen : MenuScreen
{
    public TypewriterComponent typewriter; // на тексте status

    PhoneCall call;
    PhoneBooth booth;
    int line, openFrame;

    public void Open(PhoneCall phoneCall, PhoneBooth from)
    {
        if (!Show()) return;
        call = phoneCall;
        booth = from;
        line = 0;
        openFrame = Time.frameCount;
        title.text = $"Звонок домой | {booth.Place}";
        if (call.lines == null || call.lines.Length == 0) End(true);
        else ShowLine();
    }

    void ShowLine()
    {
        PhoneCall.Line l = call.lines[line];
        string[] names = PhoneLine.Current.catalog.names;
        string who = (int)l.who < names.Length ? names[(int)l.who] : l.who.ToString();
        typewriter.ShowText($"<b>{who}</b>\n{l.text}");
    }

    protected override void Update()
    {
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        if (Opened != this || call == null || Time.frameCount == openFrame) return;
        if (k != null && k.escapeKey.wasPressedThisFrame)
        {
            End(false);
            return;
        }
        bool next = (k != null && (k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                 || (m != null && m.leftButton.wasPressedThisFrame);
        if (!next) return;
        if (typewriter.IsShowingText) typewriter.SkipTypewriter();
        else if (++line < call.lines.Length) ShowLine();
        else End(true);
    }

    void End(bool finished)
    {
        PhoneCall heard = call;
        call = null;
        Close();
        booth.menu.Open(booth, finished ? PhoneLine.Current.Done(heard) : "Повесил трубку. Договорим в другой раз.");
    }
}
