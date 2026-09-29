using UnityEngine;

/// <summary>
/// A petrol pump: park the bus next to it, get out, E starts filling (E again stops).
/// Costs money per litre; stops by itself when the tank is full or the money runs out.
/// </summary>
public class FuelPump : Interactable
{
    [Tooltip("Tank fraction per second")]
    public float rate = 0.06f;
    [Tooltip("Euros per 5 % of a tank")]
    public int pricePerStep = 1;
    public float maxBusDistance = 16f;
    public AudioClip pumpLoop;

    bool filling;
    float paidUpTo;
    AudioSource loop;

    BusController Bus => FindAnyObjectByType<BusController>();

    public override string Prompt
    {
        get
        {
            if (!Features.Has(Feature.Fuel)) return Loc.T("Zapfsäule (Tank ist heute voll)", "Pump (the tank is full tonight)");
            if (filling) return Loc.T($"Tanken stoppen  ({Mathf.RoundToInt(Progress.Data.fuel * 100)}%)", $"Stop filling  ({Mathf.RoundToInt(Progress.Data.fuel * 100)}%)");
            return Loc.T($"Tanken ({pricePerStep} € / 5%)  - Tank {Mathf.RoundToInt(Progress.Data.fuel * 100)}%", $"Refuel ({pricePerStep} € / 5%)  - tank {Mathf.RoundToInt(Progress.Data.fuel * 100)}%");
        }
    }

    public override void Use()
    {
        var game = FindAnyObjectByType<BoardingManager>();
        if (!Features.Has(Feature.Fuel)) return;
        if (filling) { Stop(); return; }
        var bus = Bus;
        if (bus == null || Vector3.Distance(bus.transform.position, transform.position) > maxBusDistance)
        {
            game?.ShowToast(Loc.T("Der Bus steht zu weit weg. Näher an die Zapfsäule fahren!", "The bus is too far away. Park closer to the pump!"));
            return;
        }
        if (Progress.Data.fuel >= 0.999f) { game?.ShowToast(Loc.T("Der Tank ist voll.", "The tank is full.")); return; }
        filling = true;
        paidUpTo = Progress.Data.fuel;
        if (pumpLoop != null)
        {
            loop = gameObject.AddComponent<AudioSource>();
            loop.clip = pumpLoop;
            loop.loop = true;
            loop.spatialBlend = 1f;
            loop.maxDistance = 20f;
            loop.volume = 0.5f * GameSettings.Effects;
            loop.Play();
        }
    }

    void Stop()
    {
        filling = false;
        if (loop != null) Destroy(loop);
        Progress.Save();
        var game = FindAnyObjectByType<BoardingManager>();
        game?.ShowToast(Loc.T($"Tank: {Mathf.RoundToInt(Progress.Data.fuel * 100)}%", $"Tank: {Mathf.RoundToInt(Progress.Data.fuel * 100)}%"));
    }

    void Update()
    {
        if (!filling) return;
        var onFoot = PlayerCombat.Instance != null ? PlayerCombat.Instance.onFoot : null;
        bool near = onFoot != null && onFoot.Walker != null && Vector3.Distance(onFoot.Walker.position, transform.position) < radius + 1.5f;
        if (!near || Progress.Data.fuel >= 1f) { Stop(); return; }
        ShiftRules.Refuel(rate * Time.deltaTime);
        // Pay every 5 %.
        while (Progress.Data.fuel - paidUpTo >= 0.05f)
        {
            if (Progress.Money < pricePerStep)
            {
                FindAnyObjectByType<BoardingManager>()?.ShowToast(Loc.T("Kein Geld mehr!", "Out of money!"));
                Progress.Data.fuel = paidUpTo;
                Stop();
                return;
            }
            Progress.AddMoney(-pricePerStep);
            paidUpTo += 0.05f;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (filling) Stop();
    }
}
