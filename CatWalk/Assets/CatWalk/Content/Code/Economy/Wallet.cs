using System;
using UnityEngine;

// Кошелёк кота: баланс в кредитах, один на сцену. Живёт отдельно от кота — тот пропадает, когда садится в космокар.
// Пока деньги только приходят: оплата за доставку (PostTerminal.Complete).
public class Wallet : MonoBehaviour
{
    public static Wallet Current { get; private set; }

    public int startBalance = 100;

    public int Balance { get; private set; }

    public event Action<int> Added;       // сколько пришло; Balance уже обновлён

    public static string Format(int credits) => $"{credits} кр";

    void Awake()
    {
        Current = this;
        Balance = startBalance;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Balance += amount;
        Added?.Invoke(amount);
    }
}
