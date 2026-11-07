using UnityEngine;
using UnityEngine.InputSystem;

// Карта: все точки с типом, расстоянием от машины и тем, долетит ли маршевый. Клик — сделать целью.
// Открывается по Tab в машине и пешком, время на паузе. Tab или Esc — закрыть.
public class MapMenu : MenuScreen
{
    Navigator navigator;
    int openedFrame;

    public void Open(Navigator owner)
    {
        navigator = owner;
        if (!Show()) return;
        openedFrame = Time.frameCount;
        Refresh();
    }

    protected override void Update()
    {
        base.Update();
        Keyboard k = Keyboard.current;
        if (Opened == this && k != null && k.tabKey.wasPressedThisFrame && Time.frameCount != openedFrame) Close();
    }

    protected override void Refresh()
    {
        ClearRows();
        CruiseDrive cruise = navigator.GetComponent<CruiseDrive>();
        FuelTank tank = navigator.GetComponent<FuelTank>();
        title.text = $"Карта · маршевый до {Navigator.FormatDistance(cruise.Range)} · топливо {Mathf.CeilToInt(tank.Fuel)} / {tank.capacity:0} л";

        foreach (Location location in navigator.Sorted())
        {
            float distance = navigator.DistanceTo(location);
            string reach = distance <= cruise.minDistance ? "рядом"
                         : distance > cruise.Range ? "далеко для двигателя"
                         : cruise.Cost(distance) > tank.Fuel ? $"мало топлива ({cruise.Cost(distance):0} л)"
                         : "можно маршевым";
            string line = $"{location.Label} · {KindName(location.kind)} · {Navigator.FormatDistance(distance)} · {reach}";
            if (location == navigator.Target) line = "[цель] " + line;
            Location picked = location;
            AddRow(line, () =>
            {
                navigator.Target = picked;
                return $"Цель: {picked.Label}";
            });
        }
        if (Location.All.Count == 0) AddRow("На карте нет ни одной точки");
    }

    static string KindName(Location.Kind kind) => kind switch
    {
        Location.Kind.Station => "станция",
        Location.Kind.FuelStop => "заправка",
        Location.Kind.Planet => "планета",
        _ => "находка",
    };
}
