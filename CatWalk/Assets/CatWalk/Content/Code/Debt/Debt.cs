using System;
using UnityEngine;

// Долг кота кредиторам, один на сцену. Каждые paymentInterval игрового времени — плановый платёж,
// изредка — внеплановое требование со своим сроком (сверх долга, его не уменьшает). Платят в меню почты.
// Просрочки поднимают давление 0..100, платежи в срок и досрочное погашение — снижают.
// С mischiefPressure каждая просрочка ещё и пакость (Mischief): слитое топливо или помятый корпус.
// Давление 100 — финал «Окончательная гибель», долг 0 — «Освобождение». На паузе время долга стоит.
// В полётном HUD долга нет — только сообщения, меню почты и пауза.
public class Debt : MonoBehaviour, ISaveable
{
    public enum MischiefKind { Fuel, Hull }

    public static Debt Current { get; private set; }

    // Кредиторы напакостили. Топливо сливает сам долг; корпус — забота того, кто подпишется.
    public static event Action<MischiefKind> Mischief;
    public static event Action Missed;    // любая просрочка — для фактов истории

    public DebtSettings settings;
    public DebtEnding ending;

    [Serializable]
    class State
    {
        public int left;                  // остаток долга, кр
        public float pressure;
        public float paymentTimer;        // до срока планового, с
        public bool paid;                 // плановый этого срока внесён
        public float demandRoll;          // до следующего решения кредиторов, с
        public int demand;                // требование, кр; 0 — нет
        public float demandTimer;         // до срока требования, с
        public bool paymentWarned, demandWarned;
        public bool freed;                // финал «Освобождение» уже показан
    }

    State s;

    public int Left => s.left;
    public float Pressure => s.pressure;
    public bool Active => s.left > 0 && s.pressure < 100f;
    public int Payment => Mathf.Min(settings.payment, s.left);
    public bool Paid => s.paid;
    public float PaymentTimer => s.paymentTimer;
    public int Demand => s.demand;
    public float DemandTimer => s.demandTimer;
    public int ExtraPayment => Mathf.Min(settings.extraPayment, s.left);

    public string Summary => s.left <= 0 ? "долг выплачен" : $"долг {Wallet.Format(s.left)} | давление кредиторов {s.pressure:0}/100";

    public static string Clock(float seconds)
    {
        int t = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{t / 60}:{t % 60:00}";
    }

    void Awake()
    {
        Current = this;
        s = new State
        {
            left = settings.total,
            paymentTimer = settings.paymentInterval,
            demandRoll = NextRoll(),
        };
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    float NextRoll() => UnityEngine.Random.Range(settings.demandEveryMin, settings.demandEveryMax);

    void Update()
    {
        if (s.left <= 0)
        {
            if (!s.freed && ending != null) { s.freed = true; ending.Open(true); }
            return;
        }
        if (s.pressure >= 100f)
        {
            if (ending != null && MenuScreen.Opened != ending) ending.Open(false);
            return;
        }
        Tick(Time.deltaTime);
    }

    void Tick(float dt)
    {
        s.paymentTimer -= dt;
        if (!s.paid && !s.paymentWarned && s.paymentTimer <= settings.warnBefore)
        {
            s.paymentWarned = true;
            Hud.Toast($"Через минуту срок платежа кредиторам: {Wallet.Format(Payment)}. Платят на почте.");
        }
        if (s.paymentTimer <= 0f)
        {
            if (!s.paid) Miss(settings.missedPayment, "Платёж кредиторам просрочен.");
            s.paymentTimer += settings.paymentInterval;
            s.paid = s.paymentWarned = false;
        }

        if (s.demand > 0)
        {
            s.demandTimer -= dt;
            if (!s.demandWarned && s.demandTimer <= settings.warnBefore)
            {
                s.demandWarned = true;
                Hud.Toast($"Минута до срока требования кредиторов: {Wallet.Format(s.demand)}.");
            }
            if (s.demandTimer <= 0f)
            {
                s.demand = 0;
                Miss(settings.missedDemand, "Требование кредиторов просрочено.");
            }
        }
        else if ((s.demandRoll -= dt) <= 0f)
        {
            s.demandRoll = NextRoll();
            if (UnityEngine.Random.value < settings.demandChance)
            {
                s.demand = UnityEngine.Random.Range(settings.demandMin, settings.demandMax + 1);
                s.demandTimer = settings.demandTime;
                s.demandWarned = false;
                Hud.Toast($"Кредиторы требуют {Wallet.Format(s.demand)} за {Clock(s.demandTimer)}. Платят на почте.");
            }
        }
    }

    void Miss(float pressure, string message)
    {
        AddPressure(pressure);
        Missed?.Invoke();
        message += $" Давление: {s.pressure:0}/100.";
        if (s.pressure >= settings.mischiefPressure && s.pressure < 100f) message += " " + DoMischief();
        Hud.Toast(message);
    }

    // Корпус портится, только если кто-то за него отвечает (подписан на Mischief).
    string DoMischief()
    {
        MischiefKind kind = Mischief != null && UnityEngine.Random.value < 0.5f ? MischiefKind.Hull : MischiefKind.Fuel;
        if (kind == MischiefKind.Fuel)
        {
            FuelTank tank = FindFirstObjectByType<FuelTank>();
            if (tank != null) tank.TryUse(tank.Fuel * settings.fuelDrain);
        }
        Mischief?.Invoke(kind);
        return kind == MischiefKind.Fuel
            ? $"Кто-то слил {Mathf.RoundToInt(settings.fuelDrain * 100f)} % топлива. «В следующий раз — двигатель»."
            : "Кто-то помял корпус космокара. «Это только начало».";
    }

    // Сдвиг давления: просрочки, платежи, вехи, поручения, досмотры. Минус — снимает.
    public void AddPressure(float amount) => s.pressure = Mathf.Clamp(s.pressure + amount, 0f, 100f);

    // Оплата из меню почты: возвращают текст статуса или null, если не хватило денег.
    public string PayScheduled()
    {
        int amount = Payment;
        if (s.paid || !Wallet.Current.TrySpend(amount)) return null;
        s.left -= amount;
        s.paid = true;
        AddPressure(-settings.paidOnTime);
        return $"Платёж внесён. Остаток долга: {Wallet.Format(s.left)}.";
    }

    public string PayDemand()
    {
        if (s.demand <= 0 || !Wallet.Current.TrySpend(s.demand)) return null;
        s.demand = 0;
        AddPressure(-settings.paidOnTime);
        return "Требование оплачено. Кредиторы довольны — пока.";
    }

    public string PayExtra()
    {
        int amount = ExtraPayment;
        if (amount <= 0 || !Wallet.Current.TrySpend(amount)) return null;
        s.left -= amount;
        AddPressure(-settings.extraRelief);
        return $"Внесено {Wallet.Format(amount)}. Остаток долга: {Wallet.Format(s.left)}.";
    }

    public string SaveKey => "debt";
    public string Save() => JsonUtility.ToJson(s);

    public void Load(string data)
    {
        State loaded = JsonUtility.FromJson<State>(data);
        if (loaded != null) s = loaded;
    }
}
