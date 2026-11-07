using System.Collections.Generic;
using System.IO;
using System.Text;
using Febucci.TextAnimatorCore.Time;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ВРЕМЕННЫЙ сборщик звонков домой (удалить после проверки). Повторный запуск обновляет, а не дублирует:
// ассеты звонков и каталог в Data/Phone/, линия домой с экраном звонка в Game.prefab, «!» над будкой в PhoneBooth.prefab.
// Сцены не трогает. Лог — Temp/calls_build.txt.
static class BuildPhoneTemp
{
    const string Data = "Assets/CatWalk/Data/Phone/";
    const string GamePath = "Assets/CatWalk/Prefabs/Game.prefab";
    const string BoothPath = "Assets/CatWalk/Prefabs/PhoneBooth.prefab";
    const string MenuWindowPath = "Assets/CatWalk/Prefabs/UI/MenuWindow.prefab";
    const string IconPath = "Assets/Plugins/Retro Arsenal/Prefabs/Interactive/Status Icons/Exclamation.prefab";
    const string TextAnimator = "Assets/Plugins/Febucci/Text Animator for Unity/";
    const string LogPath = "Temp/calls_build.txt";

    static readonly StringBuilder log = new();
    static readonly List<PhoneCall> made = new();   // звонки этого запуска -> каталог

    [MenuItem("CatWalk/Temp/Build Phone")]
    static void Build()
    {
        log.Clear();
        made.Clear();
        try
        {
            PhoneCallCatalog catalog = BuildCalls();
            BuildLine(catalog);
            BuildBooth();
            AssetDatabase.SaveAssets();
            log.AppendLine("Готово.");
            Debug.Log("Build Phone: готово.");
        }
        catch (System.Exception e)
        {
            log.AppendLine("ОШИБКА: " + e);
            Debug.LogException(e);
        }
        File.WriteAllText(LogPath, log.ToString());
    }

    static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new System.Exception("Build Phone: нет ассета " + path);
        return asset;
    }

    static T Ensure<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    static Transform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    // Game.prefab: «Phone line» с PhoneLine и экраном звонка «Call» из MenuWindow; реплика печатается в Panel/Status.
    static void BuildLine(PhoneCallCatalog catalog)
    {
        GameObject window = Load<GameObject>(MenuWindowPath);
        var timings = Load<TypingsTimingsScriptableBase>(TextAnimator + "Timings/Delays By Character.asset");
        var typewriterSettings = Load<TypewriterSettingsScriptable>(TextAnimator + "Settings/Shared Typewriter Settings.asset");

        using (var scope = new PrefabUtility.EditPrefabContentsScope(GamePath))
        {
            Transform root = scope.prefabContentsRoot.transform;
            Transform lineObject = Child(root, "Phone line");
            PhoneLine line = Ensure<PhoneLine>(lineObject.gameObject);
            line.catalog = catalog;

            Transform callObject = lineObject.Find("Call");
            if (callObject == null)
            {
                callObject = ((GameObject)PrefabUtility.InstantiatePrefab(window, lineObject)).transform;
                callObject.name = "Call";
            }
            PhoneCallScreen screen = Ensure<PhoneCallScreen>(callObject.gameObject);
            screen.title = callObject.Find("Panel/Title").GetComponent<TMP_Text>();
            screen.status = callObject.Find("Panel/Status").GetComponent<TMP_Text>();
            screen.list = callObject.Find("Panel/List");
            screen.rowTemplate = callObject.Find("Panel/Row").GetComponent<Button>();
            screen.closeOnE = false;
            screen.pauseTime = true;

            GameObject statusObject = screen.status.gameObject;
            screen.status.alignment = TextAlignmentOptions.TopLeft;
            PrefabUtility.RecordPrefabInstancePropertyModifications(screen.status);
            TextAnimator_TMP animator = Ensure<TextAnimator_TMP>(statusObject);
            animator.localSettings.timeScale = TimeScale.Unscaled; // игра на паузе, а реплика печатается
            TypewriterComponent typewriter = Ensure<TypewriterComponent>(statusObject);
            typewriter.TimingSettings = timings;
            typewriter.sharedSettings = typewriterSettings;
            screen.typewriter = typewriter;

            TMP_Text hint = callObject.Find("Panel/Hint").GetComponent<TMP_Text>();
            hint.text = "E, пробел или клик — дальше | Esc — повесить трубку";
            PrefabUtility.RecordPrefabInstancePropertyModifications(hint);

            line.screen = screen;
            log.AppendLine("Game.prefab: Phone line (PhoneLine) + Call (PhoneCallScreen, Text Animator на Panel/Status).");
        }
    }

    // PhoneBooth.prefab: «!» над будкой, пока ждёт сюжетный звонок.
    static void BuildBooth()
    {
        GameObject icon = Load<GameObject>(IconPath);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(BoothPath))
        {
            GameObject root = scope.prefabContentsRoot;
            Transform news = root.transform.Find("News");
            if (news == null)
            {
                news = ((GameObject)PrefabUtility.InstantiatePrefab(icon, root.transform)).transform;
                news.name = "News";
            }
            news.SetLocalPositionAndRotation(new Vector3(0f, 3.1f, 0f), Quaternion.identity);
            news.gameObject.SetActive(false);

            PhoneBooth booth = root.GetComponent<PhoneBooth>();
            if (booth == null) throw new System.Exception("Build Phone: в PhoneBooth.prefab нет PhoneBooth");
            booth.newsIcon = news.gameObject;
            log.AppendLine("PhoneBooth.prefab: News («!») на высоте 3,1 м.");
        }
    }

    // ---------- Данные ----------

    static PhoneCall.Line Me(string text) => new PhoneCall.Line { who = PhoneCall.Speaker.Hero, text = text };
    static PhoneCall.Line Mom(string text) => new PhoneCall.Line { who = PhoneCall.Speaker.Mom, text = text };
    static PhoneCall.Line Dad(string text) => new PhoneCall.Line { who = PhoneCall.Speaker.Dad, text = text };
    static PhoneCall.Line Machine(string text) => new PhoneCall.Line { who = PhoneCall.Speaker.Machine, text = text };

    static FactCondition AtLeast(string fact, int value) => new FactCondition { fact = fact, compare = FactCondition.Compare.AtLeast, value = value };
    static FactCondition Below(string fact, int value) => new FactCondition { fact = fact, compare = FactCondition.Compare.Below, value = value };

    static PhoneCall Call(string id, int priority, PhoneCall after, FactCondition[] conditions, string reminder, string[] reveals,
        params PhoneCall.Line[] lines)
    {
        if (!AssetDatabase.IsValidFolder("Assets/CatWalk/Data/Phone")) AssetDatabase.CreateFolder("Assets/CatWalk/Data", "Phone");
        string path = Data + "Call_" + id + ".asset";
        var call = AssetDatabase.LoadAssetAtPath<PhoneCall>(path);
        if (call == null)
        {
            call = ScriptableObject.CreateInstance<PhoneCall>();
            AssetDatabase.CreateAsset(call, path);
            log.AppendLine("Создан " + path);
        }
        else log.AppendLine("Обновлён " + path);
        call.id = id;
        call.priority = priority;
        call.repeatable = false;
        call.after = after;
        call.conditions = conditions;
        call.reminder = reminder;
        call.reveals = reveals;
        call.lines = lines;
        EditorUtility.SetDirty(call);
        made.Add(call);
        return call;
    }

    static PhoneCall Chat(string id, PhoneCall after, params PhoneCall.Line[] lines)
    {
        PhoneCall call = Call(id, 0, after, new FactCondition[0], "", new string[0], lines);
        call.repeatable = true;
        return call;
    }

    // ВРЕМЕННЫЕ тексты в тоне нарративной доски. Пороги — под 1-2 часа обычной игры:
    // долг 15000, плановый платёж 400 раз в 8 мин, досрочно по 1000.
    static PhoneCallCatalog BuildCalls()
    {
        PhoneCall hello = Call("hello", 100, null, new FactCondition[0], "",
            new[] { "Я снова вожу почту и живу в машине. Родителям звоню из будок на станциях." },
            Mom("Алло? Алло! Это ты? Отец, это он!"),
            Me("Это я, мам. Звоню из будки на станции. Тут пахнет лапшой и озоном."),
            Mom("Ты там ешь? Не одну лапшу?"),
            Me("Ещё озон. Шучу. Ем нормально. Снова вожу почту — машина тёплая, места хватает."),
            Dad("Раз тёплая — живём. Ты звони, слышишь? Мать вон у телефона дежурит."),
            Mom("Не дежурю. Просто рядом сижу. Тут удобно."),
            Me("Буду звонить. С каждой будки, где есть гудок."));

        PhoneCall study = Call("study", 90, hello, new[] { AtLeast("delivered", 3) },
            "Папа просил перезвонить. Говорит, «по делу».",
            new[] { "Когда-то я уехал учиться. Проучился недолго — пришлось вернуться к рейсам." },
            Dad("Ну что, почтальон. Сколько сегодня развёз?"),
            Me("Три. Одну хрупкую. Довёз целой, почти горжусь."),
            Dad("А учёба твоя? Ты же уезжал учиться. Книжки до сих пор в коробке стоят."),
            Me("Учёба подождёт, пап. Книжки терпеливые."),
            Dad("Они-то терпеливые. Ты вот у нас — не очень."),
            Me("Потому и вожу почту. Быстро, законно, без экзаменов."));

        PhoneCall bank = Call("bank", 80, study, new[] { AtLeast("delivered", 6) },
            "Мама просила перезвонить.",
            new[] { "У меня большой кредит. Банк продал его какой-то конторе — платить теперь им." },
            Mom("Тут письмо пришло. На твоё имя. Не из банка — из какой-то конторы."),
            Me("Не открывай, мам. Положи в ящик."),
            Mom("Я уже открыла. Написано, что теперь они вместо банка. Это как?"),
            Me("Это значит, у моего кредита новый хозяин. Бывает. Как у кота — переезд."),
            Mom("Ты бы сказал, если что-то плохое?"),
            Me("Сказал бы. Ничего плохого, просто бумажки. Мам, у меня заправка, я побежал."));

        PhoneCall missed = Call("missed", 85, hello, new[] { AtLeast("debt.missed", 1), AtLeast("debt.left", 1) },
            "Папа звонил. Просил перезвонить, «как сможешь».",
            new[] { "Когда я опаздываю с платежом, кредиторы звонят родителям." },
            Dad("Нам звонили. Вежливо так. Спрашивали, где ты и всё ли у тебя хорошо."),
            Me("...Что ты им сказал?"),
            Dad("Что хорошо. А что, не хорошо?"),
            Me("Хорошо. Опоздал с платежом, вот и беспокоятся. Заботливые."),
            Dad("Заботливые так не дышат в трубку."),
            Me("Пап. С незнакомых номеров не берите. Я сам заплачу, это моё."));

        PhoneCall threat40 = Call("threat40", 95, hello, new[] { AtLeast("debt.pressure", 40), AtLeast("debt.left", 1) },
            "Мама звонила три раза. Просила перезвонить.",
            new[] { "Кредиторы знают, где живут родители. Записка под дверью — их «привет»." },
            Mom("Под дверью записка. Без подписи. «Привет сыну». И всё."),
            Me("Порви и выкинь."),
            Mom("Я её на холодильник повесила. Почерк красивый."),
            Me("Мам. Сними. Пожалуйста. И закрывайте дверь на оба замка."),
            Mom("Ты что-то не договариваешь."),
            Me("Договариваю. Почерк правда красивый. Всё хорошо. Я заплачу, скоро."));

        Call("threat70", 96, threat40, new[] { AtLeast("debt.pressure", 70), AtLeast("debt.left", 1) },
            "Папа просил срочно перезвонить.",
            new[] { "Если я тяну с долгом, кредиторы приходят к дому родителей. Им страшно. Мне тоже, но им я этого не говорю." },
            Dad("Двое сидели у подъезда. Весь день. Ничего не делали, просто смотрели на наши окна."),
            Me("Они вас трогали?"),
            Dad("Нет. Мать им чаю вынесла. Вот этого я не понял."),
            Me("Пап, мне на днях бак слили прямо на стоянке, так что они теперь... Неважно. Сидите дома."),
            Dad("Как это — бак слили?"),
            Me("Никак. Шутка у курьеров такая. Я соберу деньги. Скоро. Обещаю."));

        Call("upgrade", 40, hello, new[] { AtLeast("upgrades", 1) }, "",
            new[] { "Машина — мой дом. Папа одобряет любой ремонт, кроме ремонта своей крыши." },
            Dad("Ты что-то с машиной сделал? Голос у тебя другой. Довольный."),
            Me("Поставил кое-что в мастерской. Теперь она едет, а не думает, ехать ли."),
            Dad("Правильно. Машина — это дом. Дом надо чинить."),
            Me("Ты так про крышу говорил. Двадцать лет."),
            Dad("И починил же!"),
            Mom("(издалека) Не починил!"));

        PhoneCall sister = Call("sister", 60, bank, new[] { AtLeast("delivered", 10) },
            "Мама просила перезвонить. Просто так, говорит.",
            new[] { "Мама вспомнила младшую сестру. Я сослался на заказ и не стал продолжать." },
            Mom("Я сегодня пекла те булочки. С маком. Которые она любила."),
            Me("..."),
            Mom("Получились. Целый противень. Папа съел три и ворчит, что мало мака."),
            Me("Мам... У меня тут заказ. Хрупкий. Потом поговорим, ладно?"),
            Mom("Ладно. Я тебе одну оставлю. Засушу, как раньше."));

        PhoneCall reason = Call("reason", 55, sister, new[] { AtLeast("debt.paid", 1200) },
            "Папа просил перезвонить. Сказал — разговор есть.",
            new[] { "Огромный кредит я взял сам — на дорогое лечение младшей сестры." },
            Dad("Мы с матерью посчитали, сколько ты уже отдал. Много."),
            Me("Осталось ещё больше. Не считайте, пап, это скучно."),
            Dad("Сын. Это ведь за её лечение. Ты один всё на себя взял."),
            Me("Клиника не ждала. Банк ждал меньше всех. Я бы снова пошёл и снова подписал."),
            Dad("Знаю. Потому и звоню. Сказать, что знаю."),
            Me("...Спасибо. Ладно. Мне на рейс."));

        Call("outcome", 50, reason, new[] { AtLeast("debt.paid", 2400), AtLeast("minutes", 40) },
            "Мама просила позвонить вечером.",
            new[] { "Лечение не помогло. Сестры больше нет. Долг остался." },
            Mom("Папа починил её велосипед. Зачем — сам не знает. Стоит в коридоре."),
            Me("Пусть стоит."),
            Mom("Я всё думаю: может, другая клиника... Может, раньше надо было..."),
            Me("Мам. Мы сделали всё. И врачи сделали. Лечение просто не помогло. Так бывает."),
            Mom("Ты так спокойно говоришь."),
            Me("Долго учился так говорить. Позвоню на неделе."),
            Mom("Ты всегда так говоришь."),
            Me("И всегда звоню."));

        Call("big", 65, hello, new[] { AtLeast("debt.paid", 5000), AtLeast("debt.left", 1) },
            "Папа просил перезвонить. Голос бодрый.",
            new[] { "Треть долга выплачена. Папа открыл праздничный лимонад." },
            Me("Пап, я треть отдал. Треть!"),
            Dad("Мать! Треть! ...Она спрашивает, треть чего."),
            Me("Всего. Скажи — треть огромного."),
            Dad("Я открыл ту бутылку. Лимонада. Берёг на праздник."),
            Me("Сколько лет берёг?"),
            Dad("Не твоё дело. Пузырьки ещё есть. Почти."));

        // Финал «Освобождение» — долг 0.
        Call("freedom", 200, null, new[] { Below("debt.left", 1), AtLeast("debt.paid", 1) },
            "Позвони домой. Теперь можно рассказать.",
            new[] { "Долг выплачен. Родители свободны. Я, кажется, тоже — и снова могу выбирать, как жить дальше." },
            Mom("Алло?"),
            Me("Мам. Всё. Последний платёж."),
            Mom("..."),
            Mom("Отец! Возьми вторую трубку!"),
            Dad("Взял. Что — всё? Совсем всё?"),
            Me("Совсем. Никто больше не придёт и не позвонит. Можно открывать дверь на звонок."),
            Dad("А ты-то теперь что?"),
            Me("Не знаю. Может, приеду. Не на рейс — просто. А там посмотрим. Может, доучусь."),
            Mom("Я булочки испеку."),
            Me("С маком."));

        Chat("chat_garden", hello,
            Mom("А у нас кабачки. Восемь штук. Куда их — не знаю."),
            Me("Пришли посылкой. Я знаю хорошую почту."),
            Mom("Так ты же её сам и привезёшь!"),
            Me("Вот и круговорот кабачков в природе."),
            Mom("Ешь там. Целую. Папа тоже целует, просто стесняется."));

        Chat("chat_machine", hello,
            Machine("Вы позвонили... Отец, куда говорить? ...Говорите после сигнала. Би-ип. Это я сказала «би-ип», сигнала нет."),
            Me("Привет, это я. Жив, сыт, лечу. Перезвоню. Автоответчик у вас отличный."));

        string catalogPath = Data + "PhoneCalls.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<PhoneCallCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PhoneCallCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }
        catalog.note = "ВРЕМЕННЫЕ тексты-заглушки в тоне нарративной доски («Дальняя почта — нарратив и мир»). "
                     + "Условия и пороги — под 1-2 часа обычной игры. Перепишет автор.";
        catalog.names = new[] { "Я", "Мама", "Папа", "Автоответчик" };
        catalog.storyGap = 3;
        catalog.calls = made.ToArray();
        EditorUtility.SetDirty(catalog);
        log.AppendLine($"Каталог {catalogPath}: звонков {catalog.calls.Length}.");
        return catalog;
    }
}
