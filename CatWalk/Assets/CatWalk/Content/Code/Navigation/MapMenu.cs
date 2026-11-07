using UnityEngine;
using UnityEngine.InputSystem;

// Карта: все точки с типом и расстоянием от машины. Клик — сделать целью.
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
        FuelTank tank = navigator.GetComponent<FuelTank>();
        title.text = $"Карта · топливо {Mathf.CeilToInt(tank.Fuel)} / {tank.capacity:0} л";

        foreach (Location location in navigator.Sorted())
        {
            float distance = navigator.DistanceTo(location);
            string line = $"{location.Label} · {KindName(location.kind)} · {Navigator.FormatDistance(distance)}";
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
