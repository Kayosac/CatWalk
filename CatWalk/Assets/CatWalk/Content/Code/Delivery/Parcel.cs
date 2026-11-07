using System.Collections.Generic;
using UnityEngine;

// Посылка — физическая коробка с заказом. Она всегда в одном месте: лежит свободно, в руках у кота или в слоте.
// В руках и в слоте коробка закреплена и ни с чем не сталкивается; свободная падает по гравитации (GravityBody).
// Пивот — в центре дна.
[RequireComponent(typeof(Rigidbody))]
public class Parcel : MonoBehaviour
{
    public static readonly List<Parcel> All = new();

    public DeliveryOrder order;

    public ParcelSlot Slot { get; private set; }

    Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

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
