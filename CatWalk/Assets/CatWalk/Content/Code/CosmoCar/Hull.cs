using System;
using System.Globalization;
using UnityEngine;

// Корпус космокара: исправность 0..100 %. Удар сильнее порога снимает проценты пропорционально превышению —
// лёгкие касания и посадка на парковку проходят даром. Ниже weakAt тяга плавно слабеет до weakThrust,
// на нуле двигатели молчат, пока машину не починят в мастерской или не отбуксируют.
[RequireComponent(typeof(CosmoCarController))]
public class Hull : MonoBehaviour, ISaveable
{
    // Удар по корпусу: какой корпус и сколько процентов снято. Хрупкие посылки на полке теряют от него сохранность.
    public static event Action<Hull, float> Hit;

    public float safeImpact = 4f;         // удар до этого (изменение скорости, м/с) — без урона
    public float damagePerImpact = 3f;    // % за каждый м/с сверх порога
    public float mischiefDamage = 20f;    // пакость кредиторов, %
    public float weakAt = 50f;            // ниже этой исправности тяга слабеет, %
    [Range(0f, 1f)] public float weakThrust = 0.4f; // доля тяги у самого нуля

    public float Condition { get; private set; } = 100f;
    public bool IsBroken => Condition <= 0f;

    CosmoCarController car;
    Rigidbody rb;

    void Awake()
    {
        car = GetComponent<CosmoCarController>();
        rb = GetComponent<Rigidbody>();
    }

    // Кредиторы при высоком давлении иногда мнут машину (Debt).
    void OnEnable() => Debt.Mischief += OnMischief;
    void OnDisable() => Debt.Mischief -= OnMischief;

    void OnMischief(Debt.MischiefKind kind)
    {
        if (kind == Debt.MischiefKind.Hull) Damage(mischiefDamage);
    }

    void Update() =>
        car.thrustScale = IsBroken ? 0f : Mathf.Lerp(weakThrust, 1f, Condition / weakAt);

    void OnCollisionEnter(Collision collision) => Damage((collision.impulse.magnitude / rb.mass - safeImpact) * damagePerImpact);

    // Снять проценты — как от удара: хрупкий груз на полке тоже пострадает.
    public void Damage(float percent)
    {
        percent = Mathf.Min(percent, Condition);
        if (percent <= 0f) return;
        Condition -= percent;
        Hit?.Invoke(this, percent);
    }

    public void Repair(float percent) => Condition = Mathf.Clamp(Condition + percent, 0f, 100f);

    public string SaveKey => "hull";
    public string Save() => Condition.ToString(CultureInfo.InvariantCulture);
    public void Load(string data) => Condition = Mathf.Clamp(float.Parse(data, CultureInfo.InvariantCulture), 0f, 100f);
}
