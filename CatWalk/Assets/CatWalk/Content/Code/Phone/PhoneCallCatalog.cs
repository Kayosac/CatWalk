using UnityEngine;

// Все звонки домой и имена говорящих.
[CreateAssetMenu(menuName = "CatWalk/Phone Call Catalog")]
public class PhoneCallCatalog : ScriptableObject
{
    [TextArea] public string note;        // для людей: что за тексты
    public string[] names = { "Я", "Мама", "Папа", "Автоответчик" }; // по PhoneCall.Speaker
    public int storyGap = 3;              // минут игры между сюжетными звонками
    public PhoneCall[] calls;
}
