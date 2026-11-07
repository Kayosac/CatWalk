using UnityEngine;

// Заказ на доставку: что везём, куда и сколько за это платят. Выдаёт его почта, в списке которой он лежит (PostTerminal.orders).
// Адрес — id почтового терминала назначения (PostTerminal.address).
[CreateAssetMenu(menuName = "CatWalk/Delivery Order")]
public class DeliveryOrder : ScriptableObject
{
    public string cargo = "Посылка";
    public string to;
    public int reward = 50;               // кредиты за доставку
}
