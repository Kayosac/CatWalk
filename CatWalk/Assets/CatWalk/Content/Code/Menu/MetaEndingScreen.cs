using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Мета-финал «Обрыв контакта»: мир распадается (всё, кроме машины и кота, мигает, дёргается и гаснет),
// идут реплики из MetaEndingSettings, потом игра имитирует аварийное завершение: кадр замирает
// (снимок экрана поверх всего, Time.timeScale 0), через паузу приложение закрывается. Окон ОС не подделываем.
// Сохранение не трогаем; перед «падением» в PlayerPrefs ставится метка — по ней главное меню показывает пасхалку.
// Это экран-меню: пока идёт, кот, машина и камеры стоят, Esc не работает.
public class MetaEndingScreen : MenuScreen
{
    public const string MarkerKey = "meta.contact_lost"; // PlayerPrefs: сколько раз финал дошёл до «падения»
    public static bool ContactLost => PlayerPrefs.GetInt(MarkerKey, 0) > 0;

    public TMP_Text line;                 // реплика внизу экрана
    public RawImage frame;                // замерший последний кадр

    MetaEndingSettings settings;

    public void Play(MetaEndingSettings s)
    {
        if (!Show()) return;
        settings = s;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        StoryFacts.Set("meta.contact", 1);
        StartCoroutine(Run());
    }

    protected override void Update() { }  // не закрывается

    IEnumerator Run()
    {
        line.text = "";
        frame.gameObject.SetActive(false);

        // Герой остаётся: машина и кот замирают на месте и не гаснут.
        var hero = new List<Transform>();
        CosmoCarSeat seat = FindFirstObjectByType<CosmoCarSeat>();
        if (seat != null)
        {
            Freeze(seat.transform, hero);
            if (seat.PlayerBody != null) Freeze(seat.PlayerBody, hero);
        }

        // Экранные HUD пропадают сразу — остаётся только этот экран.
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.isRootCanvas && c.transform != transform) c.enabled = false;

        var world = new List<Renderer>();
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.enabled && !hero.Exists(h => r.transform.IsChildOf(h))) world.Add(r);

        // Распад: с каждым шагом гаснет всё большая доля, остальное мигает и дёргается.
        for (float t = 0f; t < settings.collapseSeconds; t += 0.1f)
        {
            float gone = t / settings.collapseSeconds;
            foreach (Renderer r in world)
            {
                if (r == null || Random.value > 0.15f) continue;
                r.enabled = Random.value > gone;
                if (Random.value < 0.3f) r.transform.position += Random.insideUnitSphere * settings.glitchShift;
            }
            yield return new WaitForSecondsRealtime(0.1f);
        }
        foreach (Renderer r in world) if (r != null) r.enabled = false;

        // Последняя реплика остаётся на экране и замирает вместе с кадром.
        foreach (MetaEndingSettings.Line l in settings.lines)
        {
            line.text = l.text;
            yield return new WaitForSecondsRealtime(l.seconds);
        }

        // Метка — до «падения»: после перезапуска главное меню её увидит.
        PlayerPrefs.SetInt(MarkerKey, PlayerPrefs.GetInt(MarkerKey, 0) + 1);
        PlayerPrefs.Save();

        yield return new WaitForEndOfFrame();
        frame.texture = ScreenCapture.CaptureScreenshotAsTexture();
        frame.gameObject.SetActive(true);
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(settings.freezeSeconds);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Машину и кота — на месте: машина без управления, тела без скорости и сил.
    static void Freeze(Transform t, List<Transform> hero)
    {
        hero.Add(t);
        CosmoCarController car = t.GetComponent<CosmoCarController>();
        if (car != null) car.enabled = false;
        Rigidbody body = t.GetComponent<Rigidbody>();
        if (body == null) return;
        body.linearVelocity = body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
    }
}
