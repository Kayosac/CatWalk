using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Общий экранный слой: подсказки действий внизу экрана и короткие сообщения сверху.
// Подсказку зовут каждый кадр, пока действие доступно (Hud.Prompt в Update); видны две самые важные.
// Пока открыто меню, подсказки скрыты.
public class Hud : MonoBehaviour
{
    public static Hud Current { get; private set; }

    public TMP_Text prompt;
    public TMP_Text toast;
    public int maxPrompts = 2;
    public float toastTime = 3.5f;        // с

    readonly List<(int priority, string text)> prompts = new();
    float toastUntil;

    void Awake() => Current = this;

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    // Подсказка на этот кадр. Чем выше priority, тем ближе к центру и тем раньше остальных.
    public static void Prompt(string text, int priority = 0)
    {
        if (Current != null) Current.prompts.Add((priority, text));
    }

    public static void Toast(string text)
    {
        if (Current == null) return;
        Current.toast.text = text;
        Current.toast.gameObject.SetActive(true);
        Current.toastUntil = Time.unscaledTime + Current.toastTime;
    }

    void Start() => toast.gameObject.SetActive(false);

    void LateUpdate()
    {
        prompts.Sort((a, b) => b.priority.CompareTo(a.priority));
        var lines = new List<string>();
        if (MenuScreen.Opened == null)
            foreach ((int _, string text) in prompts)
                if (lines.Count < maxPrompts && !lines.Contains(text)) lines.Add(text);
        prompt.text = string.Join("\n", lines);
        prompts.Clear();

        if (toast.gameObject.activeSelf && Time.unscaledTime >= toastUntil) toast.gameObject.SetActive(false);
    }
}
