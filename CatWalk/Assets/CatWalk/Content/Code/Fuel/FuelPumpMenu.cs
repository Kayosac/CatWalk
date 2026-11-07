using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Экранное меню колонки: долить до полного или держать кнопку мышью, пока льётся. Пока оно открыто, кот стоит,
// курсор свободен. E или Esc — закрыть.
public class FuelPumpMenu : MonoBehaviour
{
    public TMP_Text title;
    public TMP_Text status;
    public Transform list;                // сюда добавляются строки
    public Button rowTemplate;            // неактивный образец строки

    FuelPump pump;
    int openedFrame;
    Button fillRow, holdRow;
    Behaviour[] paused;

    public void Open(FuelPump owner)
    {
        pump = owner;
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

        if (fillRow == null)
        {
            fillRow = AddRow();
            fillRow.onClick.AddListener(() => status.text = $"Залито {pump.FillFull():0.#} л.");
            holdRow = AddRow();
        }
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
        if (k != null && Time.frameCount != openedFrame && (k.eKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame))
        {
            Close();
            return;
        }

        Mouse m = Mouse.current;
        if (holdRow.interactable && m != null && m.leftButton.isPressed
            && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)holdRow.transform, m.position.ReadValue()))
            pump.Flow(Time.deltaTime);
        Refresh();
    }

    void Refresh()
    {
        FuelTank tank = pump.Tank;
        title.text = $"Колонка · на счету {Wallet.Format(Wallet.Current.Balance)}";

        int cost = pump.FullCost;
        fillRow.interactable = !tank.IsFull && cost > 0;
        SetText(fillRow, tank.IsFull ? "Бак полный"
                       : cost <= 0 ? "Долить до полного — не хватает кредитов"
                       : cost >= Mathf.CeilToInt(tank.Free * pump.pricePerLiter) ? $"Долить до полного · {Wallet.Format(cost)}"
                       : $"Залить на все деньги · {Wallet.Format(cost)}");

        holdRow.interactable = pump.CanFlow;
        SetText(holdRow, $"Держать — лить ({pump.pricePerLiter} кр/л) · в баке {Mathf.CeilToInt(tank.Fuel)} / {tank.capacity:0} л");
    }

    Button AddRow()
    {
        Button row = Instantiate(rowTemplate, list);
        row.gameObject.SetActive(true);
        return row;
    }

    static void SetText(Button row, string text) => row.GetComponentInChildren<TMP_Text>().text = text;
}
