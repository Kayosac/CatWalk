using UnityEngine;

// Числа долга: сумма, платежи, требования кредиторов и давление. Время — игровое, в секундах.
[CreateAssetMenu(menuName = "CatWalk/Debt Settings")]
public class DebtSettings : ScriptableObject
{
    public int total = 14000;             // долг на старте, кр

    [Header("Плановый платёж")]
    public int payment = 350;             // кр
    public float paymentInterval = 600f;  // с
    public float firstPayment = 1500f;    // срок первого платежа от начала игры, с; раньше него нет и требований

    [Header("Внеплановое требование")]
    public float demandEveryMin = 480f;   // как часто кредиторы решают, требовать ли, с
    public float demandEveryMax = 900f;
    [Range(0f, 1f)] public float demandChance = 0.3f;
    public int demandMin = 120;           // кр
    public int demandMax = 250;
    public float demandTime = 240f;       // срок, с

    [Header("Досрочное погашение")]
    public int extraPayment = 1000;       // кр за раз

    [Header("Давление 0..100")]
    public float missedPayment = 20f;     // просрочка планового
    public float missedDemand = 10f;      // просрочка требования
    public float paidOnTime = 5f;         // снимает платёж в срок
    public float extraRelief = 10f;       // снимает досрочное погашение
    public float mischiefPressure = 50f;  // с этого давления каждая просрочка — ещё и пакость
    [Range(0f, 1f)] public float fuelDrain = 0.25f; // пакость: доля топлива, которую сливают

    public float warnBefore = 60f;        // напоминание до срока, с
}
