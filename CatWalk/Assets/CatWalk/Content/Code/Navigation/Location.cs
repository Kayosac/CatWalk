using System.Collections.Generic;
using UnityEngine;

// Точка на карте: станция, заправка, поселение на планете, находка в пустоте.
// По ней работают карта, выбор цели и маршевый перелёт. id постоянный — на него ссылаются сохранения.
public class Location : MonoBehaviour
{
    public enum Kind { Station, FuelStop, Planet, Find }

    public static readonly List<Location> All = new();

    public string id;
    public string title;
    public Kind kind;
    public float radius = 30f;            // размер объекта: маршевый прилетает на CruiseDrive.arrivalDistance от его края, м
    public Transform arrival;             // точка прибытия маршевого; пусто — на подлёте со стороны машины

    // Имя на карте и в HUD: title, а если пусто — id или имя объекта.
    public string Label => !string.IsNullOrEmpty(title) ? title : !string.IsNullOrEmpty(id) ? id : name;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public static Location Find(string id) => string.IsNullOrEmpty(id) ? null : All.Find(l => l.id == id);
}
