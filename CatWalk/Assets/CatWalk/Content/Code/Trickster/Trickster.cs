using Febucci.TextAnimatorForUnity;
using TMPro;
using UnityEngine;

// Авантюрист — житель-котик в шарфе, линия трикстера. Встречи идут цепочкой (TricksterProfile.meetings): очередная
// открывается по своим условиям, и он ждёт кота на станции встречи — у точки из spots с именем её локации.
// F рядом — следующая реплика встречи; после последней встреча пройдена и растёт доверие. На другое место он переходит,
// только когда кот далеко. Встреча с offer открывает руины на карте и кладёт в них находку — посылку со своим заказом.
// После всех встреч ждёт там, где была последняя: кот ставит находку ему в слот (hand, F) — передача, дальше BlackHole.
// Находки нет ни в руинах, ни в руках (например, после загрузки) — она снова появляется в руинах.
// Реплики — облачко над головой, как у жителя (Resident).
//
// Факты (StoryFacts):
//   trickster.met         — встреч пройдено
//   trickster.trust       — доверие: сумма trust пройденных встреч
//   trickster.offer       — предложил руины: они на карте, находка лежит там
//   trickster.handed      — находка передана, исход неизбежен
//   trickster.hole        — 1 + секунд с взрыва (BlackHole); 0 — взрыва не было
//   trickster.void        — герой в «Вечной пустоте» (BlackHole)
//   trickster.clue.<id>   — найдена косвенная улика (TricksterClue)
public class Trickster : MonoBehaviour
{
    public TricksterProfile profile;
    public GameObject body;               // модель, облачко и слот: скрыт, пока его нет ни на одной станции
    public Transform visual;              // модель с пивотом в ногах, поворачивается к коту
    public Transform[] spots;             // где он стоит: имя точки — id локации станции (Location.id)
    public Location ruins;                // на карте только после предложения
    public ParcelSlot findSlot;           // где в руинах лежит находка
    public Parcel findPrefab;             // находка: посылка с заказом к авантюристу
    public ParcelSlot hand;               // сюда кот ставит находку

    [Header("Облачко")]
    public Canvas bubble;                 // world-space, всегда смотрит в камеру
    public TMP_Text text;
    public TypewriterComponent typewriter;
    public float lineTime = 6f;           // с; длинную — не меньше, чем печатается, и ещё 2 с
    public GameObject newsIcon;           // «!» — ждёт со встречей

    public float talkDistance = 2f;       // м; точки встреч — в 4,5 м от жителя, чтобы F не будило обоих
    public float lookRadius = 6f;         // ближе — смотрит на кота, м
    public float leaveDistance = 40f;     // переходит на другое место, только когда кот дальше, м
    public float turnSpeed = 180f;        // °/с

    Transform at;                         // точка, где стоит; null — нигде
    int line, idle;
    string pending;
    float hideAt, poll;

    void Awake()
    {
        bubble.enabled = false;
        body.SetActive(false);
    }

    void Update()
    {
        int met = StoryFacts.Get("trickster.met");
        bool offered = StoryFacts.Has("trickster.offer"), handed = StoryFacts.Has("trickster.handed");
        TricksterProfile.Meeting meeting = met < profile.meetings.Length ? profile.meetings[met] : null;
        bool ready = meeting != null && FactCondition.All(meeting.conditions);

        // Где ему быть: у открывшейся встречи; после всех — там же, где последняя; после взрыва — нигде.
        Transform want = StoryFacts.Has("trickster.hole") ? null
                       : meeting == null ? Spot(profile.meetings[^1].at) : ready ? Spot(meeting.at) : null;
        Transform cat = ParcelCarrier.Current != null ? ParcelCarrier.Current.transform : null;
        float distance = cat != null && at != null ? Vector3.Distance(cat.position, transform.position) : float.MaxValue;
        if (want != at && distance > leaveDistance && hand.IsFree) Move(want);

        ruins.enabled = offered;
        if ((poll -= Time.deltaTime) < 0f)
        {
            poll = 1f;
            if (offered && !handed) SpawnFind();
        }
        if (at == null) return;

        hand.gameObject.SetActive(offered && !handed || !hand.IsFree);
        if (!handed && !hand.IsFree && hand.Parcel.order == findPrefab.order) Handover();
        Turn(distance < lookRadius ? cat : null);

        bool here = ready && at == Spot(meeting.at);
        bool menu = MenuScreen.Opened != null;
        if (!menu && distance < talkDistance && !ParcelCarrier.Current.HasAction)
        {
            Hud.Prompt(Controls.Hint(Controls.Use, "поговорить"), 9);
            if (Controls.Pressed(Controls.Use)) Talk(here ? meeting : null);
        }

        if (!menu && pending != null) Show();
        bubble.enabled = !menu && Time.time < hideAt;
        newsIcon.SetActive(here && !bubble.enabled);
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (bubble.enabled && cam != null) bubble.transform.rotation = cam.transform.rotation;
    }

    Transform Spot(string id) => System.Array.Find(spots, s => s != null && s.name == id);

    void Move(Transform spot)
    {
        at = spot;
        line = 0;
        body.SetActive(spot != null);
        if (spot == null) return;
        transform.SetPositionAndRotation(spot.position, spot.rotation);
        visual.localRotation = Quaternion.identity;
    }

    // Встреча — реплика за репликой; без встречи — ожидание или болтовня по кругу.
    void Talk(TricksterProfile.Meeting meeting)
    {
        if (meeting != null)
        {
            Say(meeting.lines[line++]);
            if (line < meeting.lines.Length) return;
            line = 0;
            StoryFacts.Add("trickster.met");
            StoryFacts.Add("trickster.trust", meeting.trust);
            if (meeting.offer) Offer();
            return;
        }
        if (StoryFacts.Has("trickster.handed")) Say(profile.afterHandover);
        else if (StoryFacts.Has("trickster.offer")) Say(profile.waiting);
        else if (profile.idle.Length > 0) Say(profile.idle[idle++ % profile.idle.Length]);
    }

    void Offer()
    {
        StoryFacts.Set("trickster.offer", 1);
        ruins.enabled = true;
        if (Navigator.Current != null) Navigator.Current.Target = ruins;
        Hud.Toast($"{ruins.Label} — на карте");
    }

    // Находка одна: если её нет в мире, она лежит в руинах.
    void SpawnFind()
    {
        if (findSlot == null || !findSlot.IsFree || Parcel.All.Exists(p => p.order == findPrefab.order)) return;
        Parcel find = Instantiate(findPrefab);
        find.Attach(findSlot.transform, findSlot);
    }

    void Handover()
    {
        Parcel find = hand.Parcel;
        hand.Parcel = null;
        find.gameObject.SetActive(false);
        Destroy(find.gameObject);
        StoryFacts.Set("trickster.handed", 1);
        Say(profile.handover);
    }

    // Реплика показывается сразу, а если открыто меню — как только его закроют.
    void Say(string say)
    {
        if (!string.IsNullOrEmpty(say)) pending = say;
    }

    void Show()
    {
        bubble.enabled = true;
        typewriter.ShowText($"<b>{profile.displayName}</b>\n{pending}");
        hideAt = Time.time + Mathf.Max(lineTime, typewriter.GetApproximateShowDuration() + 2f);
        pending = null;
    }

    // Лицом к коту в плоскости пола; без кота — как поставили.
    void Turn(Transform cat)
    {
        Vector3 up = transform.up;
        Vector3 toCat = cat != null ? Vector3.ProjectOnPlane(cat.position - transform.position, up) : Vector3.zero;
        Quaternion target = toCat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCat, up) : transform.rotation;
        visual.rotation = Quaternion.RotateTowards(visual.rotation, target, turnSpeed * Time.deltaTime);
    }
}
