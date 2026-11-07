using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Экранное меню: заголовок, строки-кнопки, строка статуса. Основа меню почты, колонки, мастерской и паузы.
// Пока меню открыто, кот стоит, камеры не ловят мышь, курсор свободен. Открыто только одно меню сразу.
// Esc (B, Start) закрывает всегда, E с клавиатуры — если closeOnE. Строки выбираются стрелками и стиком:
// со второго кадра выделена первая строка, после перестройки — строка на том же месте.
public class MenuScreen : MonoBehaviour
{
    public static MenuScreen Opened { get; private set; }
    public static int ClosedFrame { get; private set; } = -1; // кадр, когда закрыли меню: его клавиша в мир не идёт (Controls.Pressed)

    public TMP_Text title;
    public TMP_Text status;
    public Transform list;                // сюда добавляются строки
    public Button rowTemplate;            // неактивный образец строки
    public bool closeOnE = true;
    public bool pauseTime;                // останавливает время, пока открыто

    int openedFrame;
    int selectedRow;                      // какую строку держать выделенной
    readonly List<Button> rows = new();
    readonly List<Behaviour> paused = new();

    // Открыть, если не открыто другое меню.
    protected bool Show()
    {
        if (Opened != null) return false;
        Opened = this;
        openedFrame = Time.frameCount;
        selectedRow = 0;
        gameObject.SetActive(true);
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        // Всё, что слушает клавиатуру и мышь в мире, замирает.
        paused.Clear();
        Pause(FindFirstObjectByType<CatCamera>());
        Pause(FindFirstObjectByType<CosmoCarCamera>());
        Pause(FindFirstObjectByType<CosmoCarSeat>());
        if (ParcelCarrier.Current != null)
        {
            Pause(ParcelCarrier.Current);
            Pause(ParcelCarrier.Current.GetComponent<CatController>());
            Rigidbody body = ParcelCarrier.Current.GetComponent<Rigidbody>();
            if (!pauseTime) body.linearVelocity = Vector3.Project(body.linearVelocity, ParcelCarrier.Current.transform.up);
        }
        if (pauseTime) Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (status != null) status.text = "";
        return true;
    }

    void Pause(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        paused.Add(b);
    }

    public virtual void Close()
    {
        if (Opened != this) return;
        Opened = null;
        ClosedFrame = Time.frameCount;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        gameObject.SetActive(false);
        foreach (Behaviour b in paused) if (b != null) b.enabled = true;
        paused.Clear();
        if (pauseTime) Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    protected virtual void Update()
    {
        if (Time.frameCount == openedFrame || Controls.Rebinding) return;
        if (Controls.Back.WasPressedThisFrame() || (closeOnE && Controls.PressedOnKeys(Controls.Interact))) Close();
    }

    // Выделение для стрелок и геймпада. Не в кадре открытия: иначе A, открывшая меню, сразу нажмёт первую строку.
    void LateUpdate()
    {
        if (Opened != this || Time.frameCount == openedFrame || EventSystem.current == null) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        int index = rows.FindIndex(r => r.gameObject == selected);
        if (index >= 0)
        {
            selectedRow = index;
            return;
        }
        if (selected != null && selected.activeInHierarchy && selected.transform.parent != list) return; // другая кнопка меню
        // Ближайшая доступная строка к прежней: сначала ниже, потом выше.
        for (int i = 0; i < rows.Count; i++)
            foreach (int j in new[] { selectedRow + i, selectedRow - i })
                if (j >= 0 && j < rows.Count && rows[j].interactable)
                {
                    EventSystem.current.SetSelectedGameObject(rows[j].gameObject);
                    return;
                }
    }

    void OnDisable()
    {
        if (Opened == this) Close();
    }

    // Строка: action выполняется по клику и возвращает текст статуса (или null). Без action строка неактивна.
    protected Button AddRow(string text, Func<string> action = null)
    {
        Button row = Instantiate(rowTemplate, list);
        row.gameObject.SetActive(true);
        SetText(row, text);
        row.interactable = action != null;
        // Выделенная строка (стрелки, геймпад) заметно светлее остальных: множитель 2, обычная — половина.
        ColorBlock colors = row.colors;
        colors.colorMultiplier = 2f;
        colors.normalColor = new Color(0.5f, 0.5f, 0.5f);
        colors.highlightedColor = new Color(0.65f, 0.65f, 0.65f);
        colors.pressedColor = new Color(0.4f, 0.4f, 0.4f);
        colors.selectedColor = new Color(0.95f, 0.95f, 0.95f);
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        row.colors = colors;
        if (action != null) row.onClick.AddListener(() =>
        {
            string result = action();
            if (result != null && status != null) status.text = result;
            Refresh();
        });
        rows.Add(row);
        return row;
    }

    protected void ClearRows()
    {
        foreach (Button row in rows) Destroy(row.gameObject);
        rows.Clear();
    }

    // Перестроить содержимое; вызывается после каждого клика.
    protected virtual void Refresh() { }

    protected static void SetText(Button row, string text) => row.GetComponentInChildren<TMP_Text>().text = text;
}
