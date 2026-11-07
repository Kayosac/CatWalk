using UnityEngine;

// Числа угрозы: когда кредиторы посылают охотника, как он гонится и что делает, если догнал.
// Время — игровое, в секундах; расстояния — в метрах, скорости — м/с.
[CreateAssetMenu(menuName = "CatWalk/Threat Settings")]
public class ThreatSettings : ScriptableObject
{
    [Header("Когда появляется")]
    public float firstDelay = 180f;       // после старта или загрузки — не раньше
    public float checkInterval = 20f;     // как часто кредиторы решают, послать ли охотника
    public float minPressure = 40f;       // давление долга, с которого охотник возможен
    [Range(0f, 1f)] public float chance = 0.1f;        // шанс за проверку на пороге давления
    [Range(0f, 1f)] public float chanceAtMax = 0.45f;  // при давлении 100
    public int minHeat = 3;               // или дурная слава (факт errand.heat) с этого значения
    [Range(0f, 1f)] public float heatChance = 0.04f;   // прибавка шанса за очко славы
    public float farFrom = 700f;          // от ближайшей станции, заправки или планеты — дальние маршруты
    public float spawnDistance = 1100f;   // появляется позади, вне видимости
    public float cooldown = 300f;         // передышка после погони

    [Header("Погоня")]
    public float speed = 90f;             // быстрее форсажа (~50), медленнее маршевого (~225)
    public float acceleration = 25f;      // м/с²
    public float catchRadius = 30f;       // держится ближе этого...
    public float catchTime = 4f;          // ...столько секунд — догнал
    public float loseDistance = 2600f;    // дальше — отстал
    public float hideSpeed = 8f;          // в облаке обломков медленнее этого — прячешься
    public float hideTime = 6f;           // столько в обломках — потерял цель
    public float waitTime = 12f;          // столько ждёт у атмосферы планеты или у заправки, потом уходит
    public float searchDistance = 150f;   // пока цель укрыта, ближе не подходит
    public float fuelStopZone = 100f;     // к заправкам ближе не подлетает (укрытие частичное)
    public float leaveTime = 10f;         // уходит, потом исчезает

    [Header("Если догнал")]
    public float hullDamage = 15f;        // %, хрупкий груз на полке тоже страдает (Hull)
    [Range(0f, 1f)] public float fuelDrain = 0.3f;     // доля топлива, которую сливают
    [Range(0f, 1f)] public float parcelChance = 0.35f; // шанс сбить хрупкую посылку с полки
    [Range(0f, 1f)] public float parcelLoss = 0.3f;    // сохранность, которую она теряет
    public float deadlyPressure = 85f;    // с этого давления поимка — финал «Окончательная гибель»
}
