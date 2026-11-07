using System.Collections.Generic;
using UnityEngine;

// Посылка — физическая коробка с заказом. Она всегда в одном месте: лежит свободно, в руках у кота или в слоте.
// В руках и в слоте коробка закреплена и ни с чем не сталкивается; свободная падает по гравитации (GravityBody).
// Пивот — в центре дна.
// Хрупкая посылка теряет сохранность: свободная — от собственных ударов, на полке космокара — от ударов по корпусу (Hull).
[RequireComponent(typeof(Rigidbody))]
public class Parcel : MonoBehaviour
{
    public static readonly List<Parcel> All = new();

    public DeliveryOrder order;
    [Range(0f, 1f)] public float condition = 1f; // сохранность хрупкого груза
    public float safeImpact = 4f;         // свободная коробка: удар до этого (изменение скорости, м/с) — без вреда
    public float lossPerImpact = 0.1f;    // сохранности за каждый м/с сверх порога
    public float hullShock = 2.5f;        // на полке: сохранности за 100 % урона корпуса

    public ParcelSlot Slot { get; private set; }

    public bool Fragile => order != null && order.fragile;

    // Оплата за доставку: хрупкое — до 55 % награды при полностью разбитом грузе.
    public int Reward => Fragile ? Mathf.RoundToInt(order.reward * Mathf.Lerp(0.55f, 1f, condition)) : order.reward;

    // Груз для подсказок и меню: у хрупкого — сохранность.
    public string Label => order == null ? "Посылка" : Fragile ? $"{order.cargo} (хрупкое, сохранность {Percent(condition)})" : order.cargo;

    public static string Percent(float share) => $"{Mathf.RoundToInt(share * 100f)} %";

    Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();

    void OnEnable()
    {
        All.Add(this);
        Hull.Hit += OnHullHit;
    }

    void OnDisable()
    {
        All.Remove(this);
        Hull.Hit -= OnHullHit;
    }

    void OnHullHit(Hull hull, float damage)
    {
        if (Slot != null && Slot.transform.IsChildOf(hull.transform)) Damage(damage / 100f * hullShock);
    }

    // Пока коробка закреплена, столкновений нет — сюда попадают только удары свободной.
    void OnCollisionEnter(Collision collision) => Damage((collision.impulse.magnitude / rb.mass - safeImpact) * lossPerImpact);

    void Damage(float loss)
    {
        if (Fragile && loss > 0f) condition = Mathf.Max(0f, condition - loss);
    }

    // Закрепить на точке дном: в руках (slot = null) или в слоте.
    public void Attach(Transform point, ParcelSlot slot = null)
    {
        Leave();
        Slot = slot;
        if (slot != null) slot.Parcel = this;
        rb.isKinematic = true;
        rb.detectCollisions = false;
        rb.interpolation = RigidbodyInterpolation.None;
        transform.SetParent(point, false);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    // Отпустить свободной коробкой с этой позы.
    public void Drop(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        Leave();
        transform.SetParent(null);
        transform.SetPositionAndRotation(position, rotation);
        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearVelocity = velocity;
    }

    void Leave()
    {
        if (Slot != null) Slot.Parcel = null;
        Slot = null;
    }
}
