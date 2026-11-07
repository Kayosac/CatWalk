using UnityEngine;

// Бак космокара. Топливо тратится только на тягу, пропорционально ей: форсаж жжёт сильнее, дрейф бесплатный.
// Последние reserve бака — резерв: тяга слабая, без форсажа. Пустой бак — двигатели заглохли.
public class FuelTank : MonoBehaviour
{
    public float capacity = 100f;         // л
    public float burnRate = 1f / 3f;      // л/с на полном газу без форсажа — полного бака хватает на 5 минут
    [Range(0f, 1f)] public float reserve = 0.05f;      // доля бака
    [Range(0f, 1f)] public float reservePower = 0.3f;  // тяга на резерве

    public float Fuel { get; private set; }
    public float Free => capacity - Fuel;
    public bool IsFull => Free < 0.05f;
    public bool OnReserve => Fuel <= capacity * reserve;
    public float Power => Fuel <= 0f ? 0f : OnReserve ? reservePower : 1f;

    void Awake() => Fuel = capacity;

    // throttle — тяга в долях полного газа без форсажа.
    public void Burn(float throttle, float dt) => Fuel = Mathf.Max(0f, Fuel - burnRate * throttle * dt);

    public void Fill(float liters) => Fuel = Mathf.Min(capacity, Fuel + liters);
}
