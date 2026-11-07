using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Обучение первых минут: плашка с подсказкой текущего шага (своя, не Hud.Toast) и стрелка к цели, пока кот пешком.
// Шаги (TutorialStep) по порядку: виден первый непройденный, чьи условия показа выполнены сейчас; остальные ждут своей ситуации.
// Выполнил, что сказано (или шаг провисел timeout), — шаг пройден, факт tutorial.<id>, и больше не появляется.
// Пока открыто меню, плашка спрятана, если шаг не про это меню (visibleIn). Ввод не читает.
// Выключается настройкой «Подсказки обучения» (GameSettings.Tutorial). Первая проверка — через firstCheck:
// сохранение уже загружено и факты сняты с мира.
//
// Факты:
//   tutorial.<id>   — шаг пройден (1)
//   save.count      — сохранений в будке всего (ставит SaveGame)
public class Tutorial : MonoBehaviour
{
    public TutorialStep[] steps;
    public GameObject panel;              // плашка: прячется вместе с текстом и стрелкой
    public TMP_Text text;
    public RectTransform arrow;           // ось +Y смотрит на цель
    public TMP_Text distance;             // до цели
    public float firstCheck = 1.5f;       // с после старта
    public float poll = 0.25f;            // с между проверками

    CosmoCarSeat seat;
    CosmoCarController car;
    FuelTank tank;
    CarUpgrades upgrades;
    ParkingZone[] zones;
    PhoneBooth[] booths;
    FuelPump[] pumps;
    WorkshopTerminal[] workshops;

    TutorialStep current;
    float shown, wait;

    void Start()
    {
        seat = FindFirstObjectByType<CosmoCarSeat>();
        car = FindFirstObjectByType<CosmoCarController>();
        tank = FindFirstObjectByType<FuelTank>();
        upgrades = FindFirstObjectByType<CarUpgrades>();
        zones = FindObjectsByType<ParkingZone>(FindObjectsSortMode.None);
        booths = FindObjectsByType<PhoneBooth>(FindObjectsSortMode.None);
        pumps = FindObjectsByType<FuelPump>(FindObjectsSortMode.None);
        workshops = FindObjectsByType<WorkshopTerminal>(FindObjectsSortMode.None);
        wait = firstCheck;
        panel.SetActive(false);
    }

    void Update()
    {
        bool on = GameSettings.Tutorial;
        if (on && (wait -= Time.unscaledDeltaTime) <= 0f)
        {
            wait = poll;
            Pick();
        }

        bool visible = on && current != null && (MenuScreen.Opened == null || MenuScreen.Opened.GetType().Name == current.visibleIn);
        panel.SetActive(visible);
        if (!visible) return;
        shown += Time.unscaledDeltaTime;
        Arrow();
    }

    void Pick()
    {
        TutorialStep next = null;
        foreach (TutorialStep s in steps)
        {
            if (s == null || StoryFacts.Has(s.Fact) || !Met(s.show)) continue;
            if ((!s.done.IsEmpty && Met(s.done)) || (s == current && s.timeout > 0f && shown >= s.timeout))
            {
                StoryFacts.Set(s.Fact, 1);
                continue;
            }
            next = s;
            break;
        }
        if (next == current) return;
        current = next;
        shown = 0f;
        if (next != null) text.text = next.text;
    }

    bool Met(TutorialStep.Condition c)
    {
        if (!FactCondition.All(c.facts)) return false;
        if (c.all != null) foreach (TutorialStep.State s in c.all) if (!Is(s)) return false;
        if (c.none != null) foreach (TutorialStep.State s in c.none) if (Is(s)) return false;
        return string.IsNullOrEmpty(c.menu) || (MenuScreen.Opened != null && MenuScreen.Opened.GetType().Name == c.menu);
    }

    bool Is(TutorialStep.State state)
    {
        ParcelCarrier cat = ParcelCarrier.Current;
        switch (state)
        {
            case TutorialStep.State.Driving: return seat != null && seat.Driving;
            case TutorialStep.State.Walking: return seat != null && !seat.Driving;
            case TutorialStep.State.Docked: return Array.Exists(zones, z => z.IsDocked);
            case TutorialStep.State.Magnet: return Array.Exists(zones, z => z.IsPulling);
            case TutorialStep.State.NearPost:
                return cat != null && PostTerminal.All.Exists(t => Vector3.Distance(cat.transform.position, t.usePoint.position) < t.useDistance);
            case TutorialStep.State.ParcelWaiting: return Parcel.All.Exists(p => p.Slot != null && IsIssue(p.Slot));
            case TutorialStep.State.Holding: return cat != null && cat.Held != null;
            case TutorialStep.State.Loaded: return Parcel.All.Exists(OnCar);
            case TutorialStep.State.InReceiveZone: return PostTerminal.All.Exists(t => t.Received().Count > 0);
            case TutorialStep.State.Cruising: return car != null && car.Cruising;
            case TutorialStep.State.HasTarget: return Navigator.Current != null && Navigator.Current.Target != null;
            case TutorialStep.State.LowFuel: return tank != null && tank.Fuel < tank.capacity * 0.5f;
            case TutorialStep.State.CanUpgrade:
                if (upgrades == null || Wallet.Current == null) return false;
                foreach (CarUpgrades.Kind k in Enum.GetValues(typeof(CarUpgrades.Kind)))
                    if (!upgrades.IsMax(k) && upgrades.Cost(k) <= Wallet.Current.Balance) return true;
                return false;
        }
        return false;
    }

    static bool IsIssue(ParcelSlot slot) => PostTerminal.All.Exists(t => Array.IndexOf(t.issueSlots, slot) >= 0);

    bool OnCar(Parcel p) => p.Slot != null && car != null && p.Slot.transform.IsChildOf(car.transform);

    // Стрелка — только пешком: в полёте цель показывает FlightHud.
    void Arrow()
    {
        Camera cam = Camera.main;
        ParcelCarrier cat = ParcelCarrier.Current;
        Vector3 goal = default;
        bool found = cat != null && cam != null && TryTarget(current.target, cat.transform.position, out goal);
        float d = found ? Vector3.Distance(cat.transform.position, goal) : 0f;
        found &= d > 2f;                  // уже на месте
        arrow.gameObject.SetActive(found);
        distance.gameObject.SetActive(found);
        if (!found) return;

        Vector3 v = cam.WorldToViewportPoint(goal);
        var direction = new Vector2((v.x - 0.5f) * cam.aspect, v.y - 0.5f);
        if (v.z < 0f) direction = -direction;
        if (direction.sqrMagnitude < 1e-4f) direction = Vector2.up;
        arrow.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, direction));
        distance.text = Navigator.FormatDistance(d);
    }

    bool TryTarget(TutorialStep.Target target, Vector3 from, out Vector3 point)
    {
        var points = new List<Vector3>();
        switch (target)
        {
            case TutorialStep.Target.Post:
                foreach (PostTerminal t in PostTerminal.All) points.Add(t.usePoint.position);
                break;
            case TutorialStep.Target.Parcel:
                foreach (Parcel p in Parcel.All) if (p.Slot != null && IsIssue(p.Slot)) points.Add(p.transform.position);
                break;
            case TutorialStep.Target.Car:
                if (car != null) points.Add(car.transform.position);
                break;
            case TutorialStep.Target.Address:
                // Почта-адресат посылки в руках или на полке: её зона приёма.
                foreach (Parcel p in Parcel.All)
                {
                    if (p.order == null || (!OnCar(p) && (ParcelCarrier.Current == null || ParcelCarrier.Current.Held != p))) continue;
                    PostTerminal to = PostTerminal.Find(p.order.to);
                    if (to != null) points.Add(to.receiveSlots.Length > 0 ? to.receiveSlots[0].transform.position : to.usePoint.position);
                }
                break;
            case TutorialStep.Target.Booth:
                foreach (PhoneBooth b in booths) if (b != null) points.Add(b.usePoint.position);
                break;
            case TutorialStep.Target.Pump:
                foreach (FuelPump p in pumps) if (p != null) points.Add(p.transform.position);
                break;
            case TutorialStep.Target.Workshop:
                foreach (WorkshopTerminal w in workshops) if (w != null && !w.repairOnly) points.Add(w.usePoint.position);
                break;
        }

        point = default;
        float best = float.MaxValue;
        foreach (Vector3 p in points)
        {
            float d = (p - from).sqrMagnitude;
            if (d < best)
            {
                best = d;
                point = p;
            }
        }
        return points.Count > 0;
    }
}
