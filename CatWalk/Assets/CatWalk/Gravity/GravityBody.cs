using UnityEngine;

// Тело, которое притягивается источниками гравитации: посылки, обломки, космокар.
// Встроенная гравитация Unity у него выключается.
[RequireComponent(typeof(Rigidbody))]
public class GravityBody : MonoBehaviour
{
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
    }

    void FixedUpdate() => rb.AddForce(GravitySource.At(rb.worldCenterOfMass), ForceMode.Acceleration);
}
