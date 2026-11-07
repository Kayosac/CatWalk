using UnityEngine;
using UnityEngine.InputSystem;

// Буксир. Когда машина застряла (бак пуст или корпус разбит), H — вызвать: её переносят на ближайшую свободную парковку,
// доливают топливо и латают корпус до минимума. Стоит maxCost, а если денег меньше — все деньги (без денег — даром).
// Call() работает и не в тупике — для меню паузы.
[RequireComponent(typeof(CosmoCarController), typeof(Hull))]
public class Tow : MonoBehaviour
{
    public int maxCost = 120;             // кр
    public float minFuel = 35f;           // л
    public float minHull = 60f;           // %
    public Vector3 clearance = new(1.2f, 0.7f, 2.3f); // полуразмеры машины: парковка занята, если в них что-то есть, м

    CosmoCarController car;
    FuelTank tank;
    Hull hull;
    Rigidbody rb;

    public bool Stuck => tank.Fuel <= 0f || hull.IsBroken;
    public int Cost => Mathf.Min(maxCost, Wallet.Current.Balance);

    void Awake()
    {
        car = GetComponent<CosmoCarController>();
        tank = GetComponent<FuelTank>();
        hull = GetComponent<Hull>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Только пока кот за рулём.
        if (!car.enabled || !Stuck || MenuScreen.Opened != null) return;
        Hud.Prompt($"H — вызвать буксир · {Wallet.Format(Cost)}", 10);
        Keyboard k = Keyboard.current;
        if (k != null && k.hKey.wasPressedThisFrame) Call();
    }

    // Вызвать буксир (H в тупике, меню паузы).
    public void Call()
    {
        ParkingZone zone = NearestParking();
        if (zone == null)
        {
            Hud.Toast("Буксиру некуда везти: рядом нет парковок.");
            return;
        }

        int cost = Cost;
        if (cost > 0) Wallet.Current.TrySpend(cost);
        Transform pad = zone.transform;
        car.Teleport(pad.position + pad.up * zone.dockHeight, Quaternion.LookRotation(pad.forward, pad.up));
        if (tank.Fuel < minFuel) tank.Set(minFuel);
        if (hull.Condition < minHull) hull.Repair(minHull - hull.Condition);

        Location place = NearestLocation(pad.position);
        Hud.Toast($"Буксир доставил космокар {(place != null ? $"к «{place.title}»" : "на парковку")} · "
                + (cost > 0 ? $"−{Wallet.Format(cost)}" : "даром"));
    }

    // Ближайшая парковка, на месте стоянки которой ничего нет; если заняты все — просто ближайшая.
    ParkingZone NearestParking()
    {
        ParkingZone best = null;
        bool bestFree = false;
        float bestDistance = float.MaxValue;
        foreach (ParkingZone zone in FindObjectsByType<ParkingZone>(FindObjectsSortMode.None))
        {
            bool free = IsFree(zone);
            float d = Vector3.Distance(transform.position, zone.transform.position);
            if ((free && !bestFree) || (free == bestFree && d < bestDistance))
            {
                best = zone;
                bestFree = free;
                bestDistance = d;
            }
        }
        return best;
    }

    bool IsFree(ParkingZone zone)
    {
        Transform pad = zone.transform;
        foreach (Collider c in Physics.OverlapBox(pad.position + pad.up * zone.dockHeight, clearance, pad.rotation, ~0, QueryTriggerInteraction.Ignore))
            if (c.attachedRigidbody != rb) return false;
        return true;
    }

    static Location NearestLocation(Vector3 position)
    {
        Location best = null;
        float bestDistance = float.MaxValue;
        foreach (Location location in Location.All)
        {
            float d = Vector3.Distance(position, location.transform.position);
            if (d < bestDistance)
            {
                best = location;
                bestDistance = d;
            }
        }
        return best;
    }
}
