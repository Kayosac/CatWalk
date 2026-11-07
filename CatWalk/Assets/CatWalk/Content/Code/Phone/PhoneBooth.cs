using UnityEngine;
using UnityEngine.InputSystem;

// Телефонная будка. E рядом — меню: сохранить игру, позвонить домой, «Записи».
// Сохраниться можно только здесь. Работает пешком, где бы будка ни стояла.
// Если ждёт сюжетный звонок (PhoneLine.News) — над будкой «!» и подсказка о звонке.
public class PhoneBooth : MonoBehaviour
{
    public Transform usePoint;            // где встаёт кот
    public float useDistance = 1.5f;      // м
    public PhoneBoothMenu menu;
    public GameObject newsIcon;           // «!» — есть о чём поговорить с домом

    // Название места в меню: Location в родителях, иначе «будка».
    public string Place
    {
        get
        {
            Location location = GetComponentInParent<Location>();
            return location != null ? location.Label : "будка";
        }
    }

    bool CatNear => ParcelCarrier.Current != null && ParcelCarrier.Current.isActiveAndEnabled
                 && Vector3.Distance(ParcelCarrier.Current.transform.position, usePoint.position) < useDistance;

    void Update()
    {
        Keyboard k = Keyboard.current;
        bool news = PhoneLine.Current != null && PhoneLine.Current.News != null;
        if (newsIcon != null) newsIcon.SetActive(news);
        if (MenuScreen.Opened != null || !CatNear) return;
        Hud.Prompt(news ? "E — телефон (звонок домой)" : "E — телефон", 10);
        if (k != null && k.eKey.wasPressedThisFrame) menu.Open(this);
    }

    void OnDrawGizmos()
    {
        if (usePoint == null) return;
        Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(usePoint.position, useDistance);
    }
}
