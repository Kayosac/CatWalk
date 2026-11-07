using UnityEngine;
using UnityEngine.InputSystem;

// Топливная колонка. E рядом — меню: долить до полного или держать кнопку и лить, сколько нужно.
// Льёт в бак космокара, где бы тот ни стоял. Каждый литр оплачивается из кошелька — на сколько хватает денег.
public class FuelPump : MonoBehaviour
{
    public int pricePerLiter = 1;         // кр
    public float flowRate = 10f;          // л/с, пока держат кнопку
    public float useDistance = 2.3f;      // от центра колонки до кота, м
    public FuelPumpMenu menu;

    public FuelTank Tank { get; private set; }

    float flow;                           // налито кнопкой, но ещё не залито — копится до целого литра
    GUIStyle hintStyle;

    void Start() => Tank = FindFirstObjectByType<FuelTank>();

    // Сколько стоит долить до полного; если денег меньше — на все деньги, целыми литрами.
    public int FullCost => Mathf.Min(Mathf.CeilToInt(Tank.Free * pricePerLiter), Wallet.Current.Balance / pricePerLiter * pricePerLiter);

    // Возвращает, сколько литров залито.
    public float FillFull()
    {
        int cost = FullCost;
        if (Tank.IsFull || cost <= 0 || !Wallet.Current.TrySpend(cost)) return 0f;
        float liters = Mathf.Min(Tank.Free, (float)cost / pricePerLiter);
        Tank.Fill(liters);
        return liters;
    }

    public bool CanFlow => !Tank.IsFull && Wallet.Current.Balance >= pricePerLiter;

    // Пока держат кнопку: литр за литром, каждый оплачивается сразу.
    public void Flow(float dt)
    {
        flow += flowRate * dt;
        while (flow >= 1f && CanFlow && Wallet.Current.TrySpend(pricePerLiter))
        {
            Tank.Fill(1f);
            flow -= 1f;
        }
        if (!CanFlow) flow = 0f;
    }

    bool CatNear => ParcelCarrier.Current != null && ParcelCarrier.Current.isActiveAndEnabled
                 && Vector3.Distance(ParcelCarrier.Current.transform.position, transform.position) < useDistance;

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k != null && k.eKey.wasPressedThisFrame && !menu.gameObject.activeSelf && CatNear) menu.Open(this);
    }

    void OnGUI()
    {
        if (menu.gameObject.activeSelf || !CatNear) return;
        hintStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.LowerCenter };
        GUI.Label(new Rect(0f, Screen.height - 40f, Screen.width, 30f), "E — колонка", hintStyle);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, useDistance);
    }
}
