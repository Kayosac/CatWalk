using UnityEngine;

// Числа долга: сумма, платежи, требования кредиторов и давление. Время — игровое, в секундах.
[CreateAssetMenu(menuName = "CatWalk/Debt Settings")]
public class DebtSettings : ScriptableObject
{
    public int total = 15000;             // долг на старте, кр

    [Header("Плановый платёж")]
    public int payment = 400;             // кр
    public float paymentInterval = 480f;  // с

    [Header("Внеплановое требование")]
    public float demandEveryMin = 360f;   // как часто кредиторы решают, требовать ли, с
    public float demandEveryMax = 720f;
    [Range(0f, 1f)] public float demandChance = 0.35f;
    public int demandMin = 150;           // кр
    public int demandMax = 350;
    public float demandTime = 180f;       // срок, с

    [Header("Досрочное погашение")]
    public int extraPayment = 1000;       // кр за раз

    [Header("Давление 0..100")]
    public float missedPayment = 25f;     // просрочка планового
    public float missedDemand = 15f;      // просрочка требования
    public float paidOnTime = 5f;         // снимает платёж в срок
    public float extraRelief = 10f;       // снимает досрочное погашение
    public float mischiefPressure = 50f;  // с этого давления каждая просрочка — ещё и пакость
    [Range(0f, 1f)] public float fuelDrain = 0.25f; // пакость: доля топлива, которую сливают

    public float warnBefore = 60f;        // напоминание до срока, с
}
