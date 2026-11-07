using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Экранное меню колонки: долить до полного или держать кнопку мышью (или A геймпада на выделенной строке), пока льётся.
public class FuelPumpMenu : MenuScreen
{
    FuelPump pump;
    Button fillRow, holdRow;

    public void Open(FuelPump owner)
    {
        pump = owner;
        if (!Show()) return;
        if (fillRow == null)
        {
            fillRow = AddRow("", () => $"Залито {pump.FillFull():0.#} л.");
            holdRow = AddRow("");
        }
        Refresh();
    }

    protected override void Update()
    {
        base.Update();
        if (Opened != this) return;

        Mouse m = Mouse.current;
        bool mouse = m != null && m.leftButton.isPressed
                  && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)holdRow.transform, m.position.ReadValue());
        bool pad = Controls.Interact.IsPressed() && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == holdRow.gameObject;
        if (holdRow.interactable && (mouse || pad)) pump.Flow(Time.deltaTime);
        Refresh();
    }

    protected override void Refresh()
    {
        FuelTank tank = pump.Tank;
        title.text = $"Колонка | на счету {Wallet.Format(Wallet.Current.Balance)}";

        int cost = pump.FullCost;
        fillRow.interactable = !tank.IsFull && cost > 0;
        SetText(fillRow, tank.IsFull ? "Бак полный"
                       : cost <= 0 ? "Долить до полного — не хватает кредитов"
                       : cost >= Mathf.CeilToInt(tank.Free * pump.pricePerLiter) ? $"Долить до полного | {Wallet.Format(cost)}"
                       : $"Залить на все деньги | {Wallet.Format(cost)}");

        holdRow.interactable = pump.CanFlow;
        SetText(holdRow, $"Держать — лить ({pump.pricePerLiter} кр/л) | в баке {Mathf.CeilToInt(tank.Fuel)} / {tank.capacity:0} л");
    }
}
