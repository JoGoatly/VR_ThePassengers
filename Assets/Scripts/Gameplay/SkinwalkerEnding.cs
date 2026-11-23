using System.Collections;
using UnityEngine;

/// <summary>
/// The end of the last night (19 December): the power dies, the thing from the forest is
/// suddenly in front of the driver, then darkness - and the ending text, in which it applies
/// for the job the next morning wearing his skin.
/// </summary>
public class SkinwalkerEnding : MonoBehaviour
{
    public static void Play(MainMenu menu, bool good, GameObject creature)
    {
        var go = new GameObject("Skinwalker Ending");
        go.AddComponent<SkinwalkerEnding>().StartCoroutine(go.GetComponent<SkinwalkerEnding>().Run(menu, good, creature));
    }

    float blackFrom = -1f;
    string caption;

    IEnumerator Run(MainMenu menu, bool good, GameObject creature)
    {
        var lights = FindAnyObjectByType<BusLights>();
        var bus = FindAnyObjectByType<BusController>();
        if (bus != null) { bus.engineDead = true; bus.throttleLockReason = ""; }
        // The lights flicker and die.
        for (int i = 0; i < 6 && lights != null; i++)
        {
            lights.PowerCut = i % 2 == 0;
            yield return new WaitForSeconds(Random.Range(0.08f, 0.3f));
        }
        if (lights != null) lights.PowerCut = true;
        yield return new WaitForSeconds(1.6f);
        Jumpscare.PlaySkinwalker(creature);
        yield return new WaitForSeconds(1.7f);
        blackFrom = Time.time;
        caption = Loc.T("19.12.1998  -  06:01", "12/19/1998  -  6:01 AM");
        var sound = FindAnyObjectByType<SoundManager>();
        if (sound != null) sound.PlayScare();
        yield return new WaitForSeconds(4f);
        if (menu != null) menu.ShowEnding(good);
        Destroy(gameObject);
    }

    void OnGUI()
    {
        if (blackFrom < 0f) return;
        GUI.depth = -400;
        float w = RetroGUI.VirtualWidth, h = RetroGUI.VirtualHeight;
        RetroGUI.Fill(new Rect(0, 0, w, h), Color.black);
        float a = Mathf.Clamp01((Time.time - blackFrom - 1f) / 1.2f);
        RetroGUI.ShadowLabel(new Rect(0, h / 2 - 8, w, 16), caption, new Color(0.75f, 0.1f, 0.08f, a));
    }
}
