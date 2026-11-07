using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Экранное меню почты: баланс, заказы этой почты с наградами и отметка доставки. Пока оно открыто, кот стоит, курсор свободен.
// E или Esc — закрыть.
public class PostTerminalMenu : MonoBehaviour
{
    public TMP_Text title;
    public TMP_Text status;
    public Transform list;                // сюда добавляются строки
    public Button rowTemplate;            // неактивный образец строки

    PostTerminal terminal;
    int openedFrame;
    readonly List<GameObject> rows = new();
    Behaviour[] paused;

    public void Open(PostTerminal owner)
    {
        terminal = owner;
        openedFrame = Time.frameCount;
        gameObject.SetActive(true);
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // Кот стоит на месте, камера не ловит мышь.
        ParcelCarrier cat = ParcelCarrier.Current;
        paused = new Behaviour[] { cat, cat.GetComponent<CatController>(), FindFirstObjectByType<CatCamera>() };
        foreach (Behaviour b in paused) if (b != null) b.enabled = false;
        cat.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        status.text = "";
        Refresh();
    }

    void Close()
    {
        gameObject.SetActive(false);
        foreach (Behaviour b in paused) if (b != null) b.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k != null && Time.frameCount != openedFrame && (k.eKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)) Close();
    }

    void Refresh()
    {
        foreach (GameObject row in rows) Destroy(row);
        rows.Clear();
        title.text = $"{terminal.title} · на счету {Wallet.Format(Wallet.Current.Balance)}";

        foreach (DeliveryOrder order in terminal.orders)
        {
            PostTerminal to = PostTerminal.Find(order.to);
            string line = $"{order.cargo} → {(to != null ? to.title : order.to)} · {Wallet.Format(order.reward)}";
            if (terminal.IsTaken(order)) AddRow(line + " · в пути", null);
            else AddRow("Взять: " + line, terminal.HasFreeIssueSlot ? () =>
            {
                terminal.Take(order);
                return $"Посылка «{order.cargo}» ждёт в зоне выдачи.";
            } : null);
        }

        List<Parcel> received = terminal.Received();
        int reward = PostTerminal.Reward(received);
        AddRow(received.Count > 0 ? $"Отметить доставку ({received.Count}) · +{Wallet.Format(reward)}" : "Отметить доставку — в зоне приёма нет посылок сюда",
            received.Count > 0 ? () =>
            {
                terminal.Complete(received);
                return $"Доставлено посылок: {received.Count}. Оплата: +{Wallet.Format(reward)}.";
            } : null);
    }

    void AddRow(string text, Func<string> action)
    {
        Button row = Instantiate(rowTemplate, list);
        row.gameObject.SetActive(true);
        row.GetComponentInChildren<TMP_Text>().text = text;
        row.interactable = action != null;
        if (action != null) row.onClick.AddListener(() => { status.text = action(); Refresh(); });
        rows.Add(row.gameObject);
    }
}
