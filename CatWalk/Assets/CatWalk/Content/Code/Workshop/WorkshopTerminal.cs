using UnityEngine;
using UnityEngine.InputSystem;

// Мастерская. E рядом — меню: ремонт корпуса и улучшения космокара.
// Чинит и улучшает космокар, где бы тот ни стоял. Ремонт оплачивается за каждый процент — на сколько хватает денег.
public class WorkshopTerminal : MonoBehaviour
{
    public int pricePerPercent = 2;       // кр
    public Transform usePoint;            // где встаёт кот
    public float useDistance = 2.5f;      // м
    public WorkshopMenu menu;

    public Hull Hull { get; private set; }
    public CarUpgrades Upgrades { get; private set; }

    void Start()
    {
        Hull = FindFirstObjectByType<Hull>();
        Upgrades = FindFirstObjectByType<CarUpgrades>();
    }

    // Сколько процентов не хватает до целого корпуса.
    public int Missing => Mathf.CeilToInt(100f - Hull.Condition);

    // Сколько процентов починит «Починить полностью»: все, а если денег меньше — на все деньги.
    public int RepairPercents => Mathf.Min(Missing, Wallet.Current.Balance / pricePerPercent);

    // Возвращает, на сколько процентов починили.
    public int Repair()
    {
        int percents = RepairPercents;
        if (percents <= 0 || !Wallet.Current.TrySpend(percents * pricePerPercent)) return 0;
        Hull.Repair(percents);
        return percents;
    }

    bool CatNear => ParcelCarrier.Current != null && ParcelCarrier.Current.isActiveAndEnabled
                 && Vector3.Distance(ParcelCarrier.Current.transform.position, usePoint.position) < useDistance;

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (MenuScreen.Opened != null || !CatNear) return;
        Hud.Prompt("E — мастерская", 10);
        if (k != null && k.eKey.wasPressedThisFrame) menu.Open(this);
    }

    void OnDrawGizmos()
    {
        if (usePoint == null) return;
        Gizmos.color = new Color(0.6f, 1f, 0.5f, 0.6f);
        Gizmos.DrawWireSphere(usePoint.position, useDistance);
    }
}
