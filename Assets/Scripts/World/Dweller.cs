using UnityEngine;

/// <summary>
/// Whoever lives in the houses off the road. Stands still until the player comes close,
/// then walks after them and hits. Can be beaten with the bat or shot; flees back home if
/// the player runs far enough. Uses the passenger walk animation.
/// </summary>
public class Dweller : MonoBehaviour
{
    public int health = 4;
    public float aggroRange = 11f;
    public float leashRange = 28f;
    public float chaseSpeed = 2.9f;
    public int damage = 20;
    public AudioClip growl, hitSound, vanish;
    public Vector3 home;
    public System.Action<Vector3> Died;

    Passenger walker;
    SoundManager sound;
    float repathAt, attackAt, staggerUntil, growlAt;
    bool chasing;

    void Start()
    {
        home = transform.position;
        walker = GetComponent<Passenger>();
        if (walker == null) walker = gameObject.AddComponent<Passenger>();
        walker.walkSpeed = chaseSpeed;
        sound = FindAnyObjectByType<SoundManager>();

        var col = gameObject.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f);
        col.height = 1.8f;
        col.radius = 0.35f;
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        damage += Progress.Day * 3;
    }

    void Update()
    {
        if (health <= 0) return;
        var onFoot = PlayerCombat.Instance != null ? PlayerCombat.Instance.onFoot : null;
        Transform player = GameUI.PlayerOutside && onFoot != null ? onFoot.Walker : null;

        Vector3 toPlayer = player != null ? player.position - transform.position : Vector3.positiveInfinity;
        toPlayer.y = 0f;
        float dist = player != null ? toPlayer.magnitude : float.MaxValue;
        bool playerNearHome = player != null && Vector3.Distance(player.position, home) < leashRange;

        if (!chasing && dist < aggroRange && playerNearHome)
        {
            chasing = true;
            Growl();
        }
        if (chasing && (!playerNearHome || player == null)) chasing = false;

        if (Time.time < staggerUntil) return;

        if (chasing)
        {
            if (Time.time > growlAt) Growl();
            if (dist < 1.4f)
            {
                walker.WalkPath(new Vector3[0], null, null, toPlayer.normalized);
                if (Time.time > attackAt)
                {
                    attackAt = Time.time + 1.2f;
                    PlayerCombat.Instance.Damage(damage);
                }
            }
            else if (Time.time > repathAt)
            {
                repathAt = Time.time + 0.2f;
                Vector3 target = player.position;
                target.y = transform.position.y;   // stays on its own floor
                walker.WalkPath(new[] { target }, null);
            }
        }
        else if (Time.time > repathAt && (transform.position - home).sqrMagnitude > 0.5f)
        {
            repathAt = Time.time + 0.5f;
            walker.WalkPath(new[] { home }, null);
        }
    }

    void Growl()
    {
        growlAt = Time.time + Random.Range(4f, 8f);
        if (sound != null && growl != null) sound.PlayWorld(growl, transform.position + Vector3.up * 1.6f, 1f);
    }

    public void TakeHit(int amount, Vector3 direction)
    {
        if (health <= 0) return;
        health -= amount;
        chasing = true;
        staggerUntil = Time.time + 0.45f;
        walker.WalkPath(new Vector3[0], null);
        transform.position += new Vector3(direction.x, 0f, direction.z).normalized * 0.35f;
        if (sound != null && hitSound != null) sound.PlayWorld(hitSound, transform.position + Vector3.up, 1f);
        if (health <= 0)
        {
            if (sound != null && vanish != null) sound.PlayWorld(vanish, transform.position + Vector3.up, 1f);
            Died?.Invoke(transform.position);
            Destroy(gameObject);
        }
    }
}
