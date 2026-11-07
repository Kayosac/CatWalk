using UnityEngine;
using UnityEngine.InputSystem;

// Кот носит одну посылку. F с пустыми руками — взять ближайшую (свободную или из слота).
// F с посылкой — поставить в ближайший свободный слот, а если рядом его нет — положить перед собой.
[RequireComponent(typeof(CatController))]
public class ParcelCarrier : MonoBehaviour
{
    public static ParcelCarrier Current { get; private set; }

    public Transform holdPoint;           // где коробка в руках, её дно
    public float reach = 2f;              // до посылки или слота от центра кота, м
    public float dropDistance = 1f;       // куда кладёт без слота — вперёд от ног, м

    public Parcel Held { get; private set; }

    Rigidbody rb;
    GUIStyle hintStyle;

    Vector3 Center => transform.position + transform.up;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Current = this;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null || !k.fKey.wasPressedThisFrame) return;

        if (Held == null)
        {
            Held = NearestParcel();
            if (Held != null) Held.Attach(holdPoint);
            return;
        }

        ParcelSlot slot = NearestFreeSlot();
        if (slot != null) Held.Attach(slot.transform, slot);
        else Held.Drop(transform.position + transform.forward * dropDistance + transform.up * 0.05f, transform.rotation, rb.linearVelocity);
        Held = null;
    }

    void OnGUI()
    {
        string hint = Held != null ? (NearestFreeSlot() != null ? "F — поставить посылку" : "F — положить посылку")
                    : NearestParcel() != null ? "F — взять посылку" : null;
        if (hint == null) return;
        hintStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.LowerCenter };
        GUI.Label(new Rect(0f, Screen.height - 64f, Screen.width, 30f), hint, hintStyle);
    }

    Parcel NearestParcel()
    {
        Parcel best = null;
        float bestDistance = reach;
        foreach (Parcel parcel in Parcel.All)
        {
            float d = Vector3.Distance(Center, parcel.transform.position);
            if (d < bestDistance) { best = parcel; bestDistance = d; }
        }
        return best;
    }

    ParcelSlot NearestFreeSlot()
    {
        ParcelSlot best = null;
        float bestDistance = reach;
        foreach (ParcelSlot slot in ParcelSlot.All)
        {
            if (!slot.IsFree) continue;
            float d = Vector3.Distance(Center, slot.transform.position);
            if (d < bestDistance) { best = slot; bestDistance = d; }
        }
        return best;
    }
}
