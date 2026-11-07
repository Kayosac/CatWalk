using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Почтовый терминал. E рядом — меню почты: взять заказы и отметить доставку.
// Взятый заказ появляется посылкой в свободном слоте зоны выдачи. Заказов можно взять несколько,
// пока есть свободные слоты; один и тот же заказ — только пока его посылки нет в мире.
// Сдача: поставить посылку в зону приёма этой почты и отметить доставку в меню —
// закрываются все посылки в зоне приёма, адресованные сюда, и кошелёк получает их награды (хрупкие — по сохранности).
public class PostTerminal : MonoBehaviour
{
    public static readonly List<PostTerminal> All = new();

    // Доставка закрыта: почта и сданные посылки (ещё не уничтожены). Для жителей, сохранения, долга.
    public static event System.Action<PostTerminal, List<Parcel>> Delivered;

    public string address;                // id этой почты, на него ссылаются заказы (DeliveryOrder.to)
    public string title = "Почта";        // как почта называется в меню
    public DeliveryOrder[] orders;        // заказы, которые выдаёт эта почта
    public Parcel parcelPrefab;

    public Transform usePoint;            // где встаёт кот
    public float useDistance = 2.5f;      // м
    public ParcelSlot[] issueSlots;       // зона выдачи
    public ParcelSlot[] receiveSlots;     // зона приёма
    public PostTerminalMenu menu;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public static PostTerminal Find(string address) => All.Find(t => t.address == address);

    // Добавить заказ в список, если его там нет: поручения жителей и заказы вех.
    public void AddOrder(DeliveryOrder order)
    {
        if (order == null || (orders != null && System.Array.IndexOf(orders, order) >= 0)) return;
        System.Array.Resize(ref orders, (orders != null ? orders.Length : 0) + 1);
        orders[^1] = order;
    }

    public bool IsTaken(DeliveryOrder order) => Parcel.All.Exists(p => p.order == order);

    public bool HasFreeIssueSlot => System.Array.Exists(issueSlots, s => s.IsFree);

    public void Take(DeliveryOrder order)
    {
        ParcelSlot slot = System.Array.Find(issueSlots, s => s.IsFree);
        if (slot == null || IsTaken(order)) return;
        Parcel parcel = Instantiate(parcelPrefab);
        parcel.order = order;
        parcel.Attach(slot.transform, slot);
    }

    // Посылки в зоне приёма, адресованные сюда.
    public List<Parcel> Received()
    {
        var parcels = new List<Parcel>();
        foreach (ParcelSlot slot in receiveSlots)
            if (!slot.IsFree && slot.Parcel.order != null && slot.Parcel.order.to == address) parcels.Add(slot.Parcel);
        return parcels;
    }

    public static int Reward(List<Parcel> parcels)
    {
        int sum = 0;
        foreach (Parcel parcel in parcels) sum += parcel.Reward;
        return sum;
    }

    // Посылка уничтожается вместе с оплатой, поэтому одну коробку дважды не оплатить.
    public void Complete(List<Parcel> parcels)
    {
        Wallet.Current.Add(Reward(parcels));
        Delivered?.Invoke(this, parcels);
        foreach (Parcel parcel in parcels)
        {
            parcel.Slot.Parcel = null;
            parcel.gameObject.SetActive(false); // сразу пропадает из Parcel.All
            Destroy(parcel.gameObject);
        }
    }

    bool CatNear => ParcelCarrier.Current != null && ParcelCarrier.Current.isActiveAndEnabled
                 && Vector3.Distance(ParcelCarrier.Current.transform.position, usePoint.position) < useDistance;

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (MenuScreen.Opened != null || !CatNear) return;
        Hud.Prompt("E — почта", 10);
        if (k != null && k.eKey.wasPressedThisFrame) menu.Open(this);
    }

    void OnDrawGizmos()
    {
        if (usePoint == null) return;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(usePoint.position, useDistance);
    }
}
