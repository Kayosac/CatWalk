using System;
using UnityEngine;

// Кошелёк кота: баланс в кредитах, один на сцену. Живёт отдельно от кота — тот пропадает, когда садится в космокар.
// Деньги приходят за доставку (PostTerminal.Complete) и уходят на топливо (FuelPump).
public class Wallet : MonoBehaviour
{
    public static Wallet Current { get; private set; }

    public int startBalance = 100;

    public int Balance { get; private set; }

    public event Action<int> Changed;     // на сколько изменился: + доход, − трата; Balance уже обновлён

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
        Changed?.Invoke(amount);
    }

    // Восстановить из сохранения.
    public void Set(int balance)
    {
        int delta = balance - Balance;
        Balance = balance;
        Changed?.Invoke(delta);
    }

    // Списать, только если хватает.
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || amount > Balance) return false;
        Balance -= amount;
        Changed?.Invoke(-amount);
        return true;
    }
}
