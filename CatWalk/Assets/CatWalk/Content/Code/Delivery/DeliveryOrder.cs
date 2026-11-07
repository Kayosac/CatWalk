using UnityEngine;

// Заказ на доставку: что везём, куда и сколько за это платят. Выдаёт его почта, в списке которой он лежит (PostTerminal.orders).
// Адрес — id почтового терминала назначения (PostTerminal.address).
// Хрупкий груз теряет сохранность от ударов, и оплата падает вместе с ней (Parcel.Reward).
// Заказ с условиями почта показывает, только пока они выполнены (вехи, поручения жителей).
[CreateAssetMenu(menuName = "CatWalk/Delivery Order")]
public class DeliveryOrder : ScriptableObject
{
    public string cargo = "Посылка";
    public string to;
    public int reward = 50;               // кредиты за доставку
    public bool fragile;
    public FactCondition[] requires;      // пусто — заказ виден всегда
    [TextArea] public string reaction;    // что скажет житель почты-адресата при сдаче; пусто — его обычная реакция
}
