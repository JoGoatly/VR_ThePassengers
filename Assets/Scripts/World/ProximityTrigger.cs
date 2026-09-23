using UnityEngine;

/// <summary>Runs an action once when the walking player comes close (scares, a phone that starts ringing).</summary>
public class ProximityTrigger : MonoBehaviour
{
    public float radius = 4f;
    public System.Action<ProximityTrigger> Triggered;

    void Update()
    {
        var onFoot = PlayerCombat.Instance != null ? PlayerCombat.Instance.onFoot : null;
        var player = GameUI.PlayerOutside && onFoot != null ? onFoot.Walker : null;
        if (player == null) return;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        if (d.magnitude > radius) return;
        enabled = false;
        Triggered?.Invoke(this);
    }
}
