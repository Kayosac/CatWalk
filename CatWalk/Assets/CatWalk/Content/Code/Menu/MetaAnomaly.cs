using UnityEngine;

// Сбой в пустоте — вход в мета-финал «Обрыв контакта». Пока условия MetaEndingSettings не выполнены, его нет;
// потом он появляется (с Location на visual — и на карте), дёргается и мерцает.
// Кот или машина ближе settings.radius — начинается финал (MetaEndingScreen).
//
// Факты: meta.anomaly — сбой появился; meta.contact — финал начался (в сохранение уже не попадёт: игра закроется).
// Вне сохранения: PlayerPrefs «meta.contact_lost» — сколько раз финал доходил до «падения» (MetaEndingScreen).
public class MetaAnomaly : MonoBehaviour
{
    public MetaEndingSettings settings;
    public GameObject visual;             // куски сбоя и Location; выключен, пока условия не выполнены
    public MetaEndingScreen screen;

    CosmoCarSeat seat;
    Renderer[] pieces;
    Vector3[] home;
    float poll;

    void Start()
    {
        seat = FindFirstObjectByType<CosmoCarSeat>();
        pieces = visual.GetComponentsInChildren<Renderer>(true);
        home = new Vector3[pieces.Length];
        for (int i = 0; i < pieces.Length; i++) home[i] = pieces[i].transform.localPosition;
        visual.SetActive(false);
    }

    void Update()
    {
        if (settings == null || screen == null) return;
        if (!visual.activeSelf)
        {
            if ((poll -= Time.unscaledDeltaTime) > 0f) return;
            poll = 0.5f;
            if (!settings.Ready) return;
            visual.SetActive(true);
            StoryFacts.Set("meta.anomaly", 1);
        }

        if (MenuScreen.Opened == screen) return; // финал идёт: сбой замирает и распадается вместе с миром

        // Куски то и дело перескакивают, поворачиваются и пропадают на кадр.
        for (int i = 0; i < pieces.Length; i++)
        {
            if (Random.value > 0.08f) continue;
            Transform t = pieces[i].transform;
            t.localPosition = home[i] + Random.insideUnitSphere * 4f;
            t.localRotation = Random.rotation;
            pieces[i].enabled = Random.value > 0.2f;
        }

        if (seat == null || MenuScreen.Opened != null) return;
        Vector3 hero = seat.PlayerBody != null ? seat.PlayerBody.position : seat.transform.position;
        if ((hero - transform.position).sqrMagnitude < settings.radius * settings.radius) screen.Play(settings);
    }
}
