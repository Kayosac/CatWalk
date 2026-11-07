using System.Collections.Generic;
using UnityEngine;

// Место под одну посылку: зона выдачи или приёма у почты, полка космокара.
// Посылка встаёт на точку слота дном, по его осям.
public class ParcelSlot : MonoBehaviour
{
    public static readonly List<ParcelSlot> All = new();

    public Parcel Parcel { get; set; }    // уничтоженная посылка тоже считается пустотой
    public bool IsFree => Parcel == null;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * 0.25f, new Vector3(0.65f, 0.5f, 0.46f));
    }
}
