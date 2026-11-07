using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Экранное меню: заголовок, строки-кнопки, строка статуса. Основа меню почты, колонки, мастерской и паузы.
// Пока меню открыто, кот стоит, камеры не ловят мышь, курсор свободен. Открыто только одно меню сразу.
// Esc закрывает всегда, E — если closeOnE.
public class MenuScreen : MonoBehaviour
{
    public static MenuScreen Opened { get; private set; }

    public TMP_Text title;
    public TMP_Text status;
    public Transform list;                // сюда добавляются строки
    public Button rowTemplate;            // неактивный образец строки
    public bool closeOnE = true;
    public bool pauseTime;                // останавливает время, пока открыто

    int openedFrame;
    readonly List<Button> rows = new();
    readonly List<Behaviour> paused = new();

    // Открыть, если не открыто другое меню.
    protected bool Show()
    {
        if (Opened != null) return false;
        Opened = this;
        openedFrame = Time.frameCount;
        gameObject.SetActive(true);
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

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
        gameObject.SetActive(false);
        foreach (Behaviour b in paused) if (b != null) b.enabled = true;
        paused.Clear();
        if (pauseTime) Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    protected virtual void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null || Time.frameCount == openedFrame) return;
        if (k.escapeKey.wasPressedThisFrame || (closeOnE && k.eKey.wasPressedThisFrame)) Close();
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
