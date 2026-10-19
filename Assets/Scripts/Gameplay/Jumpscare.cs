using UnityEngine;

/// <summary>
/// A jump scare: the wrong passenger you let in is suddenly right in front of your face -
/// distorted, lit red from below, with a scream - for a fraction of a second.
/// </summary>
public class Jumpscare : MonoBehaviour
{
    public static bool Running { get; private set; }

    const float Duration = 0.85f;

    Transform model, head, leftArm, rightArm, cam;
    float start;
    Vector3 basePos;

    /// <summary>Show this character model as a jump scare (a copy; the original stays where it is).</summary>
    public static void Play(GameObject characterPrefab)
    {
        if (Running || characterPrefab == null || Camera.main == null || Tutorial.Active) return;
        var go = new GameObject("Jumpscare");
        go.AddComponent<Jumpscare>().Begin(characterPrefab, Camera.main.transform);
    }

    void Begin(GameObject prefab, Transform camera)
    {
        Running = true;
        start = Time.time;
        cam = camera;
        transform.SetParent(cam, false);
        model = Instantiate(prefab, transform).transform;
        foreach (var a in model.GetComponentsInChildren<Animator>()) a.enabled = false;
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
        PsxConvert.Apply(model.gameObject);
        head = Bone("Head");
        leftArm = Bone("LeftArm");
        rightArm = Bone("RightArm");

        // Face to face: the head right in front of the camera, looking at it.
        model.localRotation = Quaternion.Euler(0f, 180f, 0f);
        model.localPosition = Vector3.zero;
        Vector3 headLocal = head != null ? transform.InverseTransformPoint(head.position) : new Vector3(0f, 1.6f, 0f);
        basePos = new Vector3(-headLocal.x, -headLocal.y + 0.02f, 0.42f - headLocal.z);
        model.localPosition = basePos;

        // Lit red from below, the rest dark.
        var light = new GameObject("Scare Light").AddComponent<Light>();
        light.transform.SetParent(transform, false);
        light.transform.localPosition = new Vector3(0f, -0.35f, 0.15f);
        light.type = LightType.Point;
        light.color = new Color(1f, 0.15f, 0.08f);
        light.range = 2.5f;
        light.intensity = 4f;

        var sound = FindAnyObjectByType<SoundManager>();
        if (sound != null) sound.PlayScare();
    }

    void LateUpdate()
    {
        float t = Time.time - start;
        if (t > Duration || cam == null) { Destroy(gameObject); return; }
        // Lunges in, shakes, arms reaching for you.
        float lunge = Mathf.Clamp01(t / 0.12f);
        Vector3 shake = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * 0.025f;
        model.localPosition = basePos + new Vector3(0f, 0f, (1f - lunge) * 0.6f) + shake;
        if (head != null) head.localRotation *= Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(-15f, 15f), Random.Range(-20f, 20f));
        foreach (var arm in new[] { leftArm, rightArm })
            if (arm != null && arm.childCount > 0)
            {
                Vector3 current = arm.GetChild(0).position - arm.position;
                Vector3 wanted = (cam.position + cam.right * (arm == leftArm ? 0.25f : -0.25f) - arm.position).normalized;
                arm.rotation = Quaternion.FromToRotation(current, wanted) * arm.rotation;
            }
    }

    void OnDestroy() => Running = false;

    void OnGUI()
    {
        float t = Time.time - start;
        float w = RetroGUI.VirtualWidth, h = RetroGUI.VirtualHeight;
        // Red flash and black frames flickering.
        if (t < 0.08f || (t > 0.3f && t < 0.36f)) RetroGUI.Fill(new Rect(0, 0, w, h), new Color(0.6f, 0f, 0f, 0.55f));
        else if (Random.value < 0.15f) RetroGUI.Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.8f));
    }

    Transform Bone(string boneName)
    {
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            int colon = n.LastIndexOf(':');
            if (colon >= 0) n = n.Substring(colon + 1);
            if (n == boneName) return t;
        }
        return null;
    }
}
