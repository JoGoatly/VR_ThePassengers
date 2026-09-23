using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The inside of the bus in bus space (the bus root's local space): the seats, where you can
/// walk (aisle, door steps, the raised rear), the floor height, the driver's area.
/// Measured in the FBX (model space, x = backwards, z = to the door side) and mapped onto
/// the bus with the pivots of the model parts, so it fits however the model is scaled.
/// </summary>
public class BusInterior
{
    // Seat cushion centres in model space (y = top of the cushion).
    static readonly Vector3[] SeatsModel =
    {
        // Low floor, both sides (the door side has no seats at the rear door).
        new Vector3(-0.940f, 0.414f, -0.309f), new Vector3(-0.940f, 0.414f, 0.319f),
        new Vector3(-0.598f, 0.414f, -0.309f), new Vector3(-0.598f, 0.414f, 0.319f),
        new Vector3(-0.277f, 0.414f, -0.309f), new Vector3(-0.277f, 0.414f, 0.319f),
        new Vector3(0.045f, 0.414f, -0.309f), new Vector3(0.045f, 0.414f, 0.319f),
        new Vector3(0.371f, 0.414f, -0.309f), new Vector3(0.693f, 0.414f, -0.309f),
        // Raised rear.
        new Vector3(1.064f, 0.548f, -0.309f), new Vector3(1.064f, 0.548f, 0.319f),
        new Vector3(1.384f, 0.548f, -0.309f), new Vector3(1.384f, 0.548f, 0.319f),
        new Vector3(1.706f, 0.548f, -0.309f), new Vector3(1.706f, 0.548f, 0.319f),
        new Vector3(2.045f, 0.548f, -0.309f), new Vector3(2.045f, 0.548f, -0.062f),
        new Vector3(2.045f, 0.548f, 0.066f), new Vector3(2.045f, 0.548f, 0.319f),
    };

    const float LowFloor = 0.296f, HighFloor = 0.43f;

    // Walkable areas in model space: (xMin, xMax, zMin, zMax).
    static readonly Vector4[] WalkModel =
    {
        new Vector4(-2.05f, 0.95f, -0.10f, 0.10f),   // aisle
        new Vector4(-2.05f, -1.35f, -0.10f, 0.45f),  // front door area
        new Vector4(0.20f, 0.85f, -0.10f, 0.45f),    // rear door area
        new Vector4(0.95f, 1.95f, -0.08f, 0.08f),    // aisle of the raised rear
    };

    static readonly (string name, Vector3 model)[] Anchors =
    {
        ("SteeringWheel", new Vector3(-1.97861f, 0.55869f, -0.36051f)),
        ("Wheel_FL", new Vector3(-1.26508f, 0.20523f, 0.48111f)),
        ("Wheel_FR", new Vector3(-1.26508f, 0.20523f, -0.48111f)),
        ("Wheels_B", new Vector3(1.07503f, 0.20523f, 0f)),
    };

    public readonly Transform bus;
    Matrix4x4 toBus, toModel;
    public bool Valid { get; private set; }

    public int SeatCount => SeatsModel.Length;

    static readonly Dictionary<Transform, BusInterior> cache = new Dictionary<Transform, BusInterior>();

    public static BusInterior Of(Transform bus)
    {
        if (bus == null) return null;
        if (!cache.TryGetValue(bus, out var inside) || inside == null)
            cache[bus] = inside = new BusInterior(bus);
        return inside;
    }

    BusInterior(Transform bus)
    {
        this.bus = bus;
        var p = Matrix4x4.identity;
        var w = Matrix4x4.identity;
        for (int i = 0; i < Anchors.Length; i++)
        {
            var t = Find(bus, Anchors[i].name);
            if (t == null) return;
            Vector3 local = bus.InverseTransformPoint(t.position);
            p.SetColumn(i, new Vector4(Anchors[i].model.x, Anchors[i].model.y, Anchors[i].model.z, 1f));
            w.SetColumn(i, new Vector4(local.x, local.y, local.z, 1f));
        }
        if (Mathf.Abs(p.determinant) < 1e-6f) return;
        toBus = w * p.inverse;
        toModel = toBus.inverse;
        Valid = true;
    }

    public Vector3 ToBus(Vector3 model) => toBus.MultiplyPoint3x4(model);
    public Vector3 ToModel(Vector3 busLocal) => toModel.MultiplyPoint3x4(busLocal);

    /// <summary>Forward (towards the driver) in bus space.</summary>
    public Vector3 Forward => toBus.MultiplyVector(Vector3.left).normalized;

    /// <summary>Top of the seat cushion, bus space.</summary>
    public Vector3 Seat(int i) => ToBus(SeatsModel[i]);

    /// <summary>The spot in the aisle next to a seat, on the floor (bus space).</summary>
    public Vector3 AisleNextTo(int i)
    {
        var m = SeatsModel[i];
        bool rear = m.x > 0.95f;
        return ToBus(new Vector3(m.x, rear ? HighFloor : LowFloor, 0f));
    }

    /// <summary>Floor height at a point (bus space in, bus space y out).</summary>
    public float FloorAt(Vector3 busLocal)
    {
        var m = ToModel(busLocal);
        float y = Mathf.Lerp(LowFloor, HighFloor, Mathf.InverseLerp(0.85f, 1.1f, m.x));
        return ToBus(new Vector3(m.x, y, m.z)).y;
    }

    /// <summary>The nearest point you can stand on (bus space in and out, y ignored).</summary>
    public Vector3 ClampWalk(Vector3 busLocal)
    {
        var m = ToModel(busLocal);
        Vector3 best = m;
        float bestD = float.MaxValue;
        foreach (var r in WalkModel)
        {
            var c = new Vector3(Mathf.Clamp(m.x, r.x, r.y), m.y, Mathf.Clamp(m.z, r.z, r.w));
            float d = (c - m).sqrMagnitude;
            if (d < bestD) { bestD = d; best = c; }
        }
        var result = ToBus(best);
        result.y = FloorAt(result);
        return result;
    }

    /// <summary>Near the front door (to get out).</summary>
    public bool AtFrontDoor(Vector3 busLocal)
    {
        var m = ToModel(busLocal);
        return m.x < -1.45f && m.z > 0.2f;
    }

    /// <summary>Near the driver's seat.</summary>
    public bool AtDriverSeat(Vector3 busLocal)
    {
        var m = ToModel(busLocal);
        return m.x < -1.55f && m.z < 0.12f;
    }

    /// <summary>Where you stand after getting up from the driver's seat.</summary>
    public Vector3 StandUpSpot => ClampWalk(ToBus(new Vector3(-1.6f, LowFloor, 0f)));

    /// <summary>Fuse box on the wall behind the driver (bus space).</summary>
    public Vector3 FuseBox => ToBus(new Vector3(-1.45f, 0.62f, -0.5f));

    /// <summary>Metres per model unit (the bus model is scaled up).</summary>
    public float Scale => toBus.MultiplyVector(Vector3.right).magnitude;

    static Transform Find(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var f = Find(child, name);
            if (f != null) return f;
        }
        return null;
    }
}
