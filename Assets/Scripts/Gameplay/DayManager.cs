using UnityEngine;

/// <summary>
/// One night on line 13: serve tonight's passengers (quota), get paid for right decisions
/// and fined for wrong ones, then drive to the depot to end the shift. Seven nights, each
/// one harder and creepier. On the last night the depot decides the ending.
/// </summary>
public class DayManager : MonoBehaviour
{
    public BoardingManager game;
    public BusController bus;
    public ForestRoad road;
    public BusRadio radio;
    public MainMenu menu;

    [Tooltip("Sound of the big 'TAG 1' title when the shift starts")]
    public AudioClip titleSound;

    [Header("Money")]
    public int correctPay = 25;
    public int wrongFine = 40;
    public int baseWage = 60;
    public int wagePerNight = 10;

    public static int QuotaFor(int day) => 4 + day;

    /// <summary>0 on the first night, 1 on the last.</summary>
    public static float Dread => (Progress.Day - 1) / (float)(Progress.LastDay - 1);

    bool quotaDone, ended, arrivedAtDepot;
    float titleAt = -1f;
    float nextHijackAt;

    public int Quota => QuotaFor(Progress.Day);

    /// <summary>The bus stands at the depot: the shift can be ended at the office PC.</summary>
    public bool AtDepot => arrivedAtDepot && !ended;

    /// <summary>Clocking out at the depot office PC ends the night.</summary>
    public void ClockOut()
    {
        if (ended) return;
        var onFoot = FindAnyObjectByType<PlayerOnFoot>();
        if (onFoot != null) onFoot.ForceEnter();
        EndShift(false);
    }

    void Start()
    {
        if (game == null) game = FindAnyObjectByType<BoardingManager>();
        if (bus == null) bus = FindAnyObjectByType<BusController>();
        if (road == null) road = FindAnyObjectByType<ForestRoad>();
        if (radio == null) radio = FindAnyObjectByType<BusRadio>();
        if (menu == null) menu = FindAnyObjectByType<MainMenu>();
        Progress.ResetShift();
        if (game != null) game.Judged += OnJudged;
        nextHijackAt = Time.time + Random.Range(180f, 360f);
    }

    void OnJudged(bool correct, IdCard card)
    {
        if (correct)
        {
            Progress.ShiftCorrect++;
            Progress.ShiftEarned += correctPay;
            Progress.AddMoney(correctPay);
            game.ShowToast($"+{correctPay} €");
        }
        else
        {
            Progress.ShiftWrong++;
            Progress.ShiftFines += wrongFine;
            Progress.AddMoney(-wrongFine);
            game.ShowToast(Loc.T($"-{wrongFine} € (Fehler)", $"-{wrongFine} € (mistake)"));
        }
        Progress.Save();
    }

    void Update()
    {
        if (ended || game == null || bus == null || road == null || GameUI.MenuOpen) return;
        if (titleAt < 0f)
        {
            // The shift begins: show the day big at the top.
            titleAt = Time.time;
            var sm = FindAnyObjectByType<SoundManager>();
            if (sm != null && titleSound != null && Camera.main != null) sm.PlayWorld(titleSound, Camera.main.transform.position, 0.9f, 0f);
        }
        float busS = road.BusArcLength;

        if (!quotaDone && game.Decisions >= Quota && game.CurrentPhase == BoardingManager.Phase.Driving)
        {
            quotaDone = true;
            game.ServingDone = true;
            // The last riders get off at the next stop, then comes the depot.
            road.RequestDepot(game.FinishRoute());
            game.Mail.Send(Loc.T("Leitstelle", "Dispatch"), Loc.T("Schichtende", "End of shift"), Loc.T(
                "Das waren die Fahrgäste für heute Nacht. Fahren Sie zum Betriebshof, stellen Sie den Bus ab und stempeln Sie sich im Büro am PC aus.\n\nLeitstelle",
                "Those were tonight's passengers. Drive to the depot, park the bus and clock out at the PC in the office.\n\nDispatch"), game.ClockText);
        }

        // From night 4 the radio sometimes tunes itself to 66.6.
        if (Progress.Day >= 4 && radio != null && Time.time > nextHijackAt)
        {
            nextHijackAt = Time.time + Random.Range(240f, 420f) / (0.6f + Dread);
            radio.Hijack();
        }

        var depot = road.Depot;
        if (!quotaDone || depot == null) return;

        // Stopped at the depot: time to get out and clock out in the office.
        if (!arrivedAtDepot && Mathf.Abs(bus.Speed) < 0.3f && Mathf.Abs(busS - depot.arcLength) < 25f)
        {
            arrivedAtDepot = true;
            game.ShowToast(Loc.T("Betriebshof erreicht. Aussteigen und im Büro am PC ausstempeln.",
                                 "Depot reached. Get out and clock out at the office PC."));
        }

        // Drove past the depot.
        if (busS > depot.arcLength + 70f)
        {
            if (Progress.Day == Progress.LastDay && Progress.Data.drivers.Count >= Story.Drivers.Length)
            {
                EndShift(true);   // the way out Jens Keller wrote about
                return;
            }
            arrivedAtDepot = false;
            road.ForgetDepot();
            road.RequestDepot();
            game.ShowToast(Loc.T("Der Betriebshof liegt hinter Ihnen... die Leitstelle schickt Sie zum nächsten.",
                                 "The depot is behind you... dispatch sends you to the next one."));
        }
    }

    void EndShift(bool escaped)
    {
        ended = true;
        int wage = baseWage + wagePerNight * Progress.Day;
        if (Progress.Day == Progress.LastDay)
        {
            bool good = escaped && Progress.Data.drivers.Count >= Story.Drivers.Length;
            Progress.DeleteSave();
            if (menu != null) menu.ShowEnding(good);
            return;
        }
        Progress.AddMoney(wage);
        Progress.Data.day++;
        Progress.Save();
        if (menu != null) menu.ShowShiftEnd(wage);
    }

    GUIStyle titleStyle, subStyle;
    float titleScale;

    // "TAG 1" big and red at the top for a few seconds when the shift starts.
    void DrawDayTitle()
    {
        float t = Time.time - titleAt;
        const float duration = 5f;
        if (titleAt < 0f || t > duration) return;
        float a = t < 0.9f ? 0f : t < 1.6f ? (t - 0.9f) / 0.7f : t > duration - 1.2f ? (duration - t) / 1.2f : 1f;
        if (a <= 0f) return;
        if (titleStyle == null || !Mathf.Approximately(titleScale, RetroGUI.Scale))
        {
            titleScale = RetroGUI.Scale;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(34 * titleScale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            subStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(8 * titleScale), alignment = TextAnchor.MiddleCenter };
        }
        float w = RetroGUI.VirtualWidth;
        // Dark band behind it.
        RetroGUI.Fill(new Rect(0, 22, w, 64), new Color(0f, 0f, 0f, 0.55f * a));
        string title = Loc.T($"TAG {Progress.Day}", $"DAY {Progress.Day}");
        // Letter spacing and a slight tremble.
        string spaced = string.Join(" ", title.ToCharArray());
        float shake = t < 2.2f ? (2.2f - t) * 1.5f : 0.3f;
        Vector2 j = new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));
        titleStyle.normal.textColor = new Color(0f, 0f, 0f, a);
        GUI.Label(RetroGUI.R(j.x + 2, 26 + j.y + 2, w, 44), spaced, titleStyle);
        titleStyle.normal.textColor = new Color(0.78f, 0.05f, 0.03f, a);
        GUI.Label(RetroGUI.R(j.x, 26 + j.y, w, 44), spaced, titleStyle);
        var date = CitizenRegistry.Today;
        subStyle.normal.textColor = new Color(0.8f, 0.75f, 0.68f, a * 0.9f);
        GUI.Label(RetroGUI.R(0, 68, w, 14), Loc.T($"NACHTLINIE 13  -  {date:dd.MM.yyyy}  -  23:40", $"NIGHT LINE 13  -  {date:dd.MM.yyyy}  -  11:40 PM"), subStyle);
    }

    void OnGUI()
    {
        if (GameUI.MenuOpen || game == null) return;
        DrawDayTitle();
        string text = Loc.T($"NACHT {Progress.Day}/{Progress.LastDay}", $"NIGHT {Progress.Day}/{Progress.LastDay}") +
                      $"   {Mathf.Min(game.Decisions, Quota)}/{Quota}   {Progress.Money} €";
        RetroGUI.ShadowLabel(new Rect(8, 6, 220, 14), text, new Color(0.85f, 0.8f, 0.7f), true, TextAnchor.UpperLeft);
    }
}
