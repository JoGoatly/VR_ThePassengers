using UnityEngine;

/// <summary>A trapdoor or ladder: takes the player on foot to another place (cellar and back).</summary>
public class Trapdoor : Interactable
{
    public Vector3 target;
    public Vector3 lookDirection = Vector3.forward;
    public bool down = true;
    public AudioClip sound;

    public override string Prompt => down ? Loc.T("Falltür öffnen", "Open trapdoor") : Loc.T("Leiter hochklettern", "Climb the ladder");

    public override void Use()
    {
        var onFoot = PlayerCombat.Instance != null ? PlayerCombat.Instance.onFoot : FindAnyObjectByType<PlayerOnFoot>();
        if (onFoot == null) return;
        PlayerCombat.Fade(1.1f);
        var sm = FindAnyObjectByType<SoundManager>();
        if (sm != null && sound != null) sm.PlayWorld(sound, transform.position, 1f, 0.3f);
        onFoot.TeleportTo(target, lookDirection);
    }
}
