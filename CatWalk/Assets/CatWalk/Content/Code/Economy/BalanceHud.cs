using TMPro;
using UnityEngine;

// Баланс справа сверху. При доходе под ним ненадолго появляется «+N кр».
public class BalanceHud : MonoBehaviour
{
    public Wallet wallet;
    public TMP_Text balance;
    public TMP_Text income;
    public float incomeTime = 2.5f;       // сколько висит «+N кр», с

    float hideAt;

    void Awake() => wallet.Changed += OnChanged;
    void OnDestroy() => wallet.Changed -= OnChanged;

    void Start()
    {
        balance.text = Wallet.Format(wallet.Balance);
        income.gameObject.SetActive(false);
    }

    void Update()
    {
        if (income.gameObject.activeSelf && Time.time >= hideAt) income.gameObject.SetActive(false);
    }

    void OnChanged(int amount)
    {
        balance.text = Wallet.Format(wallet.Balance);
        if (amount <= 0) return;
        income.text = "+" + Wallet.Format(amount);
        income.gameObject.SetActive(true);
        hideAt = Time.time + incomeTime;
    }
}
