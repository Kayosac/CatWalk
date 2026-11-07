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

    void Awake() => wallet.Added += OnAdded;
    void OnDestroy() => wallet.Added -= OnAdded;

    void Start()
    {
        balance.text = Wallet.Format(wallet.Balance);
        income.gameObject.SetActive(false);
    }

    void Update()
    {
        if (income.gameObject.activeSelf && Time.time >= hideAt) income.gameObject.SetActive(false);
    }

    void OnAdded(int amount)
    {
        balance.text = Wallet.Format(wallet.Balance);
        income.text = "+" + Wallet.Format(amount);
        income.gameObject.SetActive(true);
        hideAt = Time.time + incomeTime;
    }
}
