using UnityEngine;
using UnityEngine.InputSystem;

// Выход из космокара и посадка обратно.
// E на парковке — выйти: слева от машины появляется игрок, камера переключается на него.
// E рядом с машиной — сесть обратно.
[RequireComponent(typeof(CosmoCarController))]
public class CosmoCarSeat : MonoBehaviour
{
    public GameObject playerPrefab;       // CatPlayer
    public float exitSide = 2.6f;         // насколько левее центра машины появляется игрок, м
    public float enterDistance = 3.5f;    // с какого расстояния можно сесть, м

    CosmoCarController car;
    CosmoCarCamera carCamera;
    ParkingZone[] zones;
    GameObject player;
    Transform playerBody;
    GUIStyle hintStyle;

    void Start()
    {
        car = GetComponent<CosmoCarController>();
        carCamera = FindFirstObjectByType<CosmoCarCamera>();
        zones = FindObjectsByType<ParkingZone>(FindObjectsSortMode.None);
    }

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null || !k.eKey.wasPressedThisFrame) return;

        if (player == null) TryExit();
        else if (CanEnter()) Enter();
    }

    // С посылкой в руках не садимся: её сначала ставят на полку или кладут.
    bool CanEnter() => player != null && Vector3.Distance(playerBody.position, transform.position) < enterDistance
                    && playerBody.GetComponent<ParcelCarrier>().Held == null;

    // Подсказка внизу экрана, когда можно выйти или сесть.
    void OnGUI()
    {
        string hint = player == null ? (DockedZone() != null ? "E — выйти" : null) : (CanEnter() ? "E — сесть" : null);
        if (hint == null) return;
        hintStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.LowerCenter };
        GUI.Label(new Rect(0f, Screen.height - 40f, Screen.width, 30f), hint, hintStyle);
    }

    void TryExit()
    {
        ParkingZone zone = DockedZone();
        if (zone == null || playerPrefab == null) return;

        // Точка слева от машины на поверхности площадки, не дальше её края.
        Vector3 up = zone.transform.up;
        Vector3 offset = transform.position - transform.right * exitSide - zone.transform.position;
        Transform pad = zone.transform;
        float halfX = zone.padSize.x * 0.5f - 0.6f, halfZ = zone.padSize.y * 0.5f - 0.6f;
        Vector3 lateral = pad.right * Mathf.Clamp(Vector3.Dot(offset, pad.right), -halfX, halfX)
                        + pad.forward * Mathf.Clamp(Vector3.Dot(offset, pad.forward), -halfZ, halfZ);
        Vector3 spot = zone.transform.position + lateral + up * 0.05f;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);

        player = Instantiate(playerPrefab, spot, Quaternion.LookRotation(forward, up));
        playerBody = player.GetComponentInChildren<CatController>().transform;
        SetDriving(false);
    }

    void Enter()
    {
        Destroy(player);
        player = null;
        SetDriving(true);
    }

    void SetDriving(bool driving)
    {
        car.enabled = driving;
        if (carCamera != null) carCamera.gameObject.SetActive(driving);
    }

    // Парковка, на которой стоит машина. Кот ходит по гравитации, так что подходит площадка любой ориентации.
    ParkingZone DockedZone()
    {
        foreach (ParkingZone zone in zones)
            if (zone.IsDocked) return zone;
        return null;
    }
}
