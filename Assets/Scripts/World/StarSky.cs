using UnityEngine;

/// <summary>
/// Night sky: a few thousand small stars, a faint milky way band and a pale moon on a big
/// sphere that follows the camera. Uses an unlit, fog-free material (like the lamp glow).
/// </summary>
[DefaultExecutionOrder(300)] // after the camera moved
public class StarSky : MonoBehaviour
{
    [Tooltip("Unlit PSX material without fog and draw distance (e.g. LampGlow)")]
    public Material glowMaterial;
    public int starCount = 1400;
    public int milkyWayStars = 900;
    public float radius = 250f;
    [Tooltip("Star size range in metres at 'radius' (about 3-4 screen pixels)")]
    public Vector2 starSize = new Vector2(2.2f, 3.6f);
    [Tooltip("Stars below this elevation are skipped (degrees)")]
    public float minElevation = 6f;
    public bool moon = true;
    public int seed = 77;

    Transform root;
    Material[] tiers;
    Color[] tierColors;
    float twinkleTimer;

    void Start()
    {
        if (glowMaterial == null) return;
        var rng = new System.Random(seed);
        root = new GameObject("Star Sky").transform;

        tierColors = new[]
        {
            new Color(0.35f, 0.37f, 0.42f), new Color(0.6f, 0.62f, 0.7f), new Color(0.95f, 0.95f, 1f), new Color(1f, 0.9f, 0.75f),
        };
        tiers = new Material[tierColors.Length];
        var builders = new MeshKit.Builder[tierColors.Length];
        for (int i = 0; i < tiers.Length; i++)
        {
            tiers[i] = new Material(glowMaterial) { name = "Stars " + i };
            SetColor(tiers[i], tierColors[i]);
            builders[i] = new MeshKit.Builder();
        }

        // Random stars (most of them faint).
        for (int i = 0; i < starCount; i++)
        {
            Vector3 dir = RandomSkyDirection(rng);
            double r = rng.NextDouble();
            int tier = r < 0.55 ? 0 : r < 0.85 ? 1 : r < 0.97 ? 2 : 3;
            float size = Mathf.Lerp(starSize.x, starSize.y, (float)rng.NextDouble()) * (tier >= 2 ? 1.25f : 1f);
            AddStar(builders[tier], dir, size);
        }

        // Milky way: a tilted band with many faint stars.
        Quaternion band = Quaternion.Euler(62f, 35f, 0f);
        for (int i = 0; i < milkyWayStars; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float spread = (float)(Gaussian(rng) * 0.12);
            Vector3 dir = band * new Vector3(Mathf.Cos(a), spread, Mathf.Sin(a)).normalized;
            if (Mathf.Asin(dir.y) * Mathf.Rad2Deg < minElevation) continue;
            AddStar(builders[rng.NextDouble() < 0.85 ? 0 : 1], dir, starSize.x);
        }

        for (int i = 0; i < tiers.Length; i++)
            MeshKit.Spawn("Stars " + i, root, builders[i].ToMesh("Stars"), tiers[i], Vector3.zero, Quaternion.identity, false);

        if (moon)
        {
            var mb = new MeshKit.Builder();
            Vector3 dir = Quaternion.Euler(-28f, -40f, 0f) * Vector3.forward;
            // Round-ish moon from a few quads (PSX style), plus a faint halo.
            AddDisc(mb, dir, 7f, 10);
            var moonMat = new Material(glowMaterial) { name = "Moon" };
            SetColor(moonMat, new Color(0.85f, 0.87f, 0.8f));
            MeshKit.Spawn("Moon", root, mb.ToMesh("Moon"), moonMat, Vector3.zero, Quaternion.identity, false);

            var halo = new MeshKit.Builder();
            AddDisc(halo, dir, 16f, 12, radius + 2f);
            var haloMat = new Material(glowMaterial) { name = "Moon Halo" };
            SetColor(haloMat, new Color(0.1f, 0.11f, 0.13f));
            MeshKit.Spawn("Moon Halo", root, halo.ToMesh("Moon Halo"), haloMat, Vector3.zero, Quaternion.identity, false);
        }
    }

    static void SetColor(Material m, Color c)
    {
        m.SetColor("_MainColor", c);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
    }

    Vector3 RandomSkyDirection(System.Random rng)
    {
        while (true)
        {
            var v = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble(), (float)rng.NextDouble() * 2 - 1);
            if (v.sqrMagnitude > 1f || v.sqrMagnitude < 0.01f) continue;
            v.Normalize();
            if (Mathf.Asin(v.y) * Mathf.Rad2Deg < minElevation) continue;
            return v;
        }
    }

    static double Gaussian(System.Random rng)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2 * System.Math.PI * u2);
    }

    void AddStar(MeshKit.Builder mb, Vector3 dir, float size)
    {
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
        right.Normalize();
        Vector3 up = Vector3.Cross(dir, right);
        Vector3 p = dir * radius;
        float h = size * 0.5f;
        mb.Quad(p - right * h - up * h, p - right * h + up * h, p + right * h + up * h, p + right * h - up * h, 1, 1, 1);
    }

    void AddDisc(MeshKit.Builder mb, Vector3 dir, float discRadius, int segments, float distance = -1f)
    {
        if (distance < 0f) distance = radius - 1f;
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        Vector3 up = Vector3.Cross(dir, right);
        Vector3 c = dir * distance;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 p0 = c + (right * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * discRadius;
            Vector3 p1 = c + (right * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * discRadius;
            mb.Tri(c, p1, p0);
        }
    }

    void LateUpdate()
    {
        if (root == null) return;
        var cam = Camera.main;
        if (cam != null) root.position = cam.transform.position;

        // Gentle twinkling of the bright stars.
        twinkleTimer -= Time.deltaTime;
        if (twinkleTimer > 0f) return;
        twinkleTimer = 0.12f;
        for (int i = 1; i < tiers.Length; i++)
            SetColor(tiers[i], tierColors[i] * Random.Range(0.8f, 1.05f));
    }
}
